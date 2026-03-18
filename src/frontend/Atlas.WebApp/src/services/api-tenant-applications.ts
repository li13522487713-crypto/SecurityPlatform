import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import type { TenantApplicationDetail, TenantApplicationListItem } from "@/types/platform-v2";
import { requestApi } from "@/services/api-core";

const TENANT_APPLICATION_BASE = "/api/v2/tenant-applications";

export async function getTenantApplicationsPaged(
  params: PagedRequest
): Promise<PagedResult<TenantApplicationListItem>> {
  const query = new URLSearchParams({
    pageIndex: params.pageIndex.toString(),
    pageSize: params.pageSize.toString()
  });
  if (params.keyword) {
    query.set("keyword", params.keyword);
  }

  const response = await requestApi<ApiResponse<PagedResult<TenantApplicationListItem>>>(
    `${TENANT_APPLICATION_BASE}?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询租户开通关系失败");
  }
  return response.data;
}

export async function getTenantApplicationDetail(id: string): Promise<TenantApplicationDetail> {
  const response = await requestApi<ApiResponse<TenantApplicationDetail>>(`${TENANT_APPLICATION_BASE}/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询租户开通关系详情失败");
  }
  return response.data;
}
