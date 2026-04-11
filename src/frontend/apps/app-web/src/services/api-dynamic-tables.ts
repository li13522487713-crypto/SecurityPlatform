import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import { createSharedDynamicTablesApi } from "@atlas/shared-biz";
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

function buildAppIdHeaders(appId?: string): Record<string, string> {
  const headers: Record<string, string> = {};
  const trimmed = appId?.trim();
  if (trimmed && /^\d+$/.test(trimmed) && trimmed !== "0") {
    headers["X-App-Id"] = trimmed;
    headers["X-App-Workspace"] = "1";
  }
  return headers;
}

// ---------------------------------------------------------------------------
// Table metadata (AppHost: /api/v1/dynamic-tables)
// ---------------------------------------------------------------------------

const sharedDynamicTablesApi = createSharedDynamicTablesApi<
  DynamicTableListItem,
  DynamicTableSummary,
  DynamicFieldDefinition,
  DynamicTableCreateRequest,
  DeleteCheckResult,
  { id: string; tableKey: string }
>({
  requestApi
});

export const getDynamicTablesPaged = (
  pagedRequest: PagedRequest,
  options?: RequestInit & RequestOptions
): Promise<PagedResult<DynamicTableListItem>> =>
  sharedDynamicTablesApi.getDynamicTablesPaged(pagedRequest, options);

export const getAllDynamicTables = (
  keyword?: string,
  options?: RequestInit & RequestOptions
): Promise<DynamicTableListItem[]> => sharedDynamicTablesApi.getAllDynamicTables(keyword, options);

export const getAppScopedDynamicTables = (
  appId: string,
  keyword?: string
): Promise<AppScopedDynamicTableListItem[]> =>
  sharedDynamicTablesApi.getAppScopedDynamicTables(appId, keyword);

export const getDynamicTableSummary = (
  tableKey: string,
  appId?: string
): Promise<DynamicTableSummary | null> =>
  sharedDynamicTablesApi.getDynamicTableSummary(tableKey, {
    headers: { ...buildAppIdHeaders(appId) }
  });

export const getDynamicTableFields = (
  tableKey: string,
  appId?: string
): Promise<DynamicFieldDefinition[]> =>
  sharedDynamicTablesApi.getDynamicTableFields(tableKey, {
    headers: { ...buildAppIdHeaders(appId) }
  });

export const createDynamicTable = (
  request: DynamicTableCreateRequest
): Promise<{ id: string; tableKey: string }> => sharedDynamicTablesApi.createDynamicTable(request);

export const deleteDynamicTable = (tableKey: string): Promise<void> =>
  sharedDynamicTablesApi.deleteDynamicTable(tableKey);

export const getDynamicTableDeleteCheck = (tableKey: string): Promise<DeleteCheckResult> =>
  sharedDynamicTablesApi.getDynamicTableDeleteCheck(tableKey);

export const archiveDynamicTable = (tableKey: string): Promise<void> =>
  sharedDynamicTablesApi.archiveDynamicTable(tableKey);

export const restoreDynamicTable = (tableKey: string): Promise<void> =>
  sharedDynamicTablesApi.restoreDynamicTable(tableKey);

export async function alterDynamicTablePreview(
  tableKey: string,
  request: DynamicTableAlterRequest,
  appId?: string
): Promise<DynamicTableAlterPreviewResponse> {
  const response = await requestApi<ApiResponse<DynamicTableAlterPreviewResponse>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/schema/alter/preview`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json", ...buildAppIdHeaders(appId) },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) throw new Error(response.message || "预览失败");
  return response.data;
}

export async function alterDynamicTable(
  tableKey: string,
  request: DynamicTableAlterRequest,
  appId?: string
): Promise<void> {
  const response = await requestApi<ApiResponse<unknown>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/schema/alter`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json", ...buildAppIdHeaders(appId) },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "变更失败");
}

// ---------------------------------------------------------------------------
// Record CRUD (AppHost: /api/v1/dynamic-tables/:tableKey/records)
// ---------------------------------------------------------------------------

interface RecordContext {
  appKey: string;
  tableKey: string;
  appId?: string;
}

function recordsBase(ctx: RecordContext): string {
  const prefix = resolveAppHostPrefix(ctx.appKey);
  return `${prefix}/api/v1/dynamic-tables/${encodeURIComponent(ctx.tableKey)}/records`;
}

export async function queryDynamicRecords(
  appKey: string,
  tableKey: string,
  request: DynamicRecordQueryRequest,
  appId?: string
): Promise<DynamicRecordListResult> {
  const ctx: RecordContext = { appKey, tableKey, appId };
  const response = await requestApi<ApiResponse<DynamicRecordListResult>>(
    `${recordsBase(ctx)}/query`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json", ...buildAppIdHeaders(appId) },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getDynamicRecord(
  appKey: string,
  tableKey: string,
  id: string,
  appId?: string
): Promise<DynamicRecordDto | null> {
  const ctx: RecordContext = { appKey, tableKey, appId };
  const response = await requestApi<ApiResponse<DynamicRecordDto | null>>(
    `${recordsBase(ctx)}/${encodeURIComponent(id)}`,
    { headers: { ...buildAppIdHeaders(appId) } }
  );
  return response.data ?? null;
}

export async function createDynamicRecord(
  appKey: string,
  tableKey: string,
  request: DynamicRecordUpsertRequest,
  appId?: string
): Promise<{ id: string }> {
  const ctx: RecordContext = { appKey, tableKey, appId };
  const response = await requestApi<ApiResponse<{ id: string }>>(
    recordsBase(ctx),
    {
      method: "POST",
      headers: { "Content-Type": "application/json", ...buildAppIdHeaders(appId) },
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
  request: DynamicRecordUpsertRequest,
  appId?: string
): Promise<void> {
  const ctx: RecordContext = { appKey, tableKey, appId };
  const response = await requestApi<ApiResponse<unknown>>(
    `${recordsBase(ctx)}/${encodeURIComponent(id)}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json", ...buildAppIdHeaders(appId) },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function deleteDynamicRecord(
  appKey: string,
  tableKey: string,
  id: string,
  appId?: string
): Promise<void> {
  const ctx: RecordContext = { appKey, tableKey, appId };
  const response = await requestApi<ApiResponse<unknown>>(
    `${recordsBase(ctx)}/${encodeURIComponent(id)}`,
    { method: "DELETE", headers: { ...buildAppIdHeaders(appId) } }
  );
  if (!response.success) throw new Error(response.message || "删除失败");
}

export async function deleteDynamicRecordsBatch(
  appKey: string,
  tableKey: string,
  ids: string[],
  appId?: string
): Promise<void> {
  const ctx: RecordContext = { appKey, tableKey, appId };
  const response = await requestApi<ApiResponse<unknown>>(
    recordsBase(ctx),
    {
      method: "DELETE",
      headers: { "Content-Type": "application/json", ...buildAppIdHeaders(appId) },
      body: JSON.stringify({ ids: ids.map(Number) })
    }
  );
  if (!response.success) throw new Error(response.message || "批量删除失败");
}
