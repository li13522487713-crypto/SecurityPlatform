/**
 * Low-code runtime API for fetching runtime schema and app metadata through AppHost.
 */
import type { ApiResponse } from "@atlas/shared-core/types";
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
    `/api/v1/runtime/apps/${encodeURIComponent(appKey)}/pages/${encodeURIComponent(pageKey)}/schema${
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
    `/api/v1/lowcode-apps/by-key/${encodeURIComponent(appKey)}`
  );
  if (!response.data) {
    throw new Error(response.message || "Query failed");
  }
  return response.data;
}

export async function getAppInstanceIdByAppKey(appKey: string): Promise<string | null> {
  const normalized = appKey.trim();
  if (!normalized) {
    return null;
  }

  const detail = await getLowCodeAppByKey(normalized);
  const id = String(detail.id ?? "").trim();
  return id || null;
}
