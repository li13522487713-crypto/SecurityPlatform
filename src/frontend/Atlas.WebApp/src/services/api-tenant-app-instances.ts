import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import type {
  LowCodeAppCreateRequest,
  LowCodeAppDataSourceInfo,
  LowCodeAppEntityAliasItem,
  LowCodeAppEntityAliasesUpdateRequest,
  LowCodeAppUpdateRequest,
  LowCodeAppImportRequest,
  LowCodeAppImportResult
} from "@/types/lowcode";
import type {
  TenantAppInstanceDetail,
  TenantAppInstanceListItem,
  TenantAppDataSourceBinding,
  TenantAppFileStorageSettings,
  TenantAppFileStorageSettingsUpdateRequest,
  ResourceCenterGroupItem,
  ResourceCenterDataSourceConsumptionResponse,
  ResourceCenterRepairResult
} from "@/types/platform-v2";
import { requestApi, requestApiBlob } from "@/services/api-core";

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
  const response = await requestApi<ApiResponse<{ success: boolean; errorMessage?: string | null; latencyMs?: number | null }>>(
    `${TENANT_APP_INSTANCE_BASE}/${id}/datasource/test`,
    {
      method: "POST"
    }
  );
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

export async function getResourceCenterDataSourceConsumption(): Promise<ResourceCenterDataSourceConsumptionResponse> {
  const response = await requestApi<ApiResponse<ResourceCenterDataSourceConsumptionResponse>>(
    `${RESOURCE_CENTER_BASE}/datasource-consumption`
  );
  if (!response.data) {
    throw new Error(response.message || "查询数据源消费模型失败");
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
