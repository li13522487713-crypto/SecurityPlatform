import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import { requestApi } from "./api-core";
import type { RequestOptions } from "./api-core";
import type {
  DynamicFieldDefinition,
  DynamicFieldPermissionRule,
  DynamicFieldPermissionUpsertRequest,
  DynamicRelationDefinition,
  DynamicRelationUpsertRequest,
  DynamicTableCreateRequest,
  DynamicTableListItem,
  DynamicTableSummary
} from "@/types/dynamic-tables";

/** 与后端 PagedRequestValidator 中 PageSize 上限一致 */
export const DYNAMIC_TABLES_MAX_PAGE_SIZE = 200;

export interface AppScopedDynamicTableListItem {
  tableKey: string;
  displayName: string;
  status?: string;
}

export interface DeleteCheckBlocker {
  type: "form" | "page" | "approval" | "relation" | "view" | "etlJob";
  id: string;
  name: string;
  path?: string;
}

export interface DeleteCheckResult {
  canDelete: boolean;
  blockers: DeleteCheckBlocker[];
  warnings: string[];
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

export async function getAllDynamicTables(
  keyword?: string,
  options?: RequestInit & RequestOptions
): Promise<DynamicTableListItem[]> {
  const collected: DynamicTableListItem[] = [];
  let pageIndex = 1;
  while (true) {
    const response = await getDynamicTablesPaged(
      { pageIndex, pageSize: DYNAMIC_TABLES_MAX_PAGE_SIZE, keyword: keyword ?? "" },
      options
    );
    collected.push(...response.items);
    if (response.items.length < DYNAMIC_TABLES_MAX_PAGE_SIZE || collected.length >= response.total) {
      break;
    }
    pageIndex += 1;
  }
  return collected;
}

export async function getAppScopedDynamicTables(
  appId: string,
  keyword?: string
): Promise<AppScopedDynamicTableListItem[]> {
  void appId;
  const tables = await getAllDynamicTables(keyword);
  return tables.map((item) => ({
    tableKey: item.tableKey,
    displayName: item.displayName,
    status: item.status
  }));
}

export async function getDynamicTableSummary(tableKey: string): Promise<DynamicTableSummary | null> {
  const response = await requestApi<ApiResponse<DynamicTableSummary | null>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/summary`
  );
  return response.data ?? null;
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

export async function getDynamicTableFieldsBatch(
  tableKeys: string[]
): Promise<Record<string, DynamicFieldDefinition[]>> {
  const uniqueKeys = Array.from(new Set(tableKeys.map((key) => key.trim()).filter((key) => key.length > 0)));
  const result: Record<string, DynamicFieldDefinition[]> = {};
  await Promise.all(
    uniqueKeys.map(async (tableKey) => {
      result[tableKey] = await getDynamicTableFields(tableKey);
    })
  );
  return result;
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

export async function getDynamicTableRelations(tableKey: string): Promise<DynamicRelationDefinition[]> {
  const response = await requestApi<ApiResponse<DynamicRelationDefinition[]>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/relations`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function setDynamicTableRelations(tableKey: string, request: DynamicRelationUpsertRequest) {
  const response = await requestApi<ApiResponse<{ tableKey: string }>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/relations`,
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

export async function createDynamicTable(request: DynamicTableCreateRequest): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/dynamic-tables", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建失败");
  }
  return response.data;
}

export async function deleteDynamicTable(tableKey: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ tableKey: string }>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}`,
    {
      method: "DELETE"
    }
  );
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function getDynamicTableDeleteCheck(tableKey: string): Promise<DeleteCheckResult> {
  const response = await requestApi<ApiResponse<DeleteCheckResult>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/delete-check`
  );
  if (!response.data) {
    throw new Error(response.message || "删除检查失败");
  }
  return response.data;
}

export async function archiveDynamicTable(tableKey: string): Promise<void> {
  const response = await requestApi<ApiResponse<null>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/archive`,
    { method: "PATCH" }
  );
  if (!response.success) {
    throw new Error(response.message || "归档失败");
  }
}

export async function restoreDynamicTable(tableKey: string): Promise<void> {
  const response = await requestApi<ApiResponse<null>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/restore`,
    { method: "PATCH" }
  );
  if (!response.success) {
    throw new Error(response.message || "恢复失败");
  }
}
