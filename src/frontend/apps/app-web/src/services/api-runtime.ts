import type { ApiResponse } from "@atlas/shared-core";
import type { RuntimeMenuResponse } from "@/types/api";
import type { LowCodePageRuntimeSchema } from "@/types/lowcode-runtime";
import { isDirectRuntimeMode, requestApi, resolveAppHostPrefix } from "./api-core";

function resolveAppKeyFromPath(): string | null {
  if (typeof window === "undefined") {
    return null;
  }

  const appRouteMatch = window.location.pathname.match(/^\/apps\/([^/]+)/);
  return appRouteMatch?.[1] ? decodeURIComponent(appRouteMatch[1]) : null;
}

function resolveRuntimeAppKey(inputAppKey?: string): string | null {
  const normalized = inputAppKey?.trim();
  return normalized && normalized.length > 0 ? normalized : resolveAppKeyFromPath();
}

export async function getRuntimeMenu(appKey: string): Promise<RuntimeMenuResponse> {
  const requestPath = `${resolveAppHostPrefix(appKey)}/api/v1/runtime/apps/${encodeURIComponent(appKey)}/menu`;
  const response = await requestApi<ApiResponse<RuntimeMenuResponse>>(
    requestPath
  );
  return response.data ?? { appKey, items: [] };
}

export function buildRuntimeRecordsUrl(pageKey: string, appKey?: string): string {
  const encodedPageKey = encodeURIComponent(pageKey);
  const runtimeAppKey = resolveRuntimeAppKey(appKey);
  if (isDirectRuntimeMode() || !runtimeAppKey) {
    return `/api/app/runtime/pages/${encodedPageKey}/records`;
  }

  return `${resolveAppHostPrefix(runtimeAppKey)}/api/app/runtime/pages/${encodedPageKey}/records`;
}

export async function getRuntimePageSchema(pageKey: string, appKey?: string): Promise<LowCodePageRuntimeSchema> {
  const encodedPageKey = encodeURIComponent(pageKey);
  const runtimeAppKey = resolveRuntimeAppKey(appKey);
  const requestPath = isDirectRuntimeMode() || !runtimeAppKey
    ? `/api/app/runtime/pages/${encodedPageKey}/schema`
    : `${resolveAppHostPrefix(runtimeAppKey)}/api/app/runtime/pages/${encodedPageKey}/schema`;

  const response = await requestApi<ApiResponse<LowCodePageRuntimeSchema>>(
    requestPath
  );
  if (!response.data) {
    throw new Error(response.message || "Failed to load runtime page");
  }
  return response.data;
}
