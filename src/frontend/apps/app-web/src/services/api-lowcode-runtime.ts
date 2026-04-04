/**
 * 低代码运行态 API（平台网关 /api/v1），用于按 appKey + pageKey 拉取 Schema 等。
 * 应用宿主内直连场景优先使用 api-runtime 中的 getRuntimePageSchema。
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
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getLowCodeAppByKey(appKey: string): Promise<LowCodeAppDetail> {
  const response = await requestApi<ApiResponse<LowCodeAppDetail>>(
    `/lowcode-apps/by-key/${encodeURIComponent(appKey)}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

const V2_APP_BASE = "/api/v2/tenant-app-instances";

export async function getLowCodeAppDetail(id: string): Promise<LowCodeAppDetail> {
  const response = await requestApi<ApiResponse<LowCodeAppDetail>>(`${V2_APP_BASE}/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}
