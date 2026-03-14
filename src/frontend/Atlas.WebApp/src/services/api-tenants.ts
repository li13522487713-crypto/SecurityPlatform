import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import { requestApi, toQuery } from "@/services/api-core";

export interface TenantDto {
  id: string;
  name: string;
  code: string;
  description: string;
  adminUserId?: string;
  isActive: boolean;
  status: number;
  createdAt: string;
  updatedAt?: string;
}

export interface TenantQueryRequest extends PagedRequest {
  keyword?: string;
  isActive?: boolean;
}

export interface TenantCreateRequest {
  name: string;
  code: string;
  description?: string;
  adminUserId?: string;
}

export interface TenantUpdateRequest {
  id: string;
  name: string;
  code: string;
  description?: string;
  adminUserId?: string;
}

export async function getTenantsPaged(pagedRequest: TenantQueryRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<TenantDto>>>(`/tenants?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getTenantDetail(id: string) {
  const response = await requestApi<ApiResponse<TenantDto>>(`/tenants/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function createTenant(request: TenantCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/tenants", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建失败");
  }
  return response.data;
}

export async function updateTenant(id: string, request: TenantUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/tenants/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function toggleTenantStatus(id: string, isActive: boolean) {
  const response = await requestApi<ApiResponse<boolean>>(`/tenants/${id}/status`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ isActive })
  });
  if (!response.success) {
    throw new Error(response.message || "状态更新失败");
  }
}

export async function deleteTenant(id: string) {
  const response = await requestApi<ApiResponse<boolean>>(`/tenants/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}
