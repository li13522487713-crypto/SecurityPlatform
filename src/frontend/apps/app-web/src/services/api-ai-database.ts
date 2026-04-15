import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { downloadFile, extractResourceId, requestApi, toQuery, uploadFile } from "./api-core";

export interface AiDatabaseListItem {
  id: number;
  name: string;
  description?: string;
  botId?: number;
  recordCount: number;
  createdAt: string;
  updatedAt?: string;
}

export interface AiDatabaseDetail extends AiDatabaseListItem {
  tableSchema: string;
}

export interface AiDatabaseRecordListItem {
  id: number;
  databaseId: number;
  dataJson: string;
  createdAt: string;
  updatedAt?: string;
}

export interface AiDatabaseCreateRequest {
  name: string;
  description?: string;
  botId?: number;
  tableSchema: string;
  workspaceId?: number;
}

export interface AiDatabaseSchemaValidateResult {
  isValid: boolean;
  errors: string[];
}

export interface AiDatabaseRecordUpsertRequest {
  dataJson: string;
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
}

export async function getAiDatabasesPaged(request: PagedRequest, keyword?: string): Promise<PagedResult<AiDatabaseListItem>> {
  const response = await requestApi<ApiResponse<PagedResult<AiDatabaseListItem>>>(`/ai-databases?${toQuery(request, { keyword })}`);
  if (!response.data) {
    throw new Error(response.message || "查询数据库失败");
  }

  return response.data;
}

export async function getAiDatabaseById(id: number): Promise<AiDatabaseDetail> {
  const response = await requestApi<ApiResponse<AiDatabaseDetail>>(`/ai-databases/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询数据库详情失败");
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
    throw new Error(response.message || "创建数据库失败");
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
    throw new Error(response.message || "更新数据库失败");
  }
}

export async function deleteAiDatabase(id: number): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-databases/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除数据库失败");
  }
}

export async function validateAiDatabaseSchema(schemaJson: string): Promise<AiDatabaseSchemaValidateResult> {
  const response = await requestApi<ApiResponse<AiDatabaseSchemaValidateResult>>("/ai-databases/schema-validations", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ tableSchema: schemaJson })
  });
  if (!response.data) {
    throw new Error(response.message || "校验数据库 Schema 失败");
  }

  return response.data;
}

export async function getAiDatabaseRecordsPaged(
  id: number,
  request: PagedRequest
): Promise<PagedResult<AiDatabaseRecordListItem>> {
  const response = await requestApi<ApiResponse<PagedResult<AiDatabaseRecordListItem>>>(`/ai-databases/${id}/records?${toQuery(request)}`);
  if (!response.data) {
    throw new Error(response.message || "查询数据库记录失败");
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
    throw new Error(response.message || "创建数据库记录失败");
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
    throw new Error(response.message || "更新数据库记录失败");
  }
}

export async function deleteAiDatabaseRecord(id: number, recordId: number): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-databases/${id}/records/${recordId}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除数据库记录失败");
  }
}

export async function submitAiDatabaseImport(id: number, file: File): Promise<number> {
  const uploadResponse = await uploadFile("/files", file);
  const fileId = extractResourceId(uploadResponse.data);
  if (!uploadResponse.success || !fileId) {
    throw new Error(uploadResponse.message || "上传导入文件失败");
  }

  const response = await requestApi<ApiResponse<{ taskId?: string; TaskId?: string }>>(`/ai-databases/${id}/imports`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ fileId: Number(fileId) })
  });
  const taskId = extractResourceId(response.data as { id?: string; Id?: string } | undefined)
    ?? extractResourceId({
      id: response.data?.taskId,
      Id: response.data?.TaskId
    });
  if (!response.success || !taskId) {
    throw new Error(response.message || "提交数据库导入任务失败");
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

export async function downloadAiDatabaseTemplate(id: number): Promise<void> {
  await downloadFile(`/ai-databases/${id}/template`);
}
