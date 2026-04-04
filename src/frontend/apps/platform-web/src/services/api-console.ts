import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import type {
  LowCodeAppCreateRequest,
  LowCodeAppDataSourceInfo,
  LowCodeAppEntityAliasItem,
  LowCodeAppEntityAliasesUpdateRequest,
  LowCodeAppUpdateRequest,
  LowCodeAppImportRequest,
  LowCodeAppImportResult,
  ApplicationCatalogDetail,
  ApplicationCatalogListItem,
  ResourceCenterDataSourceConsumptionResponse,
  ResourceCenterDataSourceConsumptionSummaryResponse,
  ResourceCenterGroupsResponse,
  ResourceCenterGroupsSummaryResponse,
  ResourceCenterRepairResult,
  TenantAppDataSourceBinding,
  TenantAppFileStorageSettings,
  TenantAppFileStorageSettingsUpdateRequest,
  TenantApplicationDetail,
  TenantApplicationListItem,
  TenantAppInstanceDetail,
  TenantAppInstanceListItem
} from "@/types/platform-console";
import { requestApi, requestApiBlob } from "@/services/api-core";

const TENANT_APP_INSTANCE_BASE = "/api/v2/tenant-app-instances";
const RESOURCE_CENTER_BASE = "/api/v2/resource-center";
const TENANT_APPLICATION_BASE = "/api/v2/tenant-applications";
const APPLICATION_CATALOG_BASE = "/api/v2/application-catalogs";

// ─── 租户应用实例 ───

export async function getTenantAppInstancesPaged(
  params: PagedRequest & { category?: string },
  init?: RequestInit
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
    `${TENANT_APP_INSTANCE_BASE}?${query.toString()}`,
    init
  );
  if (!response.data) {
    throw new Error(response.message || "查询租户应用实例失败");
  }

  return response.data;
}

export async function getTenantAppInstanceDetail(id: string, init?: RequestInit): Promise<TenantAppInstanceDetail> {
  const response = await requestApi<ApiResponse<TenantAppInstanceDetail>>(`${TENANT_APP_INSTANCE_BASE}/${id}`, init);
  if (!response.data) {
    throw new Error(response.message || "查询租户应用实例详情失败");
  }

  return response.data;
}

export async function getTenantAppInstanceEntityAliases(id: string): Promise<LowCodeAppEntityAliasItem[]> {
  const response = await requestApi<ApiResponse<LowCodeAppEntityAliasItem[]>>(
    `${TENANT_APP_INSTANCE_BASE}/${id}/entity-aliases`
  );
  if (!response.data) {
    throw new Error(response.message || "查询租户应用实例实体别名失败");
  }

  return response.data;
}

export async function updateTenantAppInstanceEntityAliases(
  id: string,
  request: LowCodeAppEntityAliasesUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `${TENANT_APP_INSTANCE_BASE}/${id}/entity-aliases`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新租户应用实例实体别名失败");
  }
}

export async function getTenantAppInstanceDataSourceInfo(id: string): Promise<LowCodeAppDataSourceInfo> {
  const response = await requestApi<ApiResponse<LowCodeAppDataSourceInfo>>(
    `${TENANT_APP_INSTANCE_BASE}/${id}/datasource`
  );
  if (!response.data) {
    throw new Error(response.message || "查询租户应用实例数据源信息失败");
  }

  return response.data;
}

export async function testTenantAppInstanceDataSource(
  id: string
): Promise<{ success: boolean; errorMessage?: string | null; latencyMs?: number | null }> {
  const response = await requestApi<
    ApiResponse<{ success: boolean; errorMessage?: string | null; latencyMs?: number | null }>
  >(`${TENANT_APP_INSTANCE_BASE}/${id}/datasource/test`, {
    method: "POST"
  });
  if (!response.data) {
    throw new Error(response.message || "测试租户应用实例数据源失败");
  }

  return response.data;
}

export async function getTenantAppInstanceFileStorageSettings(id: string): Promise<TenantAppFileStorageSettings> {
  const response = await requestApi<ApiResponse<TenantAppFileStorageSettings>>(
    `${TENANT_APP_INSTANCE_BASE}/${id}/file-storage`
  );
  if (!response.data) {
    throw new Error(response.message || "查询应用文件存储配置失败");
  }

  return response.data;
}

export async function updateTenantAppInstanceFileStorageSettings(
  id: string,
  request: TenantAppFileStorageSettingsUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `${TENANT_APP_INSTANCE_BASE}/${id}/file-storage`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新应用文件存储配置失败");
  }
}

export async function createTenantAppInstance(request: LowCodeAppCreateRequest): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>(TENANT_APP_INSTANCE_BASE, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建租户应用实例失败");
  }
  return response.data;
}

export async function updateTenantAppInstance(id: string, request: LowCodeAppUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`${TENANT_APP_INSTANCE_BASE}/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新租户应用实例失败");
  }
}

export async function publishTenantAppInstance(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`${TENANT_APP_INSTANCE_BASE}/${id}/publish`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "发布租户应用实例失败");
  }
}

export async function deleteTenantAppInstance(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`${TENANT_APP_INSTANCE_BASE}/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除租户应用实例失败");
  }
}

export async function exportTenantAppInstance(id: string): Promise<Blob> {
  return requestApiBlob(`${TENANT_APP_INSTANCE_BASE}/${id}/export`);
}

export async function importTenantAppInstance(request: LowCodeAppImportRequest): Promise<LowCodeAppImportResult> {
  const response = await requestApi<ApiResponse<LowCodeAppImportResult>>(`${TENANT_APP_INSTANCE_BASE}/import`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "导入租户应用实例失败");
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
  const path =
    query.size > 0
      ? `${TENANT_APP_INSTANCE_BASE}/data-source-bindings?${query.toString()}`
      : `${TENANT_APP_INSTANCE_BASE}/data-source-bindings`;
  const response = await requestApi<ApiResponse<TenantAppDataSourceBinding[]>>(path);
  if (!response.data) {
    throw new Error(response.message || "查询应用数据源绑定失败");
  }

  return response.data;
}

export async function getResourceCenterGroups(init?: RequestInit): Promise<ResourceCenterGroupsResponse> {
  const response = await requestApi<ApiResponse<ResourceCenterGroupsResponse>>(`${RESOURCE_CENTER_BASE}/groups`, init);
  if (!response.data) {
    throw new Error(response.message || "查询资源中心分组失败");
  }

  return response.data;
}

export async function getResourceCenterGroupsSummary(init?: RequestInit): Promise<ResourceCenterGroupsSummaryResponse> {
  const response = await requestApi<ApiResponse<ResourceCenterGroupsSummaryResponse>>(
    `${RESOURCE_CENTER_BASE}/groups/summary`,
    init
  );
  if (!response.data) {
    throw new Error(response.message || "查询资源中心分组摘要失败");
  }

  return response.data;
}

export async function getResourceCenterDataSourceConsumption(
  init?: RequestInit
): Promise<ResourceCenterDataSourceConsumptionResponse> {
  const response = await requestApi<ApiResponse<ResourceCenterDataSourceConsumptionResponse>>(
    `${RESOURCE_CENTER_BASE}/datasource-consumption`,
    init
  );
  if (!response.data) {
    throw new Error(response.message || "查询数据源消费模型失败");
  }

  return response.data;
}

export async function getResourceCenterDataSourceConsumptionSummary(
  init?: RequestInit
): Promise<ResourceCenterDataSourceConsumptionSummaryResponse> {
  const response = await requestApi<ApiResponse<ResourceCenterDataSourceConsumptionSummaryResponse>>(
    `${RESOURCE_CENTER_BASE}/datasource-consumption/summary`,
    init
  );
  if (!response.data) {
    throw new Error(response.message || "查询数据源消费摘要失败");
  }

  return response.data;
}

export async function disableInvalidBinding(bindingId: string): Promise<ResourceCenterRepairResult> {
  const response = await requestApi<ApiResponse<ResourceCenterRepairResult>>(
    `${RESOURCE_CENTER_BASE}/datasource-consumption/repair/disable-invalid-binding`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ bindingId })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "禁用无效绑定失败");
  }

  return response.data;
}

export async function switchPrimaryBinding(
  tenantAppInstanceId: string,
  targetDataSourceId: string,
  note?: string
): Promise<ResourceCenterRepairResult> {
  const response = await requestApi<ApiResponse<ResourceCenterRepairResult>>(
    `${RESOURCE_CENTER_BASE}/datasource-consumption/repair/switch-primary-binding`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        tenantAppInstanceId,
        targetDataSourceId,
        note
      })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "切换主绑定失败");
  }

  return response.data;
}

export async function unbindOrphanBinding(bindingId: string): Promise<ResourceCenterRepairResult> {
  const response = await requestApi<ApiResponse<ResourceCenterRepairResult>>(
    `${RESOURCE_CENTER_BASE}/datasource-consumption/repair/unbind-orphan-binding`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ bindingId })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "解绑孤儿绑定失败");
  }

  return response.data;
}

// ─── 租户开通关系 ───

export async function getTenantApplicationsPaged(
  params: PagedRequest & { status?: string }
): Promise<PagedResult<TenantApplicationListItem>> {
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

// ─── 应用目录 ───

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

export async function updateApplicationCatalog(id: string, payload: ApplicationCatalogUpdatePayload): Promise<void> {
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
