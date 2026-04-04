import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import { requestApi, toQuery } from "./api-core";

export interface TenantDto {
  id: number;
  name: string;
  code: string;
  description?: string | null;
  adminUserId?: number | null;
  isActive: boolean;
  status: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface TenantQueryRequest extends PagedRequest {
  isActive?: boolean;
}

export interface TenantCreateRequest {
  name: string;
  code: string;
  description?: string;
  adminUserId?: number;
}

export interface TenantUpdateRequest {
  id: number;
  name: string;
  code: string;
  description?: string;
  adminUserId?: number;
}

export async function getTenantsPaged(pagedRequest: TenantQueryRequest) {
  const extra: Record<string, string | undefined> = {};
  if (pagedRequest.isActive !== undefined) {
    extra.IsActive = pagedRequest.isActive ? "true" : "false";
  }
  const query = toQuery(pagedRequest, extra);
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
  const response = await requestApi<ApiResponse<number>>("/tenants", {
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
  const response = await requestApi<ApiResponse<boolean>>(`/tenants/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function toggleTenantStatus(id: number | string, isActive: boolean) {
  const response = await requestApi<ApiResponse<boolean>>(`/tenants/${id}/status`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ isActive })
  });
  if (!response.success) {
    throw new Error(response.message || "状态更新失败");
  }
}

export async function deleteTenant(id: number | string) {
  const response = await requestApi<ApiResponse<boolean>>(`/tenants/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}
