import { message, Modal } from "ant-design-vue";
import type { ApiResponse, JsonValue } from "@/types/api";
import type { AmisEnv, AmisFetcherConfig, AmisFetcherResult } from "@/types/amis";
import { requestApi } from "@/services/api-core";
import { getActiveLocale, i18n } from "@/i18n";

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
    headers.set("Content-Type", "application/json");
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

export function createAmisEnv(): AmisEnv {
  const fetcher = async (config: AmisFetcherConfig): Promise<AmisFetcherResult> => {
    const path = normalizeUrl(config.url);
    const method = (config.method ?? "GET").toUpperCase();
    const finalPath = method === "GET" ? appendQuery(path, config.data) : path;
    const init = buildRequestInit(config);

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
        const errors = Object.entries(payload.errors).reduce<Record<string, string>>((acc, [key, value]) => {
          if (!value) {
            return acc;
          }
          const items = Array.isArray(value) ? value : [value];
          acc[key] = items.join("; ");
          return acc;
        }, {});
        return {
          data: { errors } as JsonValue,
          ok: false,
          status,
          msg
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
    locale: getAmisLocale()
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
