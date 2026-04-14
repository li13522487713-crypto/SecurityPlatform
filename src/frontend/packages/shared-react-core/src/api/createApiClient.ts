import type { ApiResponse, AuthTokenResult, PagedRequest } from "../types/index";
import {
  clearAntiforgeryToken,
  clearAuthStorage,
  getAccessToken,
  getAuthProfile,
  getAntiforgeryToken,
  getClientContextHeaders,
  getProjectId,
  getProjectScopeEnabled,
  getRefreshToken,
  getTenantId,
  setTenantId,
  setAccessToken,
  setAntiforgeryToken,
  setRefreshToken
} from "../utils/index";

export interface RequestOptions {
  disableAutoRefresh?: boolean;
  isRetry?: boolean;
  idempotencyKey?: string;
  antiforgeryToken?: string;
  antiforgeryRetry?: boolean;
  suppressErrorMessage?: boolean;
}

export interface SharedApiClientConfig {
  resolveRequestUrl: (path: string) => string;
  onUnauthorized: () => void | Promise<void>;
  includeProjectScopeHeader?: boolean;
}

interface SharedApiClient {
  persistTokenResult: (result: AuthTokenResult) => void;
  requestApi: <T>(path: string, init?: RequestInit, options?: RequestOptions) => Promise<T>;
  requestApiBlob: (path: string, init?: RequestInit, options?: RequestOptions) => Promise<Blob>;
  toQuery: (pagedRequest: PagedRequest, extra?: Record<string, string | undefined>) => string;
  requestPagedApi: <T>(
    path: string,
    params: PagedRequest,
    extra?: Record<string, string | undefined>,
    options?: RequestOptions
  ) => Promise<{ items: T[]; total: number; pageIndex: number; pageSize: number }>;
  uploadFile: (
    path: string,
    file: File,
    options?: RequestOptions
  ) => Promise<ApiResponse<{ id: number; originalName: string }>>;
  downloadFile: (path: string) => Promise<void>;
}

export function createApiClient(config: SharedApiClientConfig): SharedApiClient {
  let refreshPromise: Promise<boolean> | null = null;
  let antiforgeryPromise: Promise<string | null> | null = null;

  function extractAntiforgeryToken(
    response: ApiResponse<{ token?: string; Token?: string } | null | undefined>
  ): string | null {
    return response.data?.token ?? response.data?.Token ?? null;
  }

  function isUnsafeMethod(method: string) {
    return !["GET", "HEAD", "OPTIONS", "TRACE"].includes(method);
  }

  function generateIdempotencyKey(): string {
    if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
      return crypto.randomUUID();
    }
    return `idem-${Date.now()}-${Math.random().toString(16).slice(2)}`;
  }

  function persistTokenResult(result: AuthTokenResult) {
    setAccessToken(result.accessToken);
    setRefreshToken(result.refreshToken);
  }

  function tryParsePayload(text: string): { code?: string; message?: string; title?: string } | null {
    if (!text) return null;
    try {
      const parsed = JSON.parse(text) as {
        code?: string;
        Code?: string;
        message?: string;
        Message?: string;
        title?: string;
        Title?: string;
      };
      return {
        code: parsed.code ?? parsed.Code,
        message: parsed.message ?? parsed.Message,
        title: parsed.title ?? parsed.Title
      };
    } catch {
      return null;
    }
  }

  async function ensureAntiforgeryToken(): Promise<string | null> {
    const cached = getAntiforgeryToken();
    if (cached) return cached;
    if (antiforgeryPromise) return antiforgeryPromise;
    antiforgeryPromise = (async () => {
      try {
        const response = await requestApi<ApiResponse<{ token?: string; Token?: string }>>("/secure/antiforgery", {
          method: "GET"
        }, { disableAutoRefresh: true });
        const token = extractAntiforgeryToken(response);
        if (token) setAntiforgeryToken(token);
        return token;
      } catch {
        return null;
      } finally {
        antiforgeryPromise = null;
      }
    })();
    return antiforgeryPromise;
  }

  async function tryRefreshTokens(): Promise<boolean> {
    const refreshTokenValue = getRefreshToken();
    if (!refreshTokenValue || !getTenantId()) {
      clearAuthStorage();
      return false;
    }

    if (refreshPromise) return refreshPromise;
    refreshPromise = (async () => {
      try {
        const response = await requestApi<ApiResponse<AuthTokenResult>>("/auth/refresh", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ refreshToken: refreshTokenValue })
        }, { disableAutoRefresh: true, suppressErrorMessage: true });
        if (!response.data) return false;
        persistTokenResult(response.data);
        return true;
      } catch {
        clearAuthStorage();
        return false;
      } finally {
        refreshPromise = null;
      }
    })();
    return refreshPromise;
  }

  async function attachCommonHeaders(headers: Headers, method: string, options?: RequestOptions) {
    const token = getAccessToken();
    let tenantId = getTenantId();
    if (!tenantId && token) {
      const profileTenantId = getAuthProfile()?.tenantId?.trim();
      if (profileTenantId) {
        tenantId = profileTenantId;
        setTenantId(profileTenantId);
      }
    }
    const shouldAttachSecurityHeaders = Boolean(token);

    if (token) headers.set("Authorization", `Bearer ${token}`);
    if (tenantId && !headers.has("X-Tenant-Id")) headers.set("X-Tenant-Id", tenantId);

    const clientHeaders = getClientContextHeaders();
    (Object.entries(clientHeaders) as [string, string][]).forEach(([key, value]) => {
      if (value && !headers.has(key)) headers.set(key, value);
    });

    if (config.includeProjectScopeHeader) {
      const projectScopeEnabled = getProjectScopeEnabled();
      const projectId = getProjectId();
      if (projectScopeEnabled && projectId && !headers.has("X-Project-Id")) {
        headers.set("X-Project-Id", projectId);
      }
    }

    if (shouldAttachSecurityHeaders && isUnsafeMethod(method)) {
      const idempotencyKey = options?.idempotencyKey ?? generateIdempotencyKey();
      headers.set("Idempotency-Key", idempotencyKey);
      const csrfToken = options?.antiforgeryToken ?? (await ensureAntiforgeryToken()) ?? undefined;
      if (csrfToken) headers.set("X-CSRF-TOKEN", csrfToken);
    }
  }

  async function requestApi<T>(path: string, init?: RequestInit, options?: RequestOptions): Promise<T> {
    const headers = new Headers(init?.headers ?? {});
    const method = (init?.method ?? "GET").toUpperCase();

    if (typeof init?.body === "string" && !headers.has("Content-Type")) {
      headers.set("Content-Type", "application/json; charset=utf-8");
    }

    await attachCommonHeaders(headers, method, options);

    const response = await fetch(config.resolveRequestUrl(path), {
      ...init,
      headers,
      credentials: "include"
    });

    if (response.status === 401 && !options?.disableAutoRefresh && !options?.isRetry) {
      const refreshed = await tryRefreshTokens();
      if (refreshed) {
        return requestApi<T>(path, init, { ...(options ?? {}), isRetry: true });
      }
      await config.onUnauthorized();
      throw new Error("Session expired");
    }

    if (response.status === 403) {
      const errorText = await response.text();
      const payload = tryParsePayload(errorText);
      if (payload?.code === "ANTIFORGERY_TOKEN_INVALID" && !options?.antiforgeryRetry) {
        clearAntiforgeryToken();
        return requestApi<T>(path, init, {
          ...(options ?? {}),
          antiforgeryRetry: true,
          antiforgeryToken: undefined
        });
      }
      throw new Error(payload?.message ?? errorText ?? "Forbidden");
    }

    if (!response.ok) {
      const errorText = await response.text();
      const payload = tryParsePayload(errorText);
      throw new Error(payload?.message ?? payload?.title ?? errorText ?? "Request failed");
    }

    if (response.status === 204 || response.status === 205) return {} as T;
    return await response.json() as T;
  }

  async function requestApiBlob(path: string, init?: RequestInit, options?: RequestOptions): Promise<Blob> {
    const headers = new Headers(init?.headers ?? {});
    const method = (init?.method ?? "GET").toUpperCase();

    if (typeof init?.body === "string" && !headers.has("Content-Type")) {
      headers.set("Content-Type", "application/json; charset=utf-8");
    }

    await attachCommonHeaders(headers, method, options);

    const response = await fetch(config.resolveRequestUrl(path), {
      ...init,
      headers,
      credentials: "include"
    });

    if (response.status === 401 && !options?.disableAutoRefresh && !options?.isRetry) {
      const refreshed = await tryRefreshTokens();
      if (refreshed) {
        return requestApiBlob(path, init, { ...(options ?? {}), isRetry: true });
      }
      await config.onUnauthorized();
      throw new Error("Session expired");
    }

    if (response.status === 403) {
      const errorText = await response.text();
      const payload = tryParsePayload(errorText);
      if (payload?.code === "ANTIFORGERY_TOKEN_INVALID" && !options?.antiforgeryRetry) {
        clearAntiforgeryToken();
        return requestApiBlob(path, init, {
          ...(options ?? {}),
          antiforgeryRetry: true,
          antiforgeryToken: undefined
        });
      }
      throw new Error(payload?.message ?? errorText ?? "Forbidden");
    }

    if (!response.ok) {
      const errorText = await response.text();
      const payload = tryParsePayload(errorText);
      throw new Error(payload?.message ?? payload?.title ?? errorText ?? "Request failed");
    }

    return response.blob();
  }

  function toQuery(pagedRequest: PagedRequest, extra?: Record<string, string | undefined>) {
    const query = new URLSearchParams({
      PageIndex: pagedRequest.pageIndex.toString(),
      PageSize: pagedRequest.pageSize.toString(),
      Keyword: pagedRequest.keyword ?? "",
      SortBy: pagedRequest.sortBy ?? "",
      SortDesc: pagedRequest.sortDesc ? "true" : "false"
    });

    if (extra) {
      Object.entries(extra).forEach(([key, value]) => {
        if (value) query.set(key, value);
      });
    }
    return query.toString();
  }

  async function requestPagedApi<T>(
    path: string,
    params: PagedRequest,
    extra?: Record<string, string | undefined>,
    options?: RequestOptions
  ) {
    const qs = toQuery(params, extra);
    const response = await requestApi<ApiResponse<{ items: T[]; total: number; pageIndex: number; pageSize: number }>>(
      `${path}?${qs}`,
      undefined,
      options
    );
    if (!response.data) {
      throw new Error(response.message || "Query failed");
    }
    return response.data;
  }

  async function uploadFile(path: string, file: File, options?: RequestOptions) {
    const formData = new FormData();
    formData.append("file", file);
    return requestApi<ApiResponse<{ id: number; originalName: string }>>(
      path,
      { method: "POST", body: formData },
      options
    );
  }

  async function downloadFile(path: string): Promise<void> {
    const headers: Record<string, string> = {};
    const token = getAccessToken();
    const tenantId = getTenantId();
    if (token) headers.Authorization = `Bearer ${token}`;
    if (tenantId) headers["X-Tenant-Id"] = tenantId;
    Object.assign(headers, getClientContextHeaders());

    const response = await fetch(config.resolveRequestUrl(path), {
      headers,
      credentials: "include"
    });

    if (!response.ok) {
      throw new Error("Download failed");
    }

    const blob = await response.blob();
    const disposition = response.headers.get("Content-Disposition");
    let filename = "download";
    if (disposition) {
      const match = disposition.match(/filename\*?=['"]?(?:UTF-8'')?([^;'"]+)/i);
      if (match) {
        filename = decodeURIComponent(match[1]);
      }
    }

    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = filename;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
  }

  return {
    persistTokenResult,
    requestApi,
    requestApiBlob,
    toQuery,
    requestPagedApi,
    uploadFile,
    downloadFile
  };
}
