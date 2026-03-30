import { message, Modal } from "ant-design-vue";
import type { ApiResponse, JsonValue } from "@/types/api";
import type { AmisEnv, AmisFetcherConfig, AmisFetcherResult } from "@/types/amis";
import { requestApi } from "@/services/api-core";
import { getActiveLocale, i18n } from "@/i18n";
import { useUserStore } from "@/stores/user";
import { getAccessToken, getTenantId } from "@/utils/auth";

// AMIS 内部 React 组件使用了已废弃的 findDOMNode（第三方技术债，无法在业务层修复）。
// 在此拦截 console.error，仅屏蔽该特定告警，避免淹没开发日志中真正有意义的错误。
(function suppressAmisFindDOMNodeWarning() {
  const _origError = console.error.bind(console);
  console.error = (...args: unknown[]) => {
    const first = typeof args[0] === "string" ? args[0] : "";
    if (first.includes("findDOMNode is deprecated")) return;
    _origError(...args);
  };
})()

const API_BASE = import.meta.env.VITE_API_BASE ?? "/api/v1";
const globalComposer = i18n.global as unknown as { t: (messageKey: string) => string };

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
    const normalizedData = method === "GET" ? normalizeAmisParams(config.data ?? null) : config.data;
    const adaptedConfig = { ...config, data: normalizedData };
    const finalPath = method === "GET" ? appendQuery(path, normalizedData) : path;
    const init = buildRequestInit(adaptedConfig);

    try {
      const payload = await requestApi<ApiResponse<JsonValue>>(finalPath, init, {
        suppressErrorMessage: true
      });
      return {
        data: (payload.data ?? null) as JsonValue,
        ok: true,
        status: 200,
        msg: payload.message
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
        message.error(finalMsg);
        console.error("[AMIS] request validation failed", {
          path: finalPath,
          method,
          status,
          payload
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
      return {
        data: (payload ?? fallback) as JsonValue,
        ok: false,
        status,
        msg
      };
    }
  };

  const notify = (type: "info" | "success" | "warning" | "error", msg: string) => {
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
