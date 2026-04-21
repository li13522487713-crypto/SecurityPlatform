import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { extractResourceId, requestApi, toQuery } from "./api-core";

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

export type AiVariableDetail = AiVariableListItem;

export interface AiVariableCreateRequest {
  key: string;
  value?: string;
  scope: AiVariableScope;
  scopeId?: string | number;
}

export interface AiSystemVariableDefinition {
  key: string;
  name: string;
  description: string;
  defaultValue?: string;
}

export async function getAiVariablesPaged(
  request: PagedRequest,
  filters?: { keyword?: string; scope?: AiVariableScope; scopeId?: string | number }
): Promise<PagedResult<AiVariableListItem>> {
  const response = await requestApi<ApiResponse<PagedResult<AiVariableListItem>>>(`/ai-variables?${toQuery(request, {
    keyword: filters?.keyword,
    scope: filters?.scope !== undefined ? String(filters.scope) : undefined,
    scopeId: filters?.scopeId !== undefined ? String(filters.scopeId) : undefined
  })}`);
  if (!response.data) {
    throw new Error(response.message || "查询变量失败");
  }

  return response.data;
}

export async function getAiVariableById(id: number): Promise<AiVariableDetail> {
  const response = await requestApi<ApiResponse<AiVariableDetail>>(`/ai-variables/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询变量详情失败");
  }

  return response.data;
}

export async function createAiVariable(request: AiVariableCreateRequest): Promise<number> {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>("/ai-variables", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  const variableId = extractResourceId(response.data);
  if (!response.success || !variableId) {
    throw new Error(response.message || "创建变量失败");
  }

  return Number(variableId);
}

export async function updateAiVariable(id: number, request: AiVariableCreateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-variables/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新变量失败");
  }
}

export async function deleteAiVariable(id: number): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-variables/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除变量失败");
  }
}

export async function getAiSystemVariableDefinitions(): Promise<AiSystemVariableDefinition[]> {
  const response = await requestApi<ApiResponse<AiSystemVariableDefinition[]>>("/ai-variables/system-definitions");
  if (!response.data) {
    throw new Error(response.message || "查询系统变量失败");
  }

  return response.data;
}

export function buildWorkspaceVariableFilters(workspaceId: string, keyword?: string): {
  keyword?: string;
  scope: AiVariableScope;
  scopeId: string;
} {
  return {
    keyword,
    scope: 1,
    scopeId: workspaceId
  };
}
