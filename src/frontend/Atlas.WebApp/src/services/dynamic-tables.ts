import type { ApiResponse, PagedRequest, PagedResult, JsonValue } from "@/types/api";
import type {
  DynamicTableListItem,
  DynamicTableDetail,
  DynamicTableCreateRequest,
  DynamicTableUpdateRequest,
  DynamicFieldDefinition,
  DynamicTableAlterRequest,
  DynamicTableAlterPreviewResponse,
  DynamicFieldPermissionRule,
  DynamicFieldPermissionUpsertRequest,
  DynamicRelationDefinition,
  DynamicRelationUpsertRequest,
  DynamicRecordUpsertRequest,
  DynamicRecordQueryRequest,
  DynamicRecordListResult
} from "@/types/dynamic-tables";
import { requestApi } from "@/services/api-core";
import type { RequestOptions } from "@/services/api-core";
import { getAppAvailableDynamicTables } from "@/services/api-app-members";

/** 与后端 PagedRequestValidator 中 PageSize 上限一致 */
export const DYNAMIC_TABLES_MAX_PAGE_SIZE = 200;

export interface AppScopedDynamicTableListItem {
  tableKey: string;
  displayName: string;
}

/**
 * 按页拉取当前应用下全部动态表（多页拼接，每页不超过 {@link DYNAMIC_TABLES_MAX_PAGE_SIZE}）。
 */
export async function getAllDynamicTables(keyword?: string, options?: RequestInit & RequestOptions): Promise<DynamicTableListItem[]> {
  const collected: DynamicTableListItem[] = [];
  let pageIndex = 1;
  while (true) {
    const res = await getDynamicTablesPaged(
      { pageIndex, pageSize: DYNAMIC_TABLES_MAX_PAGE_SIZE, keyword: keyword ?? "" },
      options
    );
    collected.push(...res.items);
    if (res.items.length < DYNAMIC_TABLES_MAX_PAGE_SIZE || collected.length >= res.total) {
      break;
    }
    pageIndex += 1;
  }
  return collected;
}

/**
 * 按应用范围查询动态表（用于数据管理工作台 / ERD 设计器）
 */
export async function getAppScopedDynamicTables(
  appId: string,
  keyword?: string
): Promise<AppScopedDynamicTableListItem[]> {
  const tables = await getAppAvailableDynamicTables(appId, keyword);
  return tables.map((item) => ({
    tableKey: item.tableKey,
    displayName: item.displayName
  }));
}

export async function getDynamicTablesPaged(pagedRequest: PagedRequest, options?: RequestInit & RequestOptions) {
  const query = new URLSearchParams({
    PageIndex: pagedRequest.pageIndex.toString(),
    PageSize: pagedRequest.pageSize.toString(),
    Keyword: pagedRequest.keyword ?? ""
  }).toString();
  const response = await requestApi<ApiResponse<PagedResult<DynamicTableListItem>>>(`/dynamic-tables?${query}`, options);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getDynamicTableDetail(tableKey: string) {
  const response = await requestApi<ApiResponse<DynamicTableDetail | null>>(`/dynamic-tables/${encodeURIComponent(tableKey)}`);
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

/**
 * 批量加载字段定义，避免组件层重复拼接 Promise.all。
 */
export async function getDynamicTableFieldsBatch(
  tableKeys: string[]
): Promise<Record<string, DynamicFieldDefinition[]>> {
  const uniqueKeys = Array.from(new Set(tableKeys.filter((key) => key.trim().length > 0)));
  const pairs = await Promise.all(
    uniqueKeys.map(async (tableKey) => [tableKey, await getDynamicTableFields(tableKey)] as const)
  );
  return Object.fromEntries(pairs);
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

export async function setDynamicFieldPermissions(tableKey: string, request: DynamicFieldPermissionUpsertRequest) {
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

export async function createDynamicTable(request: DynamicTableCreateRequest) {
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

export async function updateDynamicTable(tableKey: string, request: DynamicTableUpdateRequest) {
  const response = await requestApi<ApiResponse<{ tableKey: string }>>(`/dynamic-tables/${encodeURIComponent(tableKey)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function deleteDynamicTable(tableKey: string) {
  const response = await requestApi<ApiResponse<{ tableKey: string }>>(`/dynamic-tables/${encodeURIComponent(tableKey)}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function createDynamicRecord(tableKey: string, request: DynamicRecordUpsertRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dynamic-tables/${encodeURIComponent(tableKey)}/records`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建失败");
  }
  return response.data;
}

export async function updateDynamicRecord(
  tableKey: string,
  id: string,
  request: DynamicRecordUpsertRequest
) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dynamic-tables/${encodeURIComponent(tableKey)}/records/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function deleteDynamicRecord(tableKey: string, id: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dynamic-tables/${encodeURIComponent(tableKey)}/records/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function queryDynamicRecords(
  tableKey: string,
  request: DynamicRecordQueryRequest
): Promise<DynamicRecordListResult> {
  const response = await requestApi<ApiResponse<DynamicRecordListResult>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/records/query`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getDynamicAmisSchema(path: string): Promise<JsonValue> {
  const response = await requestApi<ApiResponse<JsonValue>>(`/amis/dynamic-tables/${path}`);
  if (!response.data) {
    throw new Error(response.message || "加载 AMIS Schema 失败");
  }
  return response.data;
}

export async function previewDynamicTableAlter(
  tableKey: string,
  request: DynamicTableAlterRequest
): Promise<DynamicTableAlterPreviewResponse> {
  const response = await requestApi<ApiResponse<DynamicTableAlterPreviewResponse>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/schema/alter/preview`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "预览变更失败");
  }
  return response.data;
}

export async function alterDynamicTableSchema(
  tableKey: string,
  request: DynamicTableAlterRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ tableKey: string }>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/schema/alter`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "应用变更失败");
  }
}

// ---- 审批流绑定 ----

export interface ApprovalBindingRequest {
  approvalFlowDefinitionId: number | null;
  approvalStatusField: string | null;
}

export interface ApprovalSubmitResponse {
  instanceId: string;
  recordId: string;
  status: string;
}

export async function bindApprovalFlow(tableKey: string, request: ApprovalBindingRequest) {
  const response = await requestApi<ApiResponse<{ tableKey: string }>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/approval-binding`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "绑定失败");
  }
}

export async function submitRecordApproval(
  tableKey: string,
  recordId: string
): Promise<ApprovalSubmitResponse> {
  const response = await requestApi<ApiResponse<ApprovalSubmitResponse>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/records/${recordId}/approval`,
    {
      method: "POST"
    }
  );
  if (!response.data) {
    throw new Error(response.message || "提交审批失败");
  }
  return response.data;
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

export async function getDynamicTableDeleteCheck(tableKey: string): Promise<DeleteCheckResult> {
  const response = await requestApi<ApiResponse<DeleteCheckResult>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/delete-check`
  );
  if (!response.data) {
    throw new Error(response.message || "删除检查失败");
  }
  return response.data;
}

export interface DynamicRecordImportRequest {
  format: "csv" | "tsv" | "excel" | "xlsx";
  content: string;
  dryRun?: boolean;
  mappings?: DynamicRecordImportFieldMapping[];
  sessionId?: string;
}

export interface DynamicRecordImportFieldMapping {
  sourceField: string;
  targetField: string;
}

export interface DynamicRecordImportRowError {
  rowIndex: number;
  field?: string | null;
  errorCode: string;
  message: string;
}

export interface DynamicRecordImportResult {
  totalRows: number;
  importedRows: number;
  skippedRows: number;
  warnings: string[];
  errors: string[];
  rowErrors?: DynamicRecordImportRowError[];
  sessionId?: string;
}

export interface DynamicRecordImportAnalyzeResult {
  sessionId: string;
  format: string;
  headers: string[];
  suggestedMappings: DynamicRecordImportFieldMapping[];
  previewRowCount: number;
  previewRows: Array<Record<string, string | null>>;
}

export async function importDynamicRecords(
  tableKey: string,
  request: DynamicRecordImportRequest
): Promise<DynamicRecordImportResult> {
  const response = await requestApi<ApiResponse<DynamicRecordImportResult>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/records/import`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "导入失败");
  }
  return response.data;
}

export async function analyzeDynamicRecordImport(
  tableKey: string,
  file: File,
  format: "csv" | "tsv" | "xlsx"
): Promise<DynamicRecordImportAnalyzeResult> {
  const formData = new FormData();
  formData.append("file", file);
  formData.append("format", format);
  const response = await requestApi<ApiResponse<DynamicRecordImportAnalyzeResult>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/records/import/analyze`,
    {
      method: "POST",
      body: formData
    }
  );
  if (!response.data) {
    throw new Error(response.message || "导入分析失败");
  }
  return response.data;
}

export async function commitDynamicRecordImport(
  tableKey: string,
  request: {
    sessionId: string;
    dryRun?: boolean;
    batchSize?: number;
    mappings?: DynamicRecordImportFieldMapping[];
  }
): Promise<DynamicRecordImportResult> {
  const response = await requestApi<ApiResponse<DynamicRecordImportResult>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/records/import/commit`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "导入提交失败");
  }
  return response.data;
}

export async function pasteExcelToDynamicRecords(
  tableKey: string,
  content: string,
  dryRun = true
): Promise<DynamicRecordImportResult> {
  const response = await requestApi<ApiResponse<DynamicRecordImportResult>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/records/excel-paste`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ format: "excel", content, dryRun })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "Excel 粘贴导入失败");
  }
  return response.data;
}
