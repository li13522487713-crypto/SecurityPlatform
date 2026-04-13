import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { extractResourceId, requestApi, toQuery } from "./api-core";

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

export interface AiDatabaseSchemaValidateResult {
  isValid: boolean;
  errors: string[];
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
