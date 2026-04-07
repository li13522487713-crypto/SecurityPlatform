/**
 * Low-code runtime API (platform gateway /api/v1) for fetching Schema by appKey + pageKey.
 * For app-host direct connections, prefer getRuntimePageSchema in api-runtime.
 */
import type { ApiResponse } from "@atlas/shared-core";
import type { LowCodeAppDetail, LowCodePageRuntimeSchema } from "@/types/lowcode-runtime";
import { requestApi } from "./api-core";

export async function getLowCodeRuntimePageSchemaByKey(
  appKey: string,
  pageKey: string,
  environmentCode?: string
): Promise<LowCodePageRuntimeSchema> {
  const query = new URLSearchParams();
  if (environmentCode) {
    query.set("environmentCode", environmentCode);
  }
  const queryText = query.toString();
  const response = await requestApi<ApiResponse<LowCodePageRuntimeSchema>>(
    `/runtime/apps/${encodeURIComponent(appKey)}/pages/${encodeURIComponent(pageKey)}/schema${
      queryText ? `?${queryText}` : ""
    }`
  );
  if (!response.data) {
    throw new Error(response.message || "Query failed");
  }
  return response.data;
}

export async function getLowCodeAppByKey(appKey: string): Promise<LowCodeAppDetail> {
  const response = await requestApi<ApiResponse<LowCodeAppDetail>>(
    `/lowcode-apps/by-key/${encodeURIComponent(appKey)}`
  );
  if (!response.data) {
    throw new Error(response.message || "Query failed");
  }
  return response.data;
}

const V2_APP_BASE = "/api/v2/tenant-app-instances";

export async function getLowCodeAppDetail(id: string): Promise<LowCodeAppDetail> {
  const response = await requestApi<ApiResponse<LowCodeAppDetail>>(`${V2_APP_BASE}/${id}`);
  if (!response.data) {
    throw new Error(response.message || "Query failed");
  }
  return response.data;
}
