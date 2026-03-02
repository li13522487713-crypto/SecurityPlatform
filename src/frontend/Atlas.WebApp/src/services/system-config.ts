import { requestApi } from "@/services/api-core";
import type { ApiResponse, PagedResult } from "@/types/api";

export interface SystemConfigDto {
  id: string;
  configKey: string;
  configValue: string;
  configName: string;
  isBuiltIn: boolean;
  remark?: string;
}

export interface SystemConfigCreateRequest {
  configKey: string;
  configValue: string;
  configName: string;
  remark?: string;
}

export interface SystemConfigUpdateRequest {
  configValue: string;
  configName: string;
  remark?: string;
}

export async function getSystemConfigsPaged(params: {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
}): Promise<PagedResult<SystemConfigDto>> {
  const query = new URLSearchParams();
  query.set("pageIndex", String(params.pageIndex ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  if (params.keyword) query.set("keyword", params.keyword);
  const response = await requestApi<ApiResponse<PagedResult<SystemConfigDto>>>(`/system-configs?${query}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getSystemConfigByKey(key: string): Promise<SystemConfigDto | null> {
  const response = await requestApi<ApiResponse<SystemConfigDto>>(`/system-configs/by-key/${encodeURIComponent(key)}`);
  return response.data ?? null;
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
