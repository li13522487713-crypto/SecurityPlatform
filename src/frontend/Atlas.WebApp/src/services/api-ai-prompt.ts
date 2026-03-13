import { requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";

export interface AiPromptTemplateListItem {
  id: number;
  name: string;
  description?: string;
  category?: string;
  content: string;
  tags: string[];
  isSystem: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface AiPromptTemplateDetail extends AiPromptTemplateListItem {}

export interface AiPromptTemplateCreateRequest {
  name: string;
  description?: string;
  category?: string;
  content: string;
  tags?: string[];
  isSystem: boolean;
}

export interface AiPromptTemplateUpdateRequest {
  name: string;
  description?: string;
  category?: string;
  content: string;
  tags?: string[];
}

export async function getAiPromptTemplatesPaged(request: PagedRequest, category?: string) {
  const query = toQuery(request, { category });
  const response = await requestApi<ApiResponse<PagedResult<AiPromptTemplateListItem>>>(`/ai-prompts?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询 Prompt 列表失败");
  }

  return response.data;
}

export async function getAiPromptTemplateById(id: number) {
  const response = await requestApi<ApiResponse<AiPromptTemplateDetail>>(`/ai-prompts/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询 Prompt 详情失败");
  }

  return response.data;
}

export async function createAiPromptTemplate(request: AiPromptTemplateCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/ai-prompts", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "创建 Prompt 失败");
  }

  return Number(response.data.id);
}

export async function updateAiPromptTemplate(id: number, request: AiPromptTemplateUpdateRequest) {
  const response = await requestApi<ApiResponse<object>>(`/ai-prompts/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新 Prompt 失败");
  }
}

export async function deleteAiPromptTemplate(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-prompts/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除 Prompt 失败");
  }
}
