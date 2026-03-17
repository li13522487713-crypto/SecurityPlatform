import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import type {
  TenantAppInstanceDetail,
  TenantAppInstanceListItem,
  TenantAppDataSourceBinding,
  ResourceCenterGroupItem
} from "@/types/platform-v2";
import { requestApi } from "@/services/api-core";

const TENANT_APP_INSTANCE_BASE = "/api/v2/tenant-app-instances";
const RESOURCE_CENTER_BASE = "/api/v2/resource-center";

export async function getTenantAppInstancesPaged(
  params: PagedRequest & { category?: string }
): Promise<PagedResult<TenantAppInstanceListItem>> {
  const query = new URLSearchParams({
    pageIndex: params.pageIndex.toString(),
    pageSize: params.pageSize.toString()
  });
  if (params.keyword) {
    query.set("keyword", params.keyword);
  }
  if (params.category) {
    query.set("category", params.category);
  }

  const response = await requestApi<ApiResponse<PagedResult<TenantAppInstanceListItem>>>(
    `${TENANT_APP_INSTANCE_BASE}?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询租户应用实例失败");
  }

  return response.data;
}

export async function getTenantAppInstanceDetail(id: string): Promise<TenantAppInstanceDetail> {
  const response = await requestApi<ApiResponse<TenantAppInstanceDetail>>(`${TENANT_APP_INSTANCE_BASE}/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询租户应用实例详情失败");
  }

  return response.data;
}

export async function getTenantAppDataSourceBindings(appIds?: string[]): Promise<TenantAppDataSourceBinding[]> {
  const query = new URLSearchParams();
  (appIds ?? []).forEach((appId) => {
    if (appId) {
      query.append("appIds", appId);
    }
  });
  const path = query.size > 0
    ? `${TENANT_APP_INSTANCE_BASE}/data-source-bindings?${query.toString()}`
    : `${TENANT_APP_INSTANCE_BASE}/data-source-bindings`;
  const response = await requestApi<ApiResponse<TenantAppDataSourceBinding[]>>(path);
  if (!response.data) {
    throw new Error(response.message || "查询应用数据源绑定失败");
  }

  return response.data;
}

export async function getResourceCenterGroups(): Promise<ResourceCenterGroupItem[]> {
  const response = await requestApi<ApiResponse<ResourceCenterGroupItem[]>>(`${RESOURCE_CENTER_BASE}/groups`);
  if (!response.data) {
    throw new Error(response.message || "查询资源中心分组失败");
  }

  return response.data;
}
