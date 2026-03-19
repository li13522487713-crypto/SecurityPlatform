import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import type { ApplicationCatalogDetail, ApplicationCatalogListItem } from "@/types/platform-v2";
import { requestApi } from "@/services/api-core";

const APPLICATION_CATALOG_BASE = "/api/v2/application-catalogs";

export async function getApplicationCatalogsPaged(
  params: PagedRequest & { keyword?: string }
): Promise<PagedResult<ApplicationCatalogListItem>> {
  const query = new URLSearchParams({
    pageIndex: params.pageIndex.toString(),
    pageSize: params.pageSize.toString()
  });
  if (params.keyword) {
    query.set("keyword", params.keyword);
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
