import type { ApiResponse } from "@atlas/shared-core";
import type { RuntimeMenuResponse } from "@/types/api";
import type { LowCodePageRuntimeSchema } from "@/types/lowcode-runtime";
import { requestApi, resolveAppHostPrefix } from "./api-core";

export async function getRuntimeMenu(appKey: string): Promise<RuntimeMenuResponse> {
  const response = await requestApi<ApiResponse<RuntimeMenuResponse>>(
    `/api/v1/runtime/apps/${encodeURIComponent(appKey)}/menu`
  );
  return response.data ?? { appKey, items: [] };
}

export function buildRuntimeRecordsUrl(pageKey: string, appKey?: string): string {
  return `${resolveAppHostPrefix(appKey)}/api/app/runtime/pages/${encodeURIComponent(pageKey)}/records`;
}

export async function getRuntimePageSchema(pageKey: string, appKey?: string): Promise<LowCodePageRuntimeSchema> {
  const prefix = resolveAppHostPrefix(appKey);
  const response = await requestApi<ApiResponse<LowCodePageRuntimeSchema>>(
    `${prefix}/api/app/runtime/pages/${encodeURIComponent(pageKey)}/schema`
  );
  if (!response.data) {
    throw new Error(response.message || "加载运行时页面失败");
  }
  return response.data;
}
