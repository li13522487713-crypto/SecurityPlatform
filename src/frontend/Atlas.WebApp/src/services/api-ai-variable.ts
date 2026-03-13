import { requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";

export type AiVariableScope = 0 | 1 | 2;

export interface AiVariableListItem {
  id: number;
  key: string;
  value?: string;
  scope: AiVariableScope;
  scopeId?: number;
  createdAt: string;
  updatedAt?: string;
}

export interface AiVariableDetail extends AiVariableListItem {}

export interface AiVariableCreateRequest {
  key: string;
  value?: string;
  scope: AiVariableScope;
  scopeId?: number;
}

export interface AiVariableUpdateRequest extends AiVariableCreateRequest {}

export interface AiSystemVariableDefinition {
  key: string;
  name: string;
  description: string;
  defaultValue?: string;
}

export async function getAiVariablesPaged(
  request: PagedRequest,
  filters?: {
    scope?: AiVariableScope;
    scopeId?: number;
  }
) {
  const query = toQuery(request, {
    scope: filters?.scope !== undefined ? String(filters.scope) : undefined,
    scopeId: filters?.scopeId !== undefined ? String(filters.scopeId) : undefined
  });
  const response = await requestApi<ApiResponse<PagedResult<AiVariableListItem>>>(`/ai-variables?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询变量列表失败");
  }

  return response.data;
}

export async function getAiVariableById(id: number) {
  const response = await requestApi<ApiResponse<AiVariableDetail>>(`/ai-variables/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询变量详情失败");
  }

  return response.data;
}

export async function createAiVariable(request: AiVariableCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/ai-variables", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "创建变量失败");
  }

  return Number(response.data.id);
}

export async function updateAiVariable(id: number, request: AiVariableUpdateRequest) {
  const response = await requestApi<ApiResponse<object>>(`/ai-variables/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新变量失败");
  }
}

export async function deleteAiVariable(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-variables/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除变量失败");
  }
}

export async function getAiSystemVariableDefinitions() {
  const response = await requestApi<ApiResponse<AiSystemVariableDefinition[]>>("/ai-variables/system-definitions");
  if (!response.data) {
    throw new Error(response.message || "查询系统变量失败");
  }

  return response.data;
}
