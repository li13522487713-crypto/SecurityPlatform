import { requestApi, requestApiBlob, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";

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

export interface AiDatabaseCreateRequest {
  name: string;
  description?: string;
  botId?: number;
  tableSchema: string;
}

export interface AiDatabaseUpdateRequest extends AiDatabaseCreateRequest {}

export interface AiDatabaseRecordListItem {
  id: number;
  databaseId: number;
  dataJson: string;
  createdAt: string;
  updatedAt?: string;
}

export interface AiDatabaseRecordCreateRequest {
  dataJson: string;
}

export interface AiDatabaseRecordUpdateRequest extends AiDatabaseRecordCreateRequest {}

export interface AiDatabaseSchemaValidateResult {
  isValid: boolean;
  errors: string[];
}

export interface AiDatabaseImportRequest {
  fileId: number;
}

export interface AiDatabaseImportProgress {
  taskId: number;
  databaseId: number;
  status: 0 | 1 | 2 | 3;
  totalRows: number;
  succeededRows: number;
  failedRows: number;
  errorMessage?: string;
  createdAt: string;
  updatedAt?: string;
}

export async function getAiDatabasesPaged(request: PagedRequest, keyword?: string) {
  const query = toQuery(request, { keyword });
  const response = await requestApi<ApiResponse<PagedResult<AiDatabaseListItem>>>(`/ai-databases?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询数据库列表失败");
  }

  return response.data;
}

export async function getAiDatabaseById(id: number) {
  const response = await requestApi<ApiResponse<AiDatabaseDetail>>(`/ai-databases/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询数据库详情失败");
  }

  return response.data;
}

export async function createAiDatabase(request: AiDatabaseCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/ai-databases", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "创建数据库失败");
  }

  return Number(response.data.id);
}

export async function updateAiDatabase(id: number, request: AiDatabaseUpdateRequest) {
  const response = await requestApi<ApiResponse<object>>(`/ai-databases/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新数据库失败");
  }
}

export async function deleteAiDatabase(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-databases/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除数据库失败");
  }
}

export async function bindAiDatabaseBot(id: number, botId: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-databases/${id}/bind-bot`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ botId })
  });
  if (!response.success) {
    throw new Error(response.message || "绑定 Bot 失败");
  }
}

export async function unbindAiDatabaseBot(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-databases/${id}/bind-bot`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "解除 Bot 绑定失败");
  }
}

export async function getAiDatabaseRecordsPaged(databaseId: number, request: PagedRequest) {
  const query = toQuery(request);
  const response = await requestApi<ApiResponse<PagedResult<AiDatabaseRecordListItem>>>(
    `/ai-databases/${databaseId}/records?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询数据库记录失败");
  }

  return response.data;
}

export async function createAiDatabaseRecord(databaseId: number, request: AiDatabaseRecordCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/ai-databases/${databaseId}/records`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "创建数据库记录失败");
  }

  return Number(response.data.id);
}

export async function updateAiDatabaseRecord(
  databaseId: number,
  recordId: number,
  request: AiDatabaseRecordUpdateRequest
) {
  const response = await requestApi<ApiResponse<object>>(`/ai-databases/${databaseId}/records/${recordId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新数据库记录失败");
  }
}

export async function deleteAiDatabaseRecord(databaseId: number, recordId: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-databases/${databaseId}/records/${recordId}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除数据库记录失败");
  }
}

export async function getAiDatabaseSchema(databaseId: number) {
  const response = await requestApi<ApiResponse<{ tableSchema: string }>>(`/ai-databases/${databaseId}/schema`);
  if (!response.data) {
    throw new Error(response.message || "查询数据库 Schema 失败");
  }

  return response.data.tableSchema;
}

export async function validateAiDatabaseSchema(databaseId: number, tableSchema: string) {
  const response = await requestApi<ApiResponse<AiDatabaseSchemaValidateResult>>(
    `/ai-databases/${databaseId}/schema/validate`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ tableSchema })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "校验 Schema 失败");
  }

  return response.data;
}

export async function submitAiDatabaseImport(databaseId: number, request: AiDatabaseImportRequest) {
  const response = await requestApi<ApiResponse<{ taskId: string }>>(`/ai-databases/${databaseId}/imports`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "提交导入任务失败");
  }

  return Number(response.data.taskId);
}

export async function getAiDatabaseImportProgress(databaseId: number) {
  const response = await requestApi<ApiResponse<AiDatabaseImportProgress>>(`/ai-databases/${databaseId}/imports/latest`);
  if (!response.data) {
    throw new Error(response.message || "查询导入进度失败");
  }

  return response.data;
}

export async function downloadAiDatabaseTemplate(databaseId: number) {
  return requestApiBlob(`/ai-databases/${databaseId}/template`);
}
