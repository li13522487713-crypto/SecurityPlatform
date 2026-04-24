import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { downloadFile, extractResourceId, requestApi, toQuery, uploadFile } from "./api-core";
import { aiDatabaseMessage } from "./ai-database.i18n";

export interface AiDatabaseListItem {
  id: number;
  name: string;
  description?: string;
  botId?: number;
  recordCount: number;
  draftRecordCount?: number;
  onlineRecordCount?: number;
  queryMode?: AiDatabaseQueryMode;
  channelScope?: AiDatabaseChannelScope;
  createdAt: string;
  updatedAt?: string;
}

export interface AiDatabaseDetail extends AiDatabaseListItem {
  tableSchema: string;
  workspaceId?: number;
  fields?: AiDatabaseFieldItem[];
  channelConfigs?: AiDatabaseChannelConfigItem[];
}

export interface AiDatabaseRecordListItem {
  id: number;
  databaseId: number;
  dataJson: string;
  environment?: AiDatabaseRecordEnvironment;
  ownerUserId?: number;
  creatorUserId?: number;
  channelId?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface AiDatabaseFieldItem {
  id?: number;
  name: string;
  description?: string;
  type: string;
  required: boolean;
  indexed?: boolean;
  isSystemField?: boolean;
  sortOrder?: number;
}

export interface AiDatabaseChannelConfigItem {
  channelKey: string;
  displayName: string;
  allowDraft: boolean;
  allowOnline: boolean;
  publishChannelType?: string;
  credentialKind?: string;
  sortOrder?: number;
}

export interface AiDatabaseCreateRequest {
  name: string;
  description?: string;
  botId?: number;
  tableSchema?: string;
  workspaceId?: number;
  fields?: AiDatabaseFieldItem[];
  queryMode?: AiDatabaseQueryMode;
  channelScope?: AiDatabaseChannelScope;
}

export interface AiDatabaseSchemaValidateResult {
  isValid: boolean;
  errors: string[];
}

export interface AiDatabaseRecordUpsertRequest {
  dataJson: string;
  environment?: AiDatabaseRecordEnvironment;
}

export interface AiDatabaseModeUpdateRequest {
  queryMode: AiDatabaseQueryMode;
  channelScope: AiDatabaseChannelScope;
}

export interface AiDatabaseChannelConfigsUpdateRequest {
  items: AiDatabaseChannelConfigItem[];
}

/**
 * D5：导入任务来源枚举。File=CSV 上传；Inline=异步批量插入（前端直接 POST 行 JSON 数组）。
 */
export const enum AiDatabaseImportSource {
  File = 0,
  Inline = 1
}

export interface AiDatabaseImportProgress {
  taskId: number;
  databaseId: number;
  status: number;
  totalRows: number;
  succeededRows: number;
  failedRows: number;
  errorMessage?: string;
  createdAt: string;
  updatedAt?: string;
  /** D5：导入任务来源；旧任务默认 File。 */
  source?: AiDatabaseImportSource;
}

/** D5：批量同步插入请求 / 结果。 */
export interface AiDatabaseRecordBulkCreateRequest {
  /** 每项是单条记录的 dataJson（JSON 对象字符串）。 */
  rows: string[];
  environment?: AiDatabaseRecordEnvironment;
}

export interface AiDatabaseRecordBulkRowResult {
  index: number;
  success: boolean;
  id?: string;
  errorMessage?: string;
}

export interface AiDatabaseRecordBulkCreateResult {
  total: number;
  succeeded: number;
  failed: number;
  rows: AiDatabaseRecordBulkRowResult[];
}

export interface AiDatabaseBulkJobAccepted {
  taskId: number;
  rowCount: number;
}

/**
 * D2：行可见性策略。SingleUser 按 OwnerUserId 过滤；MultiUser 不过滤。
 */
export const enum AiDatabaseQueryMode {
  MultiUser = 0,
  SingleUser = 1
}

/**
 * D2：渠道隔离策略。Channel 按 ChannelId 过滤；Open 不过滤。
 */
export const enum AiDatabaseChannelScope {
  FullShared = 0,
  ChannelIsolated = 1,
  InternalShared = 2
}

export const enum AiDatabaseRecordEnvironment {
  Draft = 1,
  Online = 2
}

export async function getAiDatabasesPaged(
  request: PagedRequest,
  keyword?: string,
  workspaceId?: string
): Promise<PagedResult<AiDatabaseListItem>> {
  const response = await requestApi<ApiResponse<PagedResult<AiDatabaseListItem>>>(`/ai-databases?${toQuery(request, {
    keyword,
    workspaceId
  })}`);
  if (!response.data) {
    throw new Error(response.message || aiDatabaseMessage("getDatabasesPagedFailed"));
  }

  return response.data;
}

export async function getAiDatabaseById(id: number): Promise<AiDatabaseDetail> {
  const response = await requestApi<ApiResponse<AiDatabaseDetail>>(`/ai-databases/${id}`);
  if (!response.data) {
    throw new Error(response.message || aiDatabaseMessage("getDatabaseDetailFailed"));
  }

  return response.data;
}

export async function createAiDatabase(request: AiDatabaseCreateRequest): Promise<number> {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>("/ai-databases", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  const databaseId = extractResourceId(response.data);
  if (!response.success || !databaseId) {
    throw new Error(response.message || aiDatabaseMessage("createDatabaseFailed"));
  }

  return Number(databaseId);
}

export async function updateAiDatabase(id: number, request: AiDatabaseCreateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-databases/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || aiDatabaseMessage("updateDatabaseFailed"));
  }
}

export async function deleteAiDatabase(id: number): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-databases/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || aiDatabaseMessage("deleteDatabaseFailed"));
  }
}

export async function validateAiDatabaseSchema(schemaJson: string): Promise<AiDatabaseSchemaValidateResult> {
  const response = await requestApi<ApiResponse<AiDatabaseSchemaValidateResult>>("/ai-databases/schema-validations", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ tableSchema: schemaJson })
  });
  if (!response.data) {
    throw new Error(response.message || aiDatabaseMessage("validateSchemaFailed"));
  }

  return response.data;
}

export async function getAiDatabaseRecordsPaged(
  id: number,
  request: PagedRequest,
  environment: AiDatabaseRecordEnvironment = AiDatabaseRecordEnvironment.Draft
): Promise<PagedResult<AiDatabaseRecordListItem>> {
  const response = await requestApi<ApiResponse<PagedResult<AiDatabaseRecordListItem>>>(
    `/ai-databases/${id}/records?${toQuery(request, { environment })}`
  );
  if (!response.data) {
    throw new Error(response.message || aiDatabaseMessage("getRecordsFailed"));
  }

  return response.data;
}

export async function createAiDatabaseRecord(id: number, request: AiDatabaseRecordUpsertRequest): Promise<number> {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>(`/ai-databases/${id}/records`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  const recordId = extractResourceId(response.data);
  if (!response.success || !recordId) {
    throw new Error(response.message || aiDatabaseMessage("createRecordFailed"));
  }

  return Number(recordId);
}

export async function updateAiDatabaseRecord(
  id: number,
  recordId: number,
  request: AiDatabaseRecordUpsertRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-databases/${id}/records/${recordId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || aiDatabaseMessage("updateRecordFailed"));
  }
}

export async function deleteAiDatabaseRecord(id: number, recordId: number): Promise<void> {
  return deleteAiDatabaseRecordWithEnvironment(id, recordId, AiDatabaseRecordEnvironment.Draft);
}

export async function deleteAiDatabaseRecordWithEnvironment(
  id: number,
  recordId: number,
  environment: AiDatabaseRecordEnvironment
): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-databases/${id}/records/${recordId}?${toQuery({}, { environment })}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || aiDatabaseMessage("deleteRecordFailed"));
  }
}

/**
 * D5：同步批量插入记录。受 AiDatabaseQuota.MaxBulkInsertRows 限制（默认 1000）。
 * 返回每行的成功 / 失败明细。
 */
export async function createAiDatabaseRecordsBulk(
  id: number,
  request: AiDatabaseRecordBulkCreateRequest
): Promise<AiDatabaseRecordBulkCreateResult> {
  const response = await requestApi<ApiResponse<AiDatabaseRecordBulkCreateResult>>(
    `/ai-databases/${id}/records/bulk`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success || !response.data) {
    throw new Error(response.message || aiDatabaseMessage("bulkCreateFailed"));
  }

  return response.data;
}

/**
 * D5：异步批量插入记录。返回 taskId，进度通过 getAiDatabaseImportProgress 查询。
 */
export async function submitAiDatabaseBulkInsertJob(
  id: number,
  request: AiDatabaseRecordBulkCreateRequest
): Promise<AiDatabaseBulkJobAccepted> {
  const response = await requestApi<ApiResponse<AiDatabaseBulkJobAccepted>>(
    `/ai-databases/${id}/records/bulk-async`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success || !response.data) {
    throw new Error(response.message || aiDatabaseMessage("bulkAsyncSubmitFailed"));
  }

  return response.data;
}

export async function submitAiDatabaseImport(
  id: number,
  file: File,
  environment: AiDatabaseRecordEnvironment = AiDatabaseRecordEnvironment.Draft
): Promise<number> {
  const uploadResponse = await uploadFile("/files", file);
  const fileId = extractResourceId(uploadResponse.data);
  if (!uploadResponse.success || !fileId) {
    throw new Error(uploadResponse.message || aiDatabaseMessage("uploadImportFileFailed"));
  }

  const response = await requestApi<ApiResponse<{ taskId?: string; TaskId?: string }>>(`/ai-databases/${id}/imports`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ fileId: Number(fileId), environment })
  });
  const taskId = extractResourceId(response.data as { id?: string; Id?: string } | undefined)
    ?? extractResourceId({
      id: response.data?.taskId,
      Id: response.data?.TaskId
    });
  if (!response.success || !taskId) {
    throw new Error(response.message || aiDatabaseMessage("submitImportFailed"));
  }

  return Number(taskId);
}

export async function getAiDatabaseImportProgress(id: number): Promise<AiDatabaseImportProgress | null> {
  try {
    const response = await requestApi<ApiResponse<AiDatabaseImportProgress>>(`/ai-databases/${id}/imports/latest`);
    return response.data ?? null;
  } catch (error) {
    if (error instanceof Error && /404|Not Found|未找到|not found/i.test(error.message)) {
      return null;
    }

    throw error;
  }
}

export async function updateAiDatabaseMode(id: number, request: AiDatabaseModeUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-databases/${id}/mode`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || aiDatabaseMessage("updateDatabaseFailed"));
  }
}

export async function getAiDatabaseChannelConfigs(id: number): Promise<AiDatabaseChannelConfigItem[]> {
  const response = await requestApi<ApiResponse<AiDatabaseChannelConfigItem[]>>(`/ai-databases/${id}/channel-config`);
  if (!response.success) {
    throw new Error(response.message || aiDatabaseMessage("getDatabaseDetailFailed"));
  }

  return response.data ?? [];
}

export async function updateAiDatabaseChannelConfigs(
  id: number,
  request: AiDatabaseChannelConfigsUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-databases/${id}/channel-config`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || aiDatabaseMessage("updateDatabaseFailed"));
  }
}

export async function downloadAiDatabaseTemplate(id: number): Promise<void> {
  await downloadFile(`/ai-databases/${id}/template`);
}
