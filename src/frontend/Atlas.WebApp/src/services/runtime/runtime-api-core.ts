import { getLocale } from "@/i18n";
import type { ApiResponse } from "@/types/api";
import type { LowCodePageRuntimeSchema } from "@/types/lowcode";
import { getAccessToken, getTenantId } from "@/utils/auth";

export interface RuntimeMenuItem {
  pageKey: string;
  title: string;
  routePath: string;
  icon?: string | null;
  sortOrder: number;
}

export interface RuntimeMenuResponse {
  appKey: string;
  items: RuntimeMenuItem[];
}

function resolveAppHostPrefix(appKey?: string): string {
  const normalizedAppKey = appKey?.trim();
  if (normalizedAppKey) {
    return `/app-host/${encodeURIComponent(normalizedAppKey)}`;
  }

  if (typeof window === "undefined") {
    return "";
  }

  const match = window.location.pathname.match(/^\/app-host\/([^/]+)/);
  return match ? `/app-host/${match[1]}` : "";
}

async function requestRuntimeApi<T>(url: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers ?? {});
  const token = getAccessToken();
  const tenantId = getTenantId();

  if (!headers.has("Accept-Language")) {
    headers.set("Accept-Language", getLocale());
  }

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  if (tenantId && !headers.has("X-Tenant-Id")) {
    headers.set("X-Tenant-Id", tenantId);
  }

  if (typeof init?.body === "string" && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json; charset=utf-8");
  }

  const response = await fetch(url, {
    ...init,
    headers,
    credentials: "include"
  });

  const contentType = response.headers.get("content-type") ?? "";
  const isJson = contentType.includes("application/json") || contentType.includes("+json");
  const payload = isJson ? await response.json() as T : await response.text() as T;
  if (!response.ok) {
    const errorMessage = typeof payload === "object" && payload && "message" in (payload as Record<string, unknown>)
      ? String((payload as Record<string, unknown>).message ?? "请求失败")
      : "请求失败";
    throw new Error(errorMessage);
  }

  return payload;
}

export function buildRuntimeRecordsUrl(pageKey: string, appKey?: string): string {
  return `${resolveAppHostPrefix(appKey)}/api/app/runtime/pages/${encodeURIComponent(pageKey)}/records`;
}

export async function getRuntimePageSchema(pageKey: string, appKey?: string): Promise<LowCodePageRuntimeSchema> {
  const response = await requestRuntimeApi<ApiResponse<LowCodePageRuntimeSchema>>(
    `${resolveAppHostPrefix(appKey)}/api/app/runtime/pages/${encodeURIComponent(pageKey)}/schema`
  );
  if (!response.data) {
    throw new Error(response.message || "加载运行时页面失败");
  }

  return response.data;
}

export async function getRuntimeMenu(appKey: string): Promise<RuntimeMenuResponse> {
  const response = await requestRuntimeApi<ApiResponse<RuntimeMenuResponse>>(
    `/api/v1/runtime/apps/${encodeURIComponent(appKey)}/menu`
  );
  return response.data ?? { appKey, items: [] };
}
