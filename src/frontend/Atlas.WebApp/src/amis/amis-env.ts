import { message, Modal } from "ant-design-vue";
import type { ApiResponse, JsonValue } from "@/types/api";
import type { AmisEnv, AmisFetcherConfig, AmisFetcherResult } from "@/types/amis";
import { requestApi } from "@/services/api-core";
import { getActiveLocale, i18n } from "@/i18n";
import { useUserStore } from "@/stores/user";
import { getTenantId } from "@/utils/auth";
import router from "@/router";

// AMIS/三方组件会在开发环境打印已知废弃告警（第三方技术债，业务侧不可直接修复）。
// 在此仅屏蔽已确认噪音日志，避免淹没真正需要处理的错误。
(function suppressKnownThirdPartyWarnings() {
  const _origError = console.error.bind(console);
  const _origWarn = console.warn.bind(console);
  console.error = (...args: unknown[]) => {
    const first = typeof args[0] === "string" ? args[0] : "";
    if (first.includes("findDOMNode is deprecated")) return;
    _origError(...args);
  };
  console.warn = (...args: unknown[]) => {
    const first = typeof args[0] === "string" ? args[0] : "";
    if (first.includes("[ant-design-vue: Dropdown] `onVisibleChange` is deprecated")) return;
    _origWarn(...args);
  };
})()

const API_BASE = import.meta.env.VITE_API_BASE ?? "/api/v1";
const globalComposer = i18n.global as unknown as { t: (messageKey: string) => string };
const AMIS_ERROR_MESSAGE_KEY = "amis-global-error";
const AMIS_ERROR_DEDUP_WINDOW_MS = 2500;
const amisErrorShownAt = new Map<string, number>();

function normalizeUrl(url: string): string {
  if (url.startsWith("http://") || url.startsWith("https://")) {
    const parsed = new URL(url);
    return `${parsed.pathname}${parsed.search}`;
  }

  if (url.startsWith(API_BASE)) {
    const trimmed = url.slice(API_BASE.length);
    return trimmed.startsWith("/") ? trimmed : `/${trimmed}`;
  }

  return url.startsWith("/") ? url : `/${url}`;
}

function buildRequestInit(config: AmisFetcherConfig): RequestInit {
  const method = (config.method ?? "GET").toUpperCase();
  const headers = new Headers(config.headers ?? {});
  const hasBody = !["GET", "HEAD"].includes(method);
  const payload = config.data;

  if (hasBody && payload !== undefined && payload !== null) {
    if (typeof FormData !== "undefined" && payload instanceof FormData) {
      return {
        method,
        headers,
        body: payload
      };
    }

    if (typeof Blob !== "undefined" && payload instanceof Blob) {
      return {
        method,
        headers,
        body: payload
      };
    }

    if (typeof URLSearchParams !== "undefined" && payload instanceof URLSearchParams) {
      if (!headers.has("Content-Type")) {
        headers.set("Content-Type", "application/x-www-form-urlencoded;charset=UTF-8");
      }
      return {
        method,
        headers,
        body: payload.toString()
      };
    }

    if (typeof payload === "string") {
      if (!headers.has("Content-Type")) {
        headers.set("Content-Type", "application/json");
      }
      return {
        method,
        headers,
        body: payload
      };
    }

    if (!headers.has("Content-Type")) {
      headers.set("Content-Type", "application/json");
    }
    return {
      method,
      headers,
      body: JSON.stringify(payload)
    };
  }

  return {
    method,
    headers
  };
}

function normalizeAmisParams(data: JsonValue): JsonValue {
  if (!data || typeof data !== "object" || Array.isArray(data)) {
    return data;
  }
  const params = data as Record<string, JsonValue>;
  const result: Record<string, JsonValue> = {};

  for (const [key, value] of Object.entries(params)) {
    if (key === "page") {
      result["PageIndex"] = value;
    } else if (key === "perPage") {
      result["PageSize"] = value;
    } else if (key === "orderBy") {
      result["OrderBy"] = value;
    } else if (key === "orderDir") {
      result["OrderDir"] = value;
    } else {
      result[key] = value;
    }
  }
  return result;
}

function isObjectRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function coercePositiveInt(value: unknown, fallback: number): number {
  if (typeof value === "number" && Number.isFinite(value) && value > 0) {
    return Math.floor(value);
  }
  if (typeof value === "string") {
    const n = Number(value);
    if (Number.isFinite(n) && n > 0) {
      return Math.floor(n);
    }
  }
  return fallback;
}

function normalizeDynamicRecordsQueryBody(path: string, method: string, data: JsonValue): JsonValue {
  if (method !== "POST" || !/\/dynamic-tables\/[^/]+\/records\/query$/i.test(path)) {
    return data;
  }

  const payload = isObjectRecord(data) ? { ...data } : {};

  const pageIndex = coercePositiveInt(
    payload.pageIndex ?? payload.PageIndex ?? payload.page,
    1
  );
  const pageSize = coercePositiveInt(
    payload.pageSize ?? payload.PageSize ?? payload.perPage,
    20
  );

  payload.pageIndex = pageIndex;
  payload.pageSize = pageSize;
  payload.filters = Array.isArray(payload.filters)
    ? payload.filters
    : (Array.isArray(payload.Filters) ? payload.Filters : []);

  if (payload.sortBy === undefined && payload.orderBy !== undefined) {
    payload.sortBy = payload.orderBy;
  }
  if (payload.sortBy === undefined && payload.OrderBy !== undefined) {
    payload.sortBy = payload.OrderBy;
  }

  if (payload.sortDesc === undefined) {
    const dir = payload.orderDir ?? payload.OrderDir;
    if (typeof dir === "string") {
      payload.sortDesc = dir.toLowerCase() === "desc";
    }
  }

  return payload as JsonValue;
}

function appendQuery(url: string, data?: JsonValue): string {
  if (!data || typeof data !== "object" || Array.isArray(data)) {
    return url;
  }

  const params = new URLSearchParams();
  Object.entries(data).forEach(([key, value]) => {
    if (value === undefined || value === null) {
      return;
    }
    if (typeof value === "string" || typeof value === "number" || typeof value === "boolean") {
      params.set(key, String(value));
      return;
    }
    params.set(key, JSON.stringify(value));
  });

  const query = params.toString();
  if (!query) {
    return url;
  }

  return url.includes("?") ? `${url}&${query}` : `${url}?${query}`;
}

function translate(key: string): string {
  return globalComposer.t(key);
}

function normalizeErrorDedupKey(content: string): string {
  return (content || "")
    .replace(/（\s*traceId:\s*[^）]+\s*）/gi, "")
    .replace(/\(\s*traceId:\s*[^)]+\s*\)/gi, "")
    .trim();
}

function showAmisError(content: string): void {
  const finalContent = content?.trim() || translate("amis.requestFailed");
  const dedupKey = normalizeErrorDedupKey(finalContent);
  const now = Date.now();
  const lastShownAt = amisErrorShownAt.get(dedupKey) ?? 0;
  if (now - lastShownAt <= AMIS_ERROR_DEDUP_WINDOW_MS) {
    return;
  }
  amisErrorShownAt.set(dedupKey, now);
  message.open({
    key: AMIS_ERROR_MESSAGE_KEY,
    type: "error",
    content: finalContent,
    duration: 4
  });
}

function buildSafeRequestBodySnapshot(body: RequestInit["body"]): unknown {
  if (body === undefined || body === null) {
    return null;
  }
  if (typeof body === "string") {
    return body;
  }
  if (body instanceof URLSearchParams) {
    return body.toString();
  }
  if (typeof FormData !== "undefined" && body instanceof FormData) {
    return Array.from(body.entries()).map(([key, value]) => [key, typeof value === "string" ? value : `file:${value.name}`]);
  }
  if (typeof Blob !== "undefined" && body instanceof Blob) {
    return { type: body.type, size: body.size };
  }
  return "[non-string body]";
}

function logAmisRequestError(params: {
  stage: "business-fail" | "validation-fail" | "request-fail";
  path: string;
  method: string;
  status: number;
  msg: string;
  payload?: unknown;
  requestBody?: RequestInit["body"];
}) {
  console.error("[AMIS] request error", {
    stage: params.stage,
    path: params.path,
    method: params.method,
    status: params.status,
    message: params.msg,
    traceId: (params.payload as { traceId?: string } | undefined)?.traceId ?? "",
    payload: params.payload ?? null,
    requestBody: buildSafeRequestBodySnapshot(params.requestBody)
  });
}

function toCamelCaseKey(key: string): string {
  if (!key) {
    return key;
  }
  if (key.startsWith("$.") && key.length > 2) {
    key = key.slice(2);
  }
  return key.charAt(0).toLowerCase() + key.slice(1);
}

function normalizeValidationErrors(errors: Record<string, string[] | string>): Record<string, string> {
  return Object.entries(errors).reduce<Record<string, string>>((acc, [key, value]) => {
    if (!value) {
      return acc;
    }
    const items = Array.isArray(value) ? value : [value];
    const merged = items.join("; ");
    acc[key] = merged;
    const camelKey = toCamelCaseKey(key);
    if (!acc[camelKey]) {
      acc[camelKey] = merged;
    }
    return acc;
  }, {});
}

function buildValidationSummary(errors: Record<string, string>): string {
  const entries = Object.entries(errors);
  if (entries.length === 0) {
    return "";
  }
  return entries
    .filter(([key]) => !key.startsWith("$"))
    .slice(0, 6)
    .map(([field, text]) => `${field}: ${text}`)
    .join(" | ");
}

export function buildGlobalData(): Record<string, unknown> {
  try {
    const userStore = useUserStore();
    return {
      currentUser: {
        userId: userStore.profile?.id ?? "",
        userName: userStore.profile?.username ?? "",
        displayName: userStore.name,
        roles: userStore.roles,
        permissions: userStore.permissions,
        tenantId: getTenantId() ?? "",
      },
    };
  } catch {
    return { currentUser: {} };
  }
}

export function createAmisEnv(): AmisEnv {
  const fetcher = async (config: AmisFetcherConfig): Promise<AmisFetcherResult> => {
    const path = normalizeUrl(config.url);
    const method = (config.method ?? "GET").toUpperCase();
    const normalizedData = method === "GET"
      ? normalizeAmisParams(config.data ?? null)
      : normalizeDynamicRecordsQueryBody(path, method, config.data ?? null);
    const adaptedConfig = { ...config, data: normalizedData };
    const finalPath = method === "GET" ? appendQuery(path, normalizedData) : path;
    const init = buildRequestInit(adaptedConfig);

    try {
      const payload = await requestApi<ApiResponse<JsonValue>>(finalPath, init, {
        suppressErrorMessage: true
      });
      const isSuccess = payload.success !== false;
      const finalMsg = payload.message?.trim() || translate("amis.requestFailed");
      if (!isSuccess) {
        logAmisRequestError({
          stage: "business-fail",
          path: finalPath,
          method,
          status: 200,
          msg: finalMsg,
          payload,
          requestBody: init.body
        });
      }
      return {
        data: (payload.data ?? null) as JsonValue,
        ok: isSuccess,
        status: 200,
        msg: finalMsg
      };
    } catch (error) {
      const defaultError = translate("amis.requestFailed");
      const errorMessage = error instanceof Error ? error.message : defaultError;
      const errorObject = error as {
        status?: number;
        payload?: {
          title?: string;
          message?: string;
          errors?: Record<string, string[] | string>;
        } | null;
      };
      const status = errorObject.status ?? 500;
      const payload = errorObject.payload ?? null;
      const msg = payload?.message || payload?.title || errorMessage || defaultError;
      if (payload?.errors) {
        const errors = normalizeValidationErrors(payload.errors);
        const summary = buildValidationSummary(errors);
        const finalMsg = summary ? `${msg} | ${summary}` : msg;
        showAmisError(finalMsg);
        logAmisRequestError({
          stage: "validation-fail",
          path: finalPath,
          method,
          status,
          msg: finalMsg,
          payload,
          requestBody: init.body
        });
        return {
          data: { errors } as JsonValue,
          ok: false,
          status,
          msg: finalMsg
        };
      }
      const fallback: ApiResponse<JsonValue> = {
        success: false,
        code: "SERVER_ERROR",
        message: msg,
        traceId: ""
      };
      showAmisError(msg);
      logAmisRequestError({
        stage: "request-fail",
        path: finalPath,
        method,
        status,
        msg,
        payload,
        requestBody: init.body
      });
      return {
        data: (payload ?? fallback) as JsonValue,
        ok: false,
        status,
        msg
      };
    }
  };

  const notify = (type: "info" | "success" | "warning" | "error", msg: string) => {
    if (type === "error") {
      showAmisError(msg);
      return;
    }
    message.open({ type, content: msg, duration: 3 });
  };

  const alert = (msg: string) => {
    Modal.info({ title: translate("amis.notice"), content: msg });
  };

  const confirm = (msg: string) => new Promise<boolean>((resolve) => {
    Modal.confirm({
      title: translate("amis.confirmTitle"),
      content: msg,
      okText: translate("common.confirm"),
      cancelText: translate("common.cancel"),
      onOk: () => resolve(true),
      onCancel: () => resolve(false)
    });
  });

  return {
    fetcher,
    notify,
    alert,
    confirm,
    updateLocation: (location: string, replace?: boolean) => {
      console.debug('[AMIS] updateLocation:', location, replace);
      if (replace) {
        void router.replace(location);
      } else {
        void router.push(location);
      }
    },
    jumpTo: (to: string, action?: any) => {
      if (to === 'goBack') {
        router.back();
      } else {
        void router.push(to);
      }
    },
    locale: getAmisLocale(),
    data: buildGlobalData()
  };
}

export function getAmisLocale(): string {
  const appLocale = getActiveLocale();
  const map: Record<string, string> = {
    "zh-CN": "zh-CN",
    "en-US": "en-US"
  };
  return map[appLocale] ?? "zh-CN";
}
