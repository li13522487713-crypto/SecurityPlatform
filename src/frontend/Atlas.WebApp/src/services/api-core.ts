/**
 * API Core Infrastructure
 *
 * Contains: requestApi, error handling, token management, query builders.
 * Domain-specific API functions are in api.ts which imports from this module.
 */
import { message } from "ant-design-vue";
import {
  clearAuthStorage,
  getAccessToken,
  getRefreshToken,
  setAccessToken,
  setRefreshToken,
  getTenantId,
  getProjectId,
  getProjectScopeEnabled,
  getAntiforgeryToken,
  setAntiforgeryToken,
  clearAntiforgeryToken
} from "@/utils/auth";
import { getClientContextHeaders } from "@/utils/clientContext";
import router from "@/router";
import type { ApiResponse, AuthTokenResult, PagedRequest } from "@/types/api";

// ─── Configuration ───────────────────────────────────────

export const API_BASE = import.meta.env.VITE_API_BASE ?? "/api/v1";

export interface RequestOptions {
  disableAutoRefresh?: boolean;
  isRetry?: boolean;
  idempotencyKey?: string;
  antiforgeryToken?: string;
  antiforgeryRetry?: boolean;
  suppressErrorMessage?: boolean;
}

// ─── Internal State ──────────────────────────────────────

let refreshPromise: Promise<boolean> | null = null;
let antiforgeryPromise: Promise<string | null> | null = null;
let missingProjectWarningAt = 0;
const inFlightWriteRequests = new Map<string, Promise<unknown>>();

const ErrorCodes = {
  AccountLocked: "ACCOUNT_LOCKED",
  PasswordExpired: "PASSWORD_EXPIRED",
  AntiforgeryTokenInvalid: "ANTIFORGERY_TOKEN_INVALID",
  IdempotencyConflict: "IDEMPOTENCY_CONFLICT",
  IdempotencyInProgress: "IDEMPOTENCY_IN_PROGRESS"
} as const;

interface ApiErrorPayload {
  success?: boolean;
  code?: string;
  message?: string;
  title?: string;
  type?: string;
  traceId?: string;
  errors?: Record<string, string[] | string>;
}

interface ApiRequestError extends Error {
  status?: number;
  payload?: ApiErrorPayload | null;
  raw?: string;
}

export interface ClientErrorReportPayload {
  message: string;
  stack?: string;
  url?: string;
  component?: string;
  level?: string;
}

// ─── Query Builder ───────────────────────────────────────

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
      if (value) {
        query.set(key, value);
      }
    });
  }

  return query.toString();
}

// ─── Request API ─────────────────────────────────────────

export async function requestApi<T>(path: string, init?: RequestInit, options?: RequestOptions): Promise<T> {
  const headers = new Headers(init?.headers ?? {});
  const token = getAccessToken();
  const tenantId = getTenantId();
  const projectScopeEnabled = getProjectScopeEnabled();
  const projectId = getProjectId();
  const method = (init?.method ?? "GET").toUpperCase();
  const shouldAttachSecurityHeaders = Boolean(token);

  // 向后兼容：如果localStorage中有token，则设置Authorization header
  // 否则依赖httpOnly cookie自动发送（更安全）
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  if (tenantId && !headers.has("X-Tenant-Id")) {
    headers.set("X-Tenant-Id", tenantId);
  }

  const clientHeaders = getClientContextHeaders();
  (Object.entries(clientHeaders) as [string, string][]).forEach(([key, value]) => {
    if (value && !headers.has(key)) {
      headers.set(key, value);
    }
  });

  if (projectScopeEnabled && projectId && !headers.has("X-Project-Id")) {
    headers.set("X-Project-Id", projectId);
  }

  if (projectScopeEnabled && !projectId && shouldRequireProjectContext(path)) {
    const now = Date.now();
    if (now - missingProjectWarningAt > 1500) {
      message.warning("请先选择项目");
      missingProjectWarningAt = now;
    }
    throw new Error("缺少项目上下文");
  }

  if (shouldAttachSecurityHeaders && isUnsafeMethod(method)) {
    const idempotencyKey = options?.idempotencyKey ?? generateIdempotencyKey();
    headers.set("Idempotency-Key", idempotencyKey);

    const antiforgeryToken = options?.antiforgeryToken ?? (await ensureAntiforgeryToken()) ?? undefined;
    if (antiforgeryToken) {
      headers.set("X-CSRF-TOKEN", antiforgeryToken);
    }

    options = { ...(options ?? {}), idempotencyKey, antiforgeryToken };
  }

  const writeRequestSignature = shouldEnableWriteRequestDeduplication(method, shouldAttachSecurityHeaders, options)
    ? buildWriteRequestSignature(path, method, init?.body, tenantId, projectId)
    : null;
  if (writeRequestSignature) {
    const inFlight = inFlightWriteRequests.get(writeRequestSignature);
    if (inFlight) {
      return (await inFlight) as T;
    }
  }

  const requestInit: RequestInit = {
    ...init,
    headers,
    credentials: "include" // 携带httpOnly cookie凭证
  };

  const runRequest = async () => {
    const response = await fetch(`${API_BASE}${path}`, requestInit);

    const shouldAttemptRefresh = !options?.disableAutoRefresh && !options?.isRetry;
    if (response.status === 401 && shouldAttemptRefresh) {
      const refreshed = await tryRefreshTokens();
      if (refreshed) {
        return requestApi<T>(path, init, { ...(options ?? {}), isRetry: true });
      }
    }

    if (response.status === 403) {
      const errorText = await response.text();
      const errorPayload = tryParseErrorPayload(errorText);
      const errorCode = errorPayload?.code ?? "";
      const errorMessage = formatErrorMessage(errorPayload, errorText || "没有权限访问");

      if (errorCode === ErrorCodes.AntiforgeryTokenInvalid && !options?.antiforgeryRetry) {
        clearAntiforgeryToken();
        return requestApi<T>(path, init, { ...(options ?? {}), antiforgeryRetry: true, antiforgeryToken: undefined });
      }

      if (shouldAttemptRefresh) {
        const refreshed = await tryRefreshTokens();
        if (refreshed) {
          return requestApi<T>(path, init, { ...(options ?? {}), isRetry: true });
        }
      }

      if (shouldForceLogout(errorCode)) {
        forceLogout(errorMessage);
        throw new Error("登录状态已失效");
      }

      if (!options?.suppressErrorMessage) {
        showError(errorMessage);
      }
      throw buildApiError(errorMessage, response.status, errorPayload, errorText);
    }

    if (!response.ok) {
      const errorText = await response.text();
      const errorPayload = tryParseErrorPayload(errorText);
      const errorMessage = formatErrorMessage(errorPayload, errorText || "网络请求失败");
      if (!options?.suppressErrorMessage) {
        showError(errorMessage);
      }
      throw buildApiError(errorMessage, response.status, errorPayload, errorText);
    }

    return (await response.json()) as T;
  };

  const requestPromise = runRequest();
  if (writeRequestSignature) {
    inFlightWriteRequests.set(writeRequestSignature, requestPromise as Promise<unknown>);
  }

  try {
    return await requestPromise;
  } finally {
    if (writeRequestSignature) {
      const current = inFlightWriteRequests.get(writeRequestSignature);
      if (current === requestPromise) {
        inFlightWriteRequests.delete(writeRequestSignature);
      }
    }
  }
}

/**
 * 下载 Blob 资源的统一入口（复用 requestApi 的认证、CSRF、刷新等机制）
 * 适用于文件导出等返回二进制数据的接口。
 */
export async function requestApiBlob(path: string, init?: RequestInit, options?: RequestOptions): Promise<Blob> {
  const headers = new Headers(init?.headers ?? {});
  const token = getAccessToken();
  const tenantId = getTenantId();
  const method = (init?.method ?? "GET").toUpperCase();
  const shouldAttachSecurityHeaders = Boolean(token);

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }
  if (tenantId && !headers.has("X-Tenant-Id")) {
    headers.set("X-Tenant-Id", tenantId);
  }

  const clientHeaders = getClientContextHeaders();
  (Object.entries(clientHeaders) as [string, string][]).forEach(([key, value]) => {
    if (value && !headers.has(key)) {
      headers.set(key, value);
    }
  });

  if (shouldAttachSecurityHeaders && isUnsafeMethod(method)) {
    const idempotencyKey = options?.idempotencyKey ?? generateIdempotencyKey();
    headers.set("Idempotency-Key", idempotencyKey);
    const antiforgeryToken = options?.antiforgeryToken ?? (await ensureAntiforgeryToken()) ?? undefined;
    if (antiforgeryToken) {
      headers.set("X-CSRF-TOKEN", antiforgeryToken);
    }
    options = { ...(options ?? {}), idempotencyKey, antiforgeryToken };
  }

  const projectId = getProjectId();
  const projectScopeEnabled = getProjectScopeEnabled();
  if (projectScopeEnabled && projectId && !headers.has("X-Project-Id")) {
    headers.set("X-Project-Id", projectId);
  }

  const writeRequestSignature = shouldEnableWriteRequestDeduplication(method, shouldAttachSecurityHeaders, options)
    ? buildWriteRequestSignature(path, method, init?.body, tenantId, projectId, "blob")
    : null;
  if (writeRequestSignature) {
    const inFlight = inFlightWriteRequests.get(writeRequestSignature);
    if (inFlight) {
      return (await inFlight) as Blob;
    }
  }

  const requestInit: RequestInit = { ...init, headers, credentials: "include" };
  const runRequest = async () => {
    const response = await fetch(`${API_BASE}${path}`, requestInit);

    if (response.status === 401) {
      const refreshed = await tryRefreshTokens();
      if (refreshed) {
        return requestApiBlob(path, init, { ...(options ?? {}), isRetry: true });
      }
    }

    if (!response.ok) {
      const errorText = await response.text();
      const errorPayload = tryParseErrorPayload(errorText);
      const errorMessage = formatErrorMessage(errorPayload, errorText || "下载失败");
      if (!options?.suppressErrorMessage) {
        showError(errorMessage);
      }
      throw buildApiError(errorMessage, response.status, errorPayload, errorText);
    }

    return response.blob();
  };

  const requestPromise = runRequest();
  if (writeRequestSignature) {
    inFlightWriteRequests.set(writeRequestSignature, requestPromise as Promise<unknown>);
  }
  try {
    return await requestPromise;
  } finally {
    if (writeRequestSignature) {
      const current = inFlightWriteRequests.get(writeRequestSignature);
      if (current === requestPromise) {
        inFlightWriteRequests.delete(writeRequestSignature);
      }
    }
  }
}

let reportingClientError = false;

export async function reportClientErrorSilently(payload: ClientErrorReportPayload): Promise<void> {
  if (reportingClientError) {
    return;
  }
  if (!payload.message) {
    return;
  }

  reportingClientError = true;
  try {
    await requestApi<ApiResponse<{ success: boolean }>>("/audit/client-errors", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    }, {
      suppressErrorMessage: true
    });
  } catch {
    // 静默失败，避免影响主流程
  } finally {
    reportingClientError = false;
  }
}

// ─── Internal Helpers ────────────────────────────────────

function isUnsafeMethod(method: string) {
  return !["GET", "HEAD", "OPTIONS", "TRACE"].includes(method);
}

function shouldEnableWriteRequestDeduplication(
  method: string,
  shouldAttachSecurityHeaders: boolean,
  options?: RequestOptions
) {
  return shouldAttachSecurityHeaders && isUnsafeMethod(method) && !options?.isRetry && !options?.antiforgeryRetry;
}

function buildWriteRequestSignature(
  path: string,
  method: string,
  body: BodyInit | null | undefined,
  tenantId: string | null,
  projectId: string | null | undefined,
  responseType: "json" | "blob" = "json"
) {
  const normalizedBody = normalizeRequestBodyForSignature(body);
  return [tenantId ?? "", projectId ?? "", method.toUpperCase(), path, normalizedBody, responseType].join("|");
}

function normalizeRequestBodyForSignature(body: BodyInit | null | undefined) {
  if (body === null || body === undefined) {
    return "";
  }

  if (typeof body === "string") {
    return body;
  }

  if (typeof URLSearchParams !== "undefined" && body instanceof URLSearchParams) {
    return body.toString();
  }

  if (typeof FormData !== "undefined" && body instanceof FormData) {
    const entries: [string, string][] = [];
    for (const [key, value] of body.entries()) {
      if (typeof value === "string") {
        entries.push([key, value]);
      } else {
        // value is File (FormDataEntryValue = string | File)
        // Use file metadata to distinguish different files; String(file) yields "[object File]" for all
        entries.push([key, `file:${value.name}:${value.size}:${value.lastModified}:${value.type}`]);
      }
    }
    return JSON.stringify(entries);
  }

  if (typeof Blob !== "undefined" && body instanceof Blob) {
    return `blob:${body.type}:${body.size}`;
  }

  if (body instanceof ArrayBuffer) {
    return `array-buffer:${body.byteLength}`;
  }

  if (ArrayBuffer.isView(body)) {
    return `typed-array:${body.byteLength}`;
  }

  return String(body);
}

function shouldRequireProjectContext(path: string): boolean {
  const exemptPrefixes = ["/apps", "/projects", "/auth", "/secure"];
  return !exemptPrefixes.some((prefix) => path.startsWith(prefix));
}

function generateIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  if (typeof crypto !== "undefined" && typeof crypto.getRandomValues === "function") {
    const bytes = new Uint8Array(16);
    crypto.getRandomValues(bytes);
    return `idem-${Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("")}`;
  }
  return `idem-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

async function ensureAntiforgeryToken(): Promise<string | null> {
  const cached = getAntiforgeryToken();
  if (cached) {
    return cached;
  }

  if (antiforgeryPromise) {
    return antiforgeryPromise;
  }

  antiforgeryPromise = (async () => {
    try {
      const response = await requestApi<ApiResponse<{ token: string }>>("/secure/antiforgery", {
        method: "GET"
      }, { disableAutoRefresh: true });
      const token = response.data?.token ?? null;
      if (token) {
        setAntiforgeryToken(token);
      }
      return token;
    } catch {
      return null;
    } finally {
      antiforgeryPromise = null;
    }
  })();

  return antiforgeryPromise;
}

async function ensureFreshTokens(): Promise<boolean> {
  if (!getRefreshToken()) {
    return false;
  }

  if (refreshPromise) {
    return refreshPromise;
  }

  refreshPromise = (async () => {
    try {
      await refreshTokenInternal();
      return true;
    } catch (error) {
      clearAuthStorage();
      throw error;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

/** Internal token refresh - used by ensureFreshTokens */
async function refreshTokenInternal(): Promise<AuthTokenResult> {
  const refreshTokenValue = getRefreshToken();
  if (!refreshTokenValue) {
    throw new Error("缺少刷新令牌");
  }

  const response = await requestApi<ApiResponse<AuthTokenResult>>("/auth/refresh", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ refreshToken: refreshTokenValue })
  }, { disableAutoRefresh: true });
  if (!response.data) {
    throw new Error(response.message || "刷新失败");
  }

  persistTokenResult(response.data);
  return response.data;
}

export function persistTokenResult(result: AuthTokenResult) {
  setAccessToken(result.accessToken);
  setRefreshToken(result.refreshToken);
}

function tryParseErrorPayload(text: string): ApiErrorPayload | null {
  if (!text) {
    return null;
  }

  try {
    const payload = JSON.parse(text) as ApiErrorPayload;
    if (payload && (payload.code || payload.message)) {
      return payload;
    }
  } catch {
    return null;
  }

  return null;
}

function buildApiError(messageText: string, status: number, payload?: ApiErrorPayload | null, raw?: string) {
  const error = new Error(messageText) as ApiRequestError;
  error.status = status;
  error.payload = payload ?? null;
  error.raw = raw;
  return error;
}

const GLOBAL_ERROR_KEY = "global-error";

function showError(content: string) {
  message.open({
    key: GLOBAL_ERROR_KEY,
    type: "error",
    content,
    duration: 4
  });
}

function formatErrorMessage(payload: ApiErrorPayload | null, fallback: string): string {
  if (payload?.code === ErrorCodes.IdempotencyInProgress) {
    return "请求正在处理中，请稍后再试";
  }
  if (payload?.code === ErrorCodes.IdempotencyConflict) {
    return "检测到重复提交但请求内容不一致，请刷新后重试";
  }

  if (!payload) {
    return fallback;
  }

  const fragments: string[] = [];
  if (payload.title) {
    fragments.push(payload.title);
  }
  if (payload.message) {
    fragments.push(payload.message);
  }
  if (payload.errors) {
    for (const [field, value] of Object.entries(payload.errors)) {
      if (!value) {
        continue;
      }
      const items = Array.isArray(value) ? value : [value];
      const prefix = field ? `${field}: ` : "";
      fragments.push(`${prefix}${items.join("，")}`);
    }
  }

  if (fragments.length === 0) {
    return fallback;
  }

  const baseMessage = fragments.join("；");
  if (payload.traceId) {
    return `${baseMessage}（traceId: ${payload.traceId}）`;
  }

  return baseMessage;
}

async function tryRefreshTokens(): Promise<boolean> {
  try {
    return await ensureFreshTokens();
  } catch {
    forceLogout("登录已过期，请重新登录");
    return false;
  }
}

function shouldForceLogout(code: string): boolean {
  return code === ErrorCodes.AccountLocked || code === ErrorCodes.PasswordExpired;
}

function forceLogout(messageText?: string) {
  clearAuthStorage();
  if (messageText) {
    message.error(messageText);
  }
  if (router.currentRoute.value.name !== "login") {
    void router.push({ name: "login" });
  }
}
