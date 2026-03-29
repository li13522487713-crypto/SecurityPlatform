import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import type { ApplicationCatalogDetail, ApplicationCatalogListItem } from "@/types/platform-v2";
import { requestApi } from "@/services/api-core";

const APPLICATION_CATALOG_BASE = "/api/v2/application-catalogs";

export async function getApplicationCatalogsPaged(
  params: PagedRequest & { keyword?: string; status?: string; category?: string; appKey?: string }
): Promise<PagedResult<ApplicationCatalogListItem>> {
  const query = new URLSearchParams({
    pageIndex: params.pageIndex.toString(),
    pageSize: params.pageSize.toString()
  });
  if (params.keyword) {
    query.set("keyword", params.keyword);
  }
  if (params.status) {
    query.set("status", params.status);
  }
  if (params.category) {
    query.set("category", params.category);
  }
  if (params.appKey) {
    query.set("appKey", params.appKey);
  }

  const response = await requestApi<ApiResponse<PagedResult<ApplicationCatalogListItem>>>(
    `${APPLICATION_CATALOG_BASE}?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询应用目录列表失败");
  }

  return response.data;
}

export async function getApplicationCatalogDetail(id: string): Promise<ApplicationCatalogDetail> {
  const response = await requestApi<ApiResponse<ApplicationCatalogDetail>>(`${APPLICATION_CATALOG_BASE}/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询应用目录详情失败");
  }

  return response.data;
}

export async function updateApplicationCatalogDataSource(id: string, dataSourceId: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`${APPLICATION_CATALOG_BASE}/${id}/datasource`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ dataSourceId })
  });
  if (!response.success) {
    throw new Error(response.message || "更新应用目录数据源失败");
  }
}

export interface ApplicationCatalogUpdatePayload {
  name: string;
  description?: string;
  category?: string;
  icon?: string;
}

export async function updateApplicationCatalog(
  id: string,
  payload: ApplicationCatalogUpdatePayload
): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`${APPLICATION_CATALOG_BASE}/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.success) {
    throw new Error(response.message || "更新应用目录失败");
  }
}

export async function publishApplicationCatalog(id: string, releaseNote?: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`${APPLICATION_CATALOG_BASE}/${id}/publish`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ releaseNote })
  });
  if (!response.success) {
    throw new Error(response.message || "发布应用目录失败");
  }
}
