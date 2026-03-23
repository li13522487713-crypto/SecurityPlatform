import { requestApi } from "@/services/api-core";
import type { ApiResponse, PagedResult } from "@/types/api";

export type SystemConfigType = "Text" | "Number" | "Boolean" | "Json" | "Secret" | "FeatureFlag";

export interface SystemConfigDto {
  id: string;
  configKey: string;
  configValue: string;
  configName: string;
  appId?: string | null;
  groupName?: string | null;
  isEncrypted: boolean;
  version: number;
  isBuiltIn: boolean;
  configType: SystemConfigType;
  targetJson?: string | null;
  remark?: string;
}

export interface SystemConfigCreateRequest {
  configKey: string;
  configValue: string;
  configName: string;
  configType?: SystemConfigType;
  targetJson?: string;
  appId?: string;
  groupName?: string;
  isEncrypted?: boolean;
  remark?: string;
}

export interface SystemConfigUpdateRequest {
  configValue: string;
  configName: string;
  targetJson?: string;
  groupName?: string;
  isEncrypted?: boolean;
  version?: number;
  remark?: string;
}

export interface SystemConfigBatchUpsertItem {
  configKey: string;
  configValue: string;
  configName: string;
  remark?: string;
  configType?: SystemConfigType;
  targetJson?: string;
  appId?: string;
  groupName?: string;
  isEncrypted?: boolean;
  version?: number;
}

export interface SystemConfigBatchUpsertRequest {
  items: SystemConfigBatchUpsertItem[];
  appId?: string;
  groupName?: string;
}

export async function getSystemConfigsPaged(params: {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
}): Promise<PagedResult<SystemConfigDto>> {
  const query = new URLSearchParams();
  query.set("PageIndex", String(params.pageIndex ?? 1));
  query.set("PageSize", String(params.pageSize ?? 20));
  if (params.keyword) query.set("Keyword", params.keyword);
  const response = await requestApi<ApiResponse<PagedResult<SystemConfigDto>>>(`/system-configs?${query}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getSystemConfigByKey(key: string, appId?: string): Promise<SystemConfigDto | null> {
  const query = new URLSearchParams();
  if (appId) query.set("appId", appId);
  const suffix = query.size > 0 ? `?${query}` : "";
  const response = await requestApi<ApiResponse<SystemConfigDto>>(`/system-configs/by-key/${encodeURIComponent(key)}${suffix}`);
  return response.data ?? null;
}

export async function querySystemConfigs(params: {
  groupName?: string;
  appId?: string;
  keys?: string[];
}): Promise<SystemConfigDto[]> {
  const query = new URLSearchParams();
  if (params.groupName) query.set("groupName", params.groupName);
  if (params.appId) query.set("appId", params.appId);
  if (params.keys && params.keys.length > 0) query.set("keys", params.keys.join(","));
  const suffix = query.size > 0 ? `?${query}` : "";
  const response = await requestApi<ApiResponse<SystemConfigDto[]>>(`/system-configs/query${suffix}`);
  return response.data ?? [];
}

export async function createSystemConfig(request: SystemConfigCreateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/system-configs", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "创建失败");
}

export async function updateSystemConfig(id: string, request: SystemConfigUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/system-configs/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function deleteSystemConfig(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/system-configs/${id}`, {
    method: "DELETE"
  });
  if (!response.success) throw new Error(response.message || "删除失败");
}

export async function batchUpsertSystemConfigs(request: SystemConfigBatchUpsertRequest): Promise<string[]> {
  const response = await requestApi<ApiResponse<{ ids: string[] }>>("/system-configs/batch-upsert", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "批量保存失败");
  }

  return response.data?.ids ?? [];
}
