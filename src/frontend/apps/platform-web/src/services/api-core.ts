import {
  getAccessToken,
  getRefreshToken,
  getTenantId,
  getProjectId,
  getProjectScopeEnabled,
  getAntiforgeryToken,
  setAntiforgeryToken,
  clearAntiforgeryToken,
  clearAuthStorage,
  setAccessToken,
  setRefreshToken,
  getClientContextHeaders
} from "@atlas/shared-core";
import type { ApiResponse, AuthTokenResult, PagedRequest } from "@atlas/shared-core";
import { router } from "@/router";

export const API_BASE = import.meta.env.VITE_API_BASE ?? "/api/v1";

function resolveRequestUrl(path: string): string {
  if (path.startsWith("http://") || path.startsWith("https://")) return path;
  if (path.startsWith("/api/")) return path;
  return `${API_BASE}${path}`;
}

export interface RequestOptions {
  disableAutoRefresh?: boolean;
  isRetry?: boolean;
  idempotencyKey?: string;
  antiforgeryToken?: string;
  antiforgeryRetry?: boolean;
  suppressErrorMessage?: boolean;
}

let refreshPromise: Promise<boolean> | null = null;
let antiforgeryPromise: Promise<string | null> | null = null;

function isUnsafeMethod(method: string) {
  return !["GET", "HEAD", "OPTIONS", "TRACE"].includes(method);
}

function generateIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return `idem-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

async function ensureAntiforgeryToken(): Promise<string | null> {
  const cached = getAntiforgeryToken();
  if (cached) return cached;
  if (antiforgeryPromise) return antiforgeryPromise;
  antiforgeryPromise = (async () => {
    try {
      const response = await requestApi<ApiResponse<{ token: string }>>("/secure/antiforgery", {
        method: "GET"
      }, { disableAutoRefresh: true });
      const token = response.data?.token ?? null;
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

export function persistTokenResult(result: AuthTokenResult) {
  setAccessToken(result.accessToken);
  setRefreshToken(result.refreshToken);
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

function forceLogout(messageText?: string) {
  clearAuthStorage();
  if (router.currentRoute.value.name !== "login") {
    void router.push({ name: "login" });
  }
}

export async function requestApi<T>(path: string, init?: RequestInit, options?: RequestOptions): Promise<T> {
  const headers = new Headers(init?.headers ?? {});
  const token = getAccessToken();
  const tenantId = getTenantId();
  const method = (init?.method ?? "GET").toUpperCase();
  const shouldAttachSecurityHeaders = Boolean(token);

  if (typeof init?.body === "string" && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json; charset=utf-8");
  }

  if (token) headers.set("Authorization", `Bearer ${token}`);
  if (tenantId && !headers.has("X-Tenant-Id")) headers.set("X-Tenant-Id", tenantId);

  const clientHeaders = getClientContextHeaders();
  (Object.entries(clientHeaders) as [string, string][]).forEach(([key, value]) => {
    if (value && !headers.has(key)) headers.set(key, value);
  });

  const projectScopeEnabled = getProjectScopeEnabled();
  const projectId = getProjectId();
  if (projectScopeEnabled && projectId && !headers.has("X-Project-Id")) {
    headers.set("X-Project-Id", projectId);
  }

  if (shouldAttachSecurityHeaders && isUnsafeMethod(method)) {
    const idempotencyKey = options?.idempotencyKey ?? generateIdempotencyKey();
    headers.set("Idempotency-Key", idempotencyKey);
    const csrfToken = options?.antiforgeryToken ?? (await ensureAntiforgeryToken()) ?? undefined;
    if (csrfToken) headers.set("X-CSRF-TOKEN", csrfToken);
  }

  const requestInit: RequestInit = { ...init, headers, credentials: "include" };

  const response = await fetch(resolveRequestUrl(path), requestInit);

  if (response.status === 401 && !options?.disableAutoRefresh && !options?.isRetry) {
    const refreshed = await tryRefreshTokens();
    if (refreshed) {
      return requestApi<T>(path, init, { ...(options ?? {}), isRetry: true });
    }
    forceLogout();
    throw new Error("Session expired");
  }

  if (response.status === 403) {
    const errorText = await response.text();
    const payload = tryParsePayload(errorText);
    if (payload?.code === "ANTIFORGERY_TOKEN_INVALID" && !options?.antiforgeryRetry) {
      clearAntiforgeryToken();
      return requestApi<T>(path, init, { ...(options ?? {}), antiforgeryRetry: true, antiforgeryToken: undefined });
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

function tryParsePayload(text: string): { code?: string; message?: string; title?: string } | null {
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

export function toQuery(pagedRequest: PagedRequest, extra?: Record<string, string | undefined>) {
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
