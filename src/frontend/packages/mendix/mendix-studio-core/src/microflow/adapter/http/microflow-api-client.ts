/**
 * Integration/production adapter.
 * Uses backend API contracts and MicroflowApiResponse envelope.
 */
import type { MicroflowApiError, MicroflowApiResponse } from "../../contracts/api/api-envelope";
import { MicroflowApiException, mapHttpStatusToMicroflowErrorCode, normalizeMicroflowApiError } from "./microflow-api-error";

export interface MicroflowApiClientOptions {
  apiBaseUrl: string;
  appId?: string;
  workspaceId?: string;
  tenantId?: string;
  currentUser?: {
    id: string;
    name: string;
    roles?: string[];
  };
  requestHeaders?: Record<string, string> | (() => Record<string, string> | undefined);
  fetchImpl?: typeof fetch;
  onUnauthorized?: () => void;
  onForbidden?: () => void;
  onApiError?: (error: MicroflowApiError) => void;
  timeoutMsByOperation?: Partial<Record<MicroflowApiOperation, number>>;
}

export type MicroflowApiOperation =
  | "default"
  | "schema"
  | "metadata"
  | "validate"
  | "testRun"
  | "runHydration"
  | "debug";

export type MicroflowQueryValue = string | number | boolean | null | undefined | readonly (string | number | boolean)[];
export type MicroflowQuery = Record<string, MicroflowQueryValue>;

function normalizeBaseUrl(apiBaseUrl: string): string {
  return apiBaseUrl.replace(/\/+$/u, "");
}

function normalizePath(path: string): string {
  return path.startsWith("/") ? path : `/${path}`;
}

function getUrlBase(): string {
  return typeof window === "undefined" ? "http://localhost" : window.location.origin;
}

function joinBaseAndPath(apiBaseUrl: string, path: string): string {
  const normalizedPath = normalizePath(path);
  if (apiBaseUrl.endsWith("/api") && normalizedPath.startsWith("/api/")) {
    return `${apiBaseUrl}${normalizedPath.slice("/api".length)}`;
  }
  return `${apiBaseUrl}${normalizedPath}`;
}

function appendQuery(url: URL, query?: MicroflowQuery): void {
  if (!query) {
    return;
  }
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === "") {
      continue;
    }
    if (Array.isArray(value)) {
      for (const item of value) {
        url.searchParams.append(key, String(item));
      }
      continue;
    }
    url.searchParams.set(key, String(value));
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function isApiResponse<T>(value: unknown): value is MicroflowApiResponse<T> {
  return isRecord(value) && typeof value.success === "boolean";
}

function readTraceIdFromProblemDetails(payload: Record<string, unknown>): string | undefined {
  const candidates = [
    payload.traceId,
    payload.traceID,
    payload.requestId,
    payload.requestID,
    isRecord(payload.extensions) ? payload.extensions.traceId : undefined,
    isRecord(payload.extensions) ? payload.extensions.requestId : undefined,
  ];
  for (const candidate of candidates) {
    if (typeof candidate === "string" && candidate.trim()) {
      return candidate;
    }
  }
  return undefined;
}

function buildApiError(status: number, statusText: string, payload: unknown, traceId?: string): MicroflowApiError {
  if (isApiResponse<unknown>(payload) && payload.error) {
    return normalizeMicroflowApiError(payload.error, status, payload.traceId ?? traceId);
  }
  if (isRecord(payload) && isRecord(payload.error)) {
    return normalizeMicroflowApiError(payload.error, status, traceId);
  }
  if (isRecord(payload) && (typeof payload.title === "string" || typeof payload.detail === "string" || typeof payload.status === "number")) {
    return normalizeMicroflowApiError({
      code: mapHttpStatusToMicroflowErrorCode(status),
      message: typeof payload.detail === "string" ? payload.detail : typeof payload.title === "string" ? payload.title : statusText || `HTTP ${status}`,
      details: typeof payload.type === "string" ? payload.type : undefined,
      httpStatus: typeof payload.status === "number" ? payload.status : status,
      traceId: readTraceIdFromProblemDetails(payload),
      raw: payload,
    }, status, traceId);
  }
  return normalizeMicroflowApiError({
    code: mapHttpStatusToMicroflowErrorCode(status),
    message: statusText || `HTTP ${status}`,
    raw: payload,
  }, status, traceId);
}

export class MicroflowApiClient {
  private readonly baseUrl: string;
  private readonly fetchFn: typeof fetch;
  private readonly timeoutMsByOperation: Record<MicroflowApiOperation, number>;

  constructor(private readonly options: MicroflowApiClientOptions) {
    if (!options.apiBaseUrl?.trim()) {
      throw new Error("Microflow http adapter requires apiBaseUrl.");
    }
    this.baseUrl = normalizeBaseUrl(options.apiBaseUrl.trim());
    this.fetchFn = options.fetchImpl ?? globalThis.fetch.bind(globalThis);
    this.timeoutMsByOperation = {
      default: 10000,
      schema: 10000,
      metadata: 10000,
      validate: 15000,
      testRun: 20000,
      runHydration: 20000,
      debug: 20000,
      ...options.timeoutMsByOperation,
    };
  }

  get<T>(path: string, query?: MicroflowQuery, signal?: AbortSignal): Promise<T> {
    return this.request<T>("GET", path, undefined, query, signal);
  }

  post<T>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
    return this.request<T>("POST", path, body, undefined, signal);
  }

  put<T>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
    return this.request<T>("PUT", path, body, undefined, signal);
  }

  patch<T>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
    return this.request<T>("PATCH", path, body, undefined, signal);
  }

  delete<T>(path: string, signal?: AbortSignal): Promise<T> {
    return this.request<T>("DELETE", path, undefined, undefined, signal);
  }

  private async request<T>(method: string, path: string, body?: unknown, query?: MicroflowQuery, signal?: AbortSignal): Promise<T> {
    const url = new URL(joinBaseAndPath(this.baseUrl, path), getUrlBase());
    appendQuery(url, query);
    const headers = this.createHeaders(body !== undefined);
    const { signal: requestSignal, dispose } = this.createRequestAbortSignal(signal, this.resolveTimeoutMs(method, path));

    let response: Response;
    try {
      response = await this.fetchFn(url.toString(), {
        method,
        signal: requestSignal,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      });
    } catch (caught) {
      const apiError = normalizeMicroflowApiError(caught);
      this.options.onApiError?.(apiError);
      throw new MicroflowApiException(apiError.message, { apiError });
    } finally {
      dispose();
    }
    const payload = await this.readPayload(response);
    const traceId = isApiResponse<unknown>(payload) ? payload.traceId : response.headers.get("X-Trace-Id") ?? response.headers.get("traceparent") ?? undefined;

    if (!response.ok) {
      if (response.status === 401) {
        this.options.onUnauthorized?.();
      }
      if (response.status === 403) {
        this.options.onForbidden?.();
      }
      const apiError = buildApiError(response.status, response.statusText, payload, traceId);
      this.options.onApiError?.(apiError);
      throw new MicroflowApiException(apiError.message, {
        status: response.status,
        traceId,
        apiError,
      });
    }

    if (isApiResponse<T>(payload)) {
      if (!payload.success || payload.data === undefined) {
        const apiError = normalizeMicroflowApiError(payload.error ?? { code: "MICROFLOW_UNKNOWN_ERROR", message: "Microflow API response missing data." }, response.status, traceId);
        this.options.onApiError?.(apiError);
        throw new MicroflowApiException(apiError.message, { status: response.status, traceId, apiError });
      }
      return payload.data;
    }

    if (payload === undefined) {
      return undefined as T;
    }

    const apiError = normalizeMicroflowApiError({
      code: "MICROFLOW_UNKNOWN_ERROR",
      message: "Microflow API response is not a valid envelope.",
      raw: payload,
    }, response.status, traceId);
    this.options.onApiError?.(apiError);
    throw new MicroflowApiException(apiError.message, { status: response.status, traceId, apiError });
  }

  private createHeaders(hasBody: boolean): HeadersInit {
    const requestHeaders = typeof this.options.requestHeaders === "function"
      ? this.options.requestHeaders() ?? {}
      : this.options.requestHeaders ?? {};
    const headers: Record<string, string> = {
      Accept: "application/json",
      ...requestHeaders,
    };
    if (hasBody) {
      headers["Content-Type"] = "application/json";
    }
    if (this.options.workspaceId) {
      headers["X-Workspace-Id"] = this.options.workspaceId;
    }
    if (this.options.tenantId) {
      headers["X-Tenant-Id"] = this.options.tenantId;
    }
    if (this.options.currentUser?.id) {
      headers["X-User-Id"] = this.options.currentUser.id;
    }
    return headers;
  }

  private async readPayload(response: Response): Promise<unknown> {
    if (response.status === 204) {
      return undefined;
    }
    const text = await response.text();
    if (!text) {
      return undefined;
    }
    try {
      return JSON.parse(text) as unknown;
    } catch {
      return text;
    }
  }

  private resolveTimeoutMs(method: string, path: string): number {
    const normalizedPath = path.toLowerCase();
    const operation: MicroflowApiOperation = normalizedPath.includes("/debug-sessions/")
      ? "debug"
      : normalizedPath.endsWith("/validate")
        ? "validate"
        : normalizedPath.endsWith("/test-run")
          ? "testRun"
          : normalizedPath.includes("/runs/")
            ? "runHydration"
            : normalizedPath.includes("/metadata")
              ? "metadata"
              : normalizedPath.includes("/schema")
                ? "schema"
                : "default";
    const timeoutMs = this.timeoutMsByOperation[operation];
    return method === "GET" || method === "POST" || method === "PUT" || method === "PATCH" || method === "DELETE"
      ? timeoutMs
      : this.timeoutMsByOperation.default;
  }

  private createRequestAbortSignal(signal: AbortSignal | undefined, timeoutMs: number): {
    signal: AbortSignal | undefined;
    dispose: () => void;
  } {
    if (!(timeoutMs > 0) && !signal) {
      return { signal, dispose: () => undefined };
    }

    const controller = new AbortController();
    const disposers: Array<() => void> = [];

    if (signal) {
      if (signal.aborted) {
        controller.abort(signal.reason);
      } else {
        const abortListener = () => controller.abort(signal.reason);
        signal.addEventListener("abort", abortListener, { once: true });
        disposers.push(() => signal.removeEventListener("abort", abortListener));
      }
    }

    let timeoutHandle: ReturnType<typeof setTimeout> | undefined;
    if (timeoutMs > 0) {
      timeoutHandle = setTimeout(() => {
        controller.abort(new DOMException(`Microflow request timed out after ${timeoutMs}ms.`, "TimeoutError"));
      }, timeoutMs);
      disposers.push(() => {
        if (timeoutHandle !== undefined) {
          clearTimeout(timeoutHandle);
        }
      });
    }

    return {
      signal: controller.signal,
      dispose: () => {
        for (const disposer of disposers) {
          disposer();
        }
      }
    };
  }
}
