import { requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";

export interface AgentListItem {
  id: number;
  name: string;
  description?: string;
  avatarUrl?: string;
  status: string;
  modelName?: string;
  createdAt: string;
  publishVersion: number;
}

export interface AgentDetail {
  id: number;
  name: string;
  description?: string;
  avatarUrl?: string;
  systemPrompt?: string;
  modelConfigId?: number;
  modelName?: string;
  temperature?: number;
  maxTokens?: number;
  status: string;
  creatorId: number;
  createdAt: string;
  updatedAt?: string;
  publishedAt?: string;
  publishVersion: number;
  knowledgeBaseIds?: number[];
}

export interface AgentCreateRequest {
  name: string;
  description?: string;
  systemPrompt?: string;
  modelConfigId?: number;
  modelName?: string;
  temperature?: number;
  maxTokens?: number;
}

export interface AgentUpdateRequest extends AgentCreateRequest {
  avatarUrl?: string;
  knowledgeBaseIds?: number[];
}

export async function getAgentsPaged(request: PagedRequest, status?: string) {
  const query = toQuery(request, { status });
  const response = await requestApi<ApiResponse<PagedResult<AgentListItem>>>(`/agents?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询 Agent 列表失败");
  }
  return response.data;
}

export async function getAgentById(id: number) {
  const response = await requestApi<ApiResponse<AgentDetail>>(`/agents/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询 Agent 失败");
  }
  return response.data;
}

export async function createAgent(request: AgentCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/agents", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建 Agent 失败");
  }
}

export async function updateAgent(id: number, request: AgentUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agents/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新 Agent 失败");
  }
}

export async function deleteAgent(id: number) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agents/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除 Agent 失败");
  }
}

export async function duplicateAgent(id: number) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agents/${id}/duplicate`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "复制 Agent 失败");
  }
}

export async function publishAgent(id: number) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agents/${id}/publish`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "发布 Agent 失败");
  }
}
