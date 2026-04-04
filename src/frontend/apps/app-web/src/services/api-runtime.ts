import type { ApiResponse } from "@atlas/shared-core";
import type { RuntimeMenuResponse } from "@/types/api";
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
