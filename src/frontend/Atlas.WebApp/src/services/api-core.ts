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
import { getCurrentAppIdFromStorage } from "@/utils/app-context";
import { getClientContextHeaders } from "@/utils/clientContext";
import router from "@/router";
import type { ApiResponse, AuthTokenResult, PagedRequest } from "@/types/api";
import { getLocale, translate } from "@/i18n";

// ─── Configuration ───────────────────────────────────────

export const API_BASE = import.meta.env.VITE_API_BASE ?? "/api/v1";

function resolveRequestUrl(path: string): string {
  if (path.startsWith("http://") || path.startsWith("https://")) {
    return path;
  }

  if (path.startsWith("/api/")) {
    return path;
  }

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

// ─── Internal State ──────────────────────────────────────

let refreshPromise: Promise<boolean> | null = null;
let antiforgeryPromise: Promise<string | null> | null = null;
let missingProjectWarningAt = 0;
const inFlightWriteRequests = new Map<string, Promise<unknown>>();
const globalErrorShownAt = new Map<string, number>();
const APP_ID_HEADER = "X-App-Id";
const APP_WORKSPACE_HEADER = "X-App-Workspace";
const APP_WORKSPACE_HEADER_VALUE = "1";

const ErrorCodes = {
  AccountLocked: "ACCOUNT_LOCKED",
  PasswordExpired: "PASSWORD_EXPIRED",
  AntiforgeryTokenInvalid: "ANTIFORGERY_TOKEN_INVALID",
  IdempotencyConflict: "IDEMPOTENCY_CONFLICT",
  IdempotencyInProgress: "IDEMPOTENCY_IN_PROGRESS",
  ProjectRequired: "PROJECT_REQUIRED",
  ProjectForbidden: "PROJECT_FORBIDDEN",
  CrossTenantForbidden: "CROSS_TENANT_FORBIDDEN"
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
  code?: string;
  isNetworkError?: boolean;
  isRetryable?: boolean;
  cause?: unknown;
}

export function isApiNetworkError(error: unknown): error is ApiRequestError {
  const requestError = error as ApiRequestError | undefined;
  return Boolean(requestError?.isNetworkError);
}

export function isApiAuthTerminalError(error: unknown): boolean {
  const requestError = error as ApiRequestError | undefined;
  if (!requestError) {
    return false;
  }
  if (requestError.status === 401) {
    return true;
  }
  const code = requestError.payload?.code ?? requestError.code ?? "";
  return code === ErrorCodes.AccountLocked || code === ErrorCodes.PasswordExpired;
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

  // 对 JSON 字符串请求体兜底补齐内容类型，避免后端返回 415 Unsupported Media Type。
  if (typeof init?.body === "string" && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json; charset=utf-8");
  }

  if (!headers.has("Accept-Language")) {
    headers.set("Accept-Language", getLocale());
  }

  // 向后兼容：如果localStorage中有token，则设置Authorization header
  // 否则依赖httpOnly cookie自动发送（更安全）
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  if (tenantId && !headers.has("X-Tenant-Id")) {
    headers.set("X-Tenant-Id", tenantId);
  }

  const appId = resolveCurrentAppId();
  if (appId && !headers.has(APP_ID_HEADER)) {
    headers.set(APP_ID_HEADER, appId);
    if (!headers.has(APP_WORKSPACE_HEADER)) {
      headers.set(APP_WORKSPACE_HEADER, APP_WORKSPACE_HEADER_VALUE);
    }
  }

  if (shouldRequireTenantContext(path) && !headers.has("X-Tenant-Id")) {
    const missingTenantMessage = shouldAttachSecurityHeaders
      ? translate("apiCore.tenantContextInvalidRelogin")
      : translate("apiCore.tenantGuidRequired");
    if (!options?.suppressErrorMessage) {
      showError(missingTenantMessage, { longDedupWindow: true });
    }
    if (shouldAttachSecurityHeaders) {
      clearAuthStorage();
      if (router.currentRoute.value.name !== "login") {
        void router.push({ name: "login" });
      }
    }
    throw buildApiError(missingTenantMessage, 400, {
      code: "VALIDATION_ERROR",
      message: missingTenantMessage
    }, missingTenantMessage);
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
      message.warning(translate("apiCore.selectProjectFirst"));
      missingProjectWarningAt = now;
    }
    const missingCtx = translate("apiCore.projectContextMissing");
    const selectFirst = translate("apiCore.selectProjectFirst");
    throw buildApiError(missingCtx, 400, {
      code: ErrorCodes.ProjectRequired,
      message: selectFirst
    }, missingCtx);
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
    ? buildWriteRequestSignature(path, method, init?.body, tenantId, projectId, appId)
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
  const networkRetryLimit = shouldRetryableReadMethod(method) ? 2 : 0;

  const runRequest = async () => {
    const response = await fetchWithRetry(resolveRequestUrl(path), requestInit, path, method, networkRetryLimit);

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
      const errorMessage = formatErrorMessage(errorPayload, errorText || translate("apiCore.forbiddenDefault"));

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
        throw new Error(translate("apiCore.sessionInvalid"));
      }

      if (!options?.suppressErrorMessage) {
        showError(errorMessage);
      }
      throw buildApiError(errorMessage, response.status, errorPayload, errorText);
    }

    if (!response.ok) {
      const errorText = await response.text();
      const errorPayload = tryParseErrorPayload(errorText);
      const errorMessage = formatErrorMessage(errorPayload, errorText || translate("apiCore.networkRequestFailed"));
      if (!options?.suppressErrorMessage) {
        showError(errorMessage);
      }
      throw buildApiError(errorMessage, response.status, errorPayload, errorText);
    }

    return await parseSuccessResponse<T>(response);
  };

  const requestPromise = runRequest();
  if (writeRequestSignature) {
    inFlightWriteRequests.set(writeRequestSignature, requestPromise as Promise<unknown>);
  }

  try {
    return await requestPromise;
  } catch (error) {
    handleNetworkRequestErrorDisplay(error, options);
    throw error;
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

  if (!headers.has("Accept-Language")) {
    headers.set("Accept-Language", getLocale());
  }

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }
  if (tenantId && !headers.has("X-Tenant-Id")) {
    headers.set("X-Tenant-Id", tenantId);
  }
  const appId = resolveCurrentAppId();
  if (appId && !headers.has(APP_ID_HEADER)) {
    headers.set(APP_ID_HEADER, appId);
    if (!headers.has(APP_WORKSPACE_HEADER)) {
      headers.set(APP_WORKSPACE_HEADER, APP_WORKSPACE_HEADER_VALUE);
    }
  }
  if (shouldRequireTenantContext(path) && !headers.has("X-Tenant-Id")) {
    const missingTenantMessage = shouldAttachSecurityHeaders
      ? translate("apiCore.tenantContextInvalidRelogin")
      : translate("apiCore.tenantGuidRequired");
    if (!options?.suppressErrorMessage) {
      showError(missingTenantMessage, { longDedupWindow: true });
    }
    if (shouldAttachSecurityHeaders) {
      clearAuthStorage();
      if (router.currentRoute.value.name !== "login") {
        void router.push({ name: "login" });
      }
    }
    throw buildApiError(missingTenantMessage, 400, {
      code: "VALIDATION_ERROR",
      message: missingTenantMessage
    }, missingTenantMessage);
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
  if (projectScopeEnabled && !projectId && shouldRequireProjectContext(path)) {
    const now = Date.now();
    if (now - missingProjectWarningAt > 1500) {
      message.warning(translate("apiCore.selectProjectFirst"));
      missingProjectWarningAt = now;
    }
    const missingCtx = translate("apiCore.projectContextMissing");
    const selectFirst = translate("apiCore.selectProjectFirst");
    throw buildApiError(missingCtx, 400, {
      code: ErrorCodes.ProjectRequired,
      message: selectFirst
    }, missingCtx);
  }

  const writeRequestSignature = shouldEnableWriteRequestDeduplication(method, shouldAttachSecurityHeaders, options)
    ? buildWriteRequestSignature(path, method, init?.body, tenantId, projectId, appId, "blob")
    : null;
  if (writeRequestSignature) {
    const inFlight = inFlightWriteRequests.get(writeRequestSignature);
    if (inFlight) {
      return (await inFlight) as Blob;
    }
  }

  const requestInit: RequestInit = { ...init, headers, credentials: "include" };
  const networkRetryLimit = shouldRetryableReadMethod(method) ? 2 : 0;
  const runRequest = async () => {
    const response = await fetchWithRetry(resolveRequestUrl(path), requestInit, path, method, networkRetryLimit);

    if (response.status === 401) {
      const refreshed = await tryRefreshTokens();
      if (refreshed) {
        return requestApiBlob(path, init, { ...(options ?? {}), isRetry: true });
      }
    }

    if (!response.ok) {
      const errorText = await response.text();
      const errorPayload = tryParseErrorPayload(errorText);
      const errorMessage = formatErrorMessage(errorPayload, errorText || translate("apiCore.downloadFailed"));
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
  } catch (error) {
    handleNetworkRequestErrorDisplay(error, options);
    throw error;
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

function shouldRetryableReadMethod(method: string): boolean {
  return method === "GET" || method === "HEAD";
}

function isNetworkFailure(error: unknown): boolean {
  if (typeof DOMException !== "undefined" && error instanceof DOMException && error.name === "AbortError") {
    return false;
  }

  if (typeof navigator !== "undefined" && navigator.onLine === false) {
    return true;
  }

  if (typeof TypeError !== "undefined" && error instanceof TypeError) {
    const text = (error.message ?? "").toLowerCase();
    return text.includes("failed to fetch")
      || text.includes("network error")
      || text.includes("fetch failed")
      || text.includes("load failed")
      || text.includes("econnreset")
      || text.includes("enotfound")
      || text.includes("networkerror");
  }

  if (error && typeof error === "object") {
    const maybeError = error as { message?: string; code?: string; name?: string };
    const normalizedMessage = (maybeError.message ?? "").toLowerCase();
    const normalizedCode = (maybeError.code ?? "").toLowerCase();
    if (normalizedCode === "econnreset" || normalizedCode === "enotfound" || normalizedCode === "ecanceled") {
      return true;
    }
    if (normalizedMessage.includes("fetch failed") || normalizedMessage.includes("network")) {
      return true;
    }
  }

  return false;
}

function buildNetworkError(path: string, method: string, cause: unknown): ApiRequestError {
  const isOffline = typeof navigator !== "undefined" && navigator.onLine === false;
  const messageText = isOffline
    ? translate("apiCore.networkOffline")
    : translate("apiCore.networkRequestFailed");
  const error = new Error(messageText) as ApiRequestError;
  error.status = 0;
  error.code = "NETWORK_ERROR";
  error.payload = {
    code: "NETWORK_ERROR",
    message: messageText
  };
  error.raw = `${method.toUpperCase()} ${path}`;
  error.isNetworkError = true;
  error.isRetryable = true;
  error.cause = cause;
  return error;
}

function shouldRetryNetworkRequest(method: string, attempt: number, maxRetries: number): boolean {
  if (attempt >= maxRetries) {
    return false;
  }
  return shouldRetryableReadMethod(method.toUpperCase());
}

async function waitWithBackoff(attempt: number): Promise<void> {
  const baseDelay = 250;
  const jitter = Math.floor(Math.random() * 80);
  const delay = baseDelay * (2 ** attempt) + jitter;
  await new Promise((resolve) => setTimeout(resolve, delay));
}

async function fetchWithRetry(
  url: string,
  init: RequestInit,
  path: string,
  method: string,
  maxRetries: number
): Promise<Response> {
  let attempt = 0;
  while (true) {
    try {
      return await fetch(url, init);
    } catch (error) {
      if (!isNetworkFailure(error)) {
        throw error;
      }
      if (!shouldRetryNetworkRequest(method, attempt, maxRetries)) {
        throw buildNetworkError(path, method, error);
      }
      await waitWithBackoff(attempt);
      attempt += 1;
    }
  }
}

function handleNetworkRequestErrorDisplay(error: unknown, options?: RequestOptions) {
  if (options?.suppressErrorMessage) {
    return;
  }
  if (isApiNetworkError(error)) {
    showError(error.message, { longDedupWindow: true });
  }
}

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
  appId: string | null,
  responseType: "json" | "blob" = "json"
) {
  const normalizedBody = normalizeRequestBodyForSignature(body);
  return [tenantId ?? "", projectId ?? "", appId ?? "", method.toUpperCase(), path, normalizedBody, responseType].join("|");
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

/**
 * 将请求 path 规范为「去版本前缀、去 query」后的形态，便于与豁免表匹配。
 * 支持：`/notifications/...`、`/api/v1/users`、`/api/v2/tenant-app-instances`、`https://host/api/v1/x`。
 */
function normalizePathForProjectContext(path: string): string {
  let p = path;
  const qIndex = p.indexOf("?");
  if (qIndex >= 0) {
    p = p.slice(0, qIndex);
  }

  if (p.startsWith("http://") || p.startsWith("https://")) {
    try {
      p = new URL(p).pathname;
    } catch {
      return p.startsWith("/") ? p : `/${p}`;
    }
  }

  const versioned = p.match(/^(\/api\/v\d+)(?=\/|$)/);
  if (versioned) {
    const rest = p.slice(versioned[1].length);
    if (rest.length === 0 || rest === "/") {
      p = "/";
    } else {
      p = rest.startsWith("/") ? rest : `/${rest}`;
    }
  }

  return p.startsWith("/") ? p : `/${p}`;
}

/**
 * 项目域开启时，仅对「应用/流程/运行态等业务资源」要求 X-Project-Id；
 * 租户级管理接口（通知、数据源、用户角色等）不得因未选项目而被前端误拦。
 */
function shouldRequireProjectContext(path: string): boolean {
  const normalized = normalizePathForProjectContext(path);
  const exemptPrefixes = [
    "/admin",
    "/application-catalogs",
    "/apps",
    "/assets",
    "/audit",
    "/auth",
    "/coze-mappings",
    "/debug-layer",
    "/departments",
    "/dict-data",
    "/dict-types",
    "/license",
    "/login-logs",
    "/menus",
    "/model-configs",
    "/monitor",
    "/notifications",
    "/permissions",
    "/personal-access-tokens",
    "/platform",
    "/plugin-market",
    "/plugins",
    "/positions",
    "/projects",
    "/resource-center",
    "/release-center",
    "/runtime-contexts",
    "/runtime-executions",
    "/roles",
    "/secure",
    "/sessions",
    "/system-configs",
    "/table-views",
    "/templates",
    "/tenant-applications",
    "/tenant-app-instances",
    "/tenant-datasources",
    "/tenants",
    "/users",
    "/webhooks"
  ];
  return !exemptPrefixes.some((prefix) => normalized === prefix || normalized.startsWith(`${prefix}/`));
}

function resolveCurrentAppId(): string | null {
  if (typeof window === "undefined") {
    return null;
  }

  const match = window.location.pathname.match(/^\/apps\/([^/]+)/);
  if (!match || !match[1]) {
    return getCurrentAppIdFromStorage();
  }

  return decodeURIComponent(match[1]);
}

function shouldRequireTenantContext(path: string): boolean {
  const exemptPrefixes = ["/health", "/openapi", "/license/"];
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

  // 刷新令牌接口依赖租户上下文，缺失时直接判定为未登录态，避免触发后端租户校验报错。
  if (!getTenantId()) {
    clearAuthStorage();
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
      if (isApiAuthTerminalError(error)) {
        clearAuthStorage();
      }
      throw error;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

/**
 * 应用启动阶段静默恢复会话：
 * - 新标签页场景下 sessionStorage 为空，但 localStorage 仍有 refresh_token
 * - 先尝试刷新 access token，避免首批业务请求先出现 401 再重试
 */
export async function warmupAuthSession(): Promise<void> {
  if (getAccessToken()) {
    return;
  }

  if (!getRefreshToken() || !getTenantId()) {
    return;
  }

  try {
    await ensureFreshTokens();
  } catch {
    // 预热失败不打断页面启动，后续由路由守卫/请求链路兜底处理
  }
}

/**
 * 网络恢复后的轻量会话恢复：
 * - access_token 缺失且 refresh_token 存在时尝试刷新；
 * - 刷新失败仅在明确认证失效时触发退出。
 */
export async function recoverAuthSession(): Promise<boolean> {
  if (!getTenantId()) {
    return false;
  }

  if (getAccessToken()) {
    return true;
  }

  if (!getRefreshToken()) {
    return false;
  }

  try {
    await ensureFreshTokens();
    return true;
  } catch (error) {
    if (isApiAuthTerminalError(error)) {
      forceLogout(translate("apiCore.sessionExpiredRelogin"));
    }
    return false;
  }
}

/** Internal token refresh - used by ensureFreshTokens */
async function refreshTokenInternal(): Promise<AuthTokenResult> {
  const refreshTokenValue = getRefreshToken();
  if (!refreshTokenValue) {
    throw new Error(translate("apiCore.missingRefreshToken"));
  }

  const response = await requestApi<ApiResponse<AuthTokenResult>>("/auth/refresh", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ refreshToken: refreshTokenValue })
  }, { disableAutoRefresh: true, suppressErrorMessage: true });
  if (!response.data) {
    throw new Error(response.message || translate("apiCore.refreshFailed"));
  }

  persistTokenResult(response.data);
  return response.data;
}

export function persistTokenResult(result: AuthTokenResult) {
  setAccessToken(result.accessToken);
  setRefreshToken(result.refreshToken);
}

async function parseSuccessResponse<T>(response: Response): Promise<T> {
  // 某些接口（例如 204）或异常网关场景会返回空响应体，直接 response.json() 会抛 Unexpected end of JSON input。
  if (response.status === 204 || response.status === 205) {
    return {} as T;
  }

  const bodyText = await response.text();
  if (!bodyText || !bodyText.trim()) {
    return {} as T;
  }

  if (!isJsonContentType(response.headers.get("content-type"))) {
    throw buildApiError(translate("apiCore.responseNotJson"), response.status, null, bodyText);
  }

  try {
    return JSON.parse(bodyText) as T;
  } catch {
    throw buildApiError(translate("apiCore.responseInvalidJson"), response.status, null, bodyText);
  }
}

function isJsonContentType(contentType: string | null): boolean {
  if (!contentType) {
    return false;
  }

  const normalized = contentType.toLowerCase();
  return normalized.includes("application/json")
    || normalized.includes("+json")
    || normalized.includes("text/json");
}

function tryParseErrorPayload(text: string): ApiErrorPayload | null {
  if (!text) {
    return null;
  }

  try {
    const payload = JSON.parse(text) as ApiErrorPayload;
    if (payload && (payload.code || payload.message || payload.errors || payload.title)) {
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
  error.code = payload?.code;
  error.raw = raw;
  error.isNetworkError = false;
  error.isRetryable = false;
  return error;
}

const GLOBAL_ERROR_KEY = "global-error";
const GLOBAL_ERROR_DEDUP_WINDOW_MS = 2500;
const GLOBAL_ERROR_DEDUP_LONG_WINDOW_MS = 6000;
const GLOBAL_ERROR_CACHE_MAX = 100;

function showError(content: string, options?: { longDedupWindow?: boolean }) {
  const dedupKey = toErrorDedupKey(content);
  const now = Date.now();
  const dedupWindow = options?.longDedupWindow ? GLOBAL_ERROR_DEDUP_LONG_WINDOW_MS : GLOBAL_ERROR_DEDUP_WINDOW_MS;
  const lastShownAt = globalErrorShownAt.get(dedupKey) ?? 0;
  // 避免并发请求在短时间内重复弹出完全相同的错误。
  if (now - lastShownAt <= dedupWindow) {
    return;
  }
  if (globalErrorShownAt.size >= GLOBAL_ERROR_CACHE_MAX) {
    for (const [key, timestamp] of globalErrorShownAt.entries()) {
      if (now - timestamp > GLOBAL_ERROR_DEDUP_LONG_WINDOW_MS) {
        globalErrorShownAt.delete(key);
      }
    }
    if (globalErrorShownAt.size >= GLOBAL_ERROR_CACHE_MAX) {
      const oldestKey = globalErrorShownAt.keys().next().value as string | undefined;
      if (oldestKey) {
        globalErrorShownAt.delete(oldestKey);
      }
    }
  }
  globalErrorShownAt.set(dedupKey, now);
  message.open({
    key: GLOBAL_ERROR_KEY,
    type: "error",
    content,
    duration: 4
  });
}

function toErrorDedupKey(content: string) {
  // traceId 每次请求都不同，去重时忽略该片段，保证同类错误只提示一次。
  return content
    .replace(/（\s*traceId:\s*[^）]+\s*）/gi, "")
    .replace(/\(\s*traceId:\s*[^)]+\s*\)/gi, "")
    .trim();
}

function formatErrorMessage(payload: ApiErrorPayload | null, fallback: string): string {
  if (payload?.code === ErrorCodes.IdempotencyInProgress) {
    return translate("apiCore.idempotencyInProgress");
  }
  if (payload?.code === ErrorCodes.IdempotencyConflict) {
    return translate("apiCore.idempotencyConflict");
  }
  if (payload?.code === ErrorCodes.ProjectRequired) {
    return translate("apiCore.projectModeSelectProjectFirst");
  }
  if (payload?.code === ErrorCodes.ProjectForbidden) {
    return translate("apiCore.projectForbidden");
  }
  if (payload?.code === ErrorCodes.CrossTenantForbidden) {
    return translate("apiCore.crossTenantForbidden");
  }

  if (!payload) {
    return fallback;
  }

  const sepItems = translate("apiCore.errSepItems");
  const sepFragments = translate("apiCore.errSepFragments");

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
      const prefix = field ? translate("apiCore.errFieldPrefix", { field }) : "";
      fragments.push(`${prefix}${items.join(sepItems)}`);
    }
  }

  if (fragments.length === 0) {
    return fallback;
  }

  const baseMessage = fragments.join(sepFragments);
  if (payload.traceId) {
    return `${baseMessage}${translate("apiCore.traceIdSuffix", { traceId: payload.traceId })}`;
  }

  return baseMessage;
}

async function tryRefreshTokens(): Promise<boolean> {
  try {
    return await ensureFreshTokens();
  } catch (error) {
    if (isApiAuthTerminalError(error)) {
      forceLogout(translate("apiCore.sessionExpiredRelogin"));
    }
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
