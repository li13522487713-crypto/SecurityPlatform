import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import { requestApi } from "./api-core";
import type { RequestOptions } from "./api-core";

/** 与后端 PagedRequestValidator 中 PageSize 上限一致 */
export const DYNAMIC_TABLES_MAX_PAGE_SIZE = 200;

export interface DynamicTableListItem {
  tableKey: string;
  displayName: string;
  status?: string;
}

export interface DynamicFieldDefinition {
  name: string;
  displayName?: string;
}

export interface DynamicFieldPermissionRule {
  fieldName: string;
  roleCode: string;
  canView: boolean;
  canEdit: boolean;
}

export interface DynamicFieldPermissionUpsertRequest {
  permissions: DynamicFieldPermissionRule[];
}

export async function getDynamicTablesPaged(
  pagedRequest: PagedRequest,
  options?: RequestInit & RequestOptions
) {
  const query = new URLSearchParams({
    PageIndex: pagedRequest.pageIndex.toString(),
    PageSize: pagedRequest.pageSize.toString(),
    Keyword: pagedRequest.keyword ?? ""
  }).toString();
  const response = await requestApi<ApiResponse<PagedResult<DynamicTableListItem>>>(
    `/dynamic-tables?${query}`,
    undefined,
    options
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getDynamicTableFields(tableKey: string): Promise<DynamicFieldDefinition[]> {
  const response = await requestApi<ApiResponse<DynamicFieldDefinition[]>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/fields`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getDynamicFieldPermissions(tableKey: string): Promise<DynamicFieldPermissionRule[]> {
  const response = await requestApi<ApiResponse<DynamicFieldPermissionRule[]>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/field-permissions`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function setDynamicFieldPermissions(
  tableKey: string,
  request: DynamicFieldPermissionUpsertRequest
) {
  const response = await requestApi<ApiResponse<{ tableKey: string }>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/field-permissions`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}
