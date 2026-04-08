import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import { requestApi, resolveAppHostPrefix } from "./api-core";
import type { RequestOptions } from "./api-core";
import type {
  AppScopedDynamicTableListItem,
  DeleteCheckResult,
  DynamicFieldDefinition,
  DynamicRecordDto,
  DynamicRecordListResult,
  DynamicRecordQueryRequest,
  DynamicRecordUpsertRequest,
  DynamicTableAlterPreviewResponse,
  DynamicTableAlterRequest,
  DynamicTableCreateRequest,
  DynamicTableListItem,
  DynamicTableSummary
} from "@/types/dynamic-tables";

const DYNAMIC_TABLES_MAX_PAGE_SIZE = 200;

// ---------------------------------------------------------------------------
// Table metadata (AppHost: /api/v1/dynamic-tables)
// ---------------------------------------------------------------------------

export async function getDynamicTablesPaged(
  pagedRequest: PagedRequest,
  options?: RequestInit & RequestOptions
): Promise<PagedResult<DynamicTableListItem>> {
  const queryParams = new URLSearchParams({
    PageIndex: pagedRequest.pageIndex.toString(),
    PageSize: pagedRequest.pageSize.toString()
  });
  const keyword = pagedRequest.keyword?.trim();
  if (keyword) queryParams.set("Keyword", keyword);
  const response = await requestApi<ApiResponse<PagedResult<DynamicTableListItem>>>(
    `/dynamic-tables?${queryParams.toString()}`,
    undefined,
    options
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getAllDynamicTables(
  keyword?: string,
  options?: RequestInit & RequestOptions
): Promise<DynamicTableListItem[]> {
  const collected: DynamicTableListItem[] = [];
  let pageIndex = 1;
  for (;;) {
    const response = await getDynamicTablesPaged(
      { pageIndex, pageSize: DYNAMIC_TABLES_MAX_PAGE_SIZE, keyword: keyword ?? "" },
      options
    );
    collected.push(...response.items);
    if (response.items.length < DYNAMIC_TABLES_MAX_PAGE_SIZE || collected.length >= response.total) break;
    pageIndex += 1;
  }
  return collected;
}

export async function getAppScopedDynamicTables(
  appId: string,
  keyword?: string
): Promise<AppScopedDynamicTableListItem[]> {
  const trimmedAppId = appId?.trim();
  if (!trimmedAppId || !/^\d+$/.test(trimmedAppId) || trimmedAppId === "0") return [];
  const queryParams = new URLSearchParams();
  const normalizedKeyword = keyword?.trim();
  if (normalizedKeyword) queryParams.set("keyword", normalizedKeyword);
  const query = queryParams.toString();
  const endpoint = `/api/v2/tenant-app-instances/${trimmedAppId}/roles/available-dynamic-tables${query ? `?${query}` : ""}`;
  const response = await requestApi<ApiResponse<Array<{ tableKey: string; displayName: string }>>>(endpoint);
  return (response.data ?? []).map((item) => ({
    tableKey: item.tableKey,
    displayName: item.displayName
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
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function createDynamicTable(request: DynamicTableCreateRequest): Promise<{ id: string; tableKey: string }> {
  const response = await requestApi<ApiResponse<{ id: string; tableKey: string }>>("/dynamic-tables", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) throw new Error(response.message || "创建失败");
  return response.data;
}

export async function deleteDynamicTable(tableKey: string): Promise<void> {
  const response = await requestApi<ApiResponse<unknown>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}`,
    { method: "DELETE" }
  );
  if (!response.success) throw new Error(response.message || "删除失败");
}

export async function getDynamicTableDeleteCheck(tableKey: string): Promise<DeleteCheckResult> {
  const response = await requestApi<ApiResponse<DeleteCheckResult>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/delete-check`
  );
  if (!response.data) throw new Error(response.message || "删除检查失败");
  return response.data;
}

export async function archiveDynamicTable(tableKey: string): Promise<void> {
  const response = await requestApi<ApiResponse<null>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/archive`,
    { method: "PATCH" }
  );
  if (!response.success) throw new Error(response.message || "归档失败");
}

export async function restoreDynamicTable(tableKey: string): Promise<void> {
  const response = await requestApi<ApiResponse<null>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/restore`,
    { method: "PATCH" }
  );
  if (!response.success) throw new Error(response.message || "恢复失败");
}

export async function alterDynamicTablePreview(
  tableKey: string,
  request: DynamicTableAlterRequest
): Promise<DynamicTableAlterPreviewResponse> {
  const response = await requestApi<ApiResponse<DynamicTableAlterPreviewResponse>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/schema/alter-preview`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) throw new Error(response.message || "预览失败");
  return response.data;
}

export async function alterDynamicTable(
  tableKey: string,
  request: DynamicTableAlterRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<unknown>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/schema/alter`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "变更失败");
}

// ---------------------------------------------------------------------------
// Record CRUD (AppHost: /api/v1/dynamic-tables/:tableKey/records)
// ---------------------------------------------------------------------------

function recordsBase(appKey: string, tableKey: string): string {
  const prefix = resolveAppHostPrefix(appKey);
  return `${prefix}/api/v1/dynamic-tables/${encodeURIComponent(tableKey)}/records`;
}

export async function queryDynamicRecords(
  appKey: string,
  tableKey: string,
  request: DynamicRecordQueryRequest
): Promise<DynamicRecordListResult> {
  const response = await requestApi<ApiResponse<DynamicRecordListResult>>(
    `${recordsBase(appKey, tableKey)}/query`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getDynamicRecord(
  appKey: string,
  tableKey: string,
  id: string
): Promise<DynamicRecordDto | null> {
  const response = await requestApi<ApiResponse<DynamicRecordDto | null>>(
    `${recordsBase(appKey, tableKey)}/${encodeURIComponent(id)}`
  );
  return response.data ?? null;
}

export async function createDynamicRecord(
  appKey: string,
  tableKey: string,
  request: DynamicRecordUpsertRequest
): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    recordsBase(appKey, tableKey),
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) throw new Error(response.message || "创建失败");
  return response.data;
}

export async function updateDynamicRecord(
  appKey: string,
  tableKey: string,
  id: string,
  request: DynamicRecordUpsertRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<unknown>>(
    `${recordsBase(appKey, tableKey)}/${encodeURIComponent(id)}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function deleteDynamicRecord(
  appKey: string,
  tableKey: string,
  id: string
): Promise<void> {
  const response = await requestApi<ApiResponse<unknown>>(
    `${recordsBase(appKey, tableKey)}/${encodeURIComponent(id)}`,
    { method: "DELETE" }
  );
  if (!response.success) throw new Error(response.message || "删除失败");
}

export async function deleteDynamicRecordsBatch(
  appKey: string,
  tableKey: string,
  ids: string[]
): Promise<void> {
  const response = await requestApi<ApiResponse<unknown>>(
    recordsBase(appKey, tableKey),
    {
      method: "DELETE",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ ids: ids.map(Number) })
    }
  );
  if (!response.success) throw new Error(response.message || "批量删除失败");
}
