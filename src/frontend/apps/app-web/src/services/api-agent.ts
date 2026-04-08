import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@atlas/shared-core";
import type { PagedResult } from "@atlas/shared-core";

export interface AgentDetail {
  id: string;
  name: string;
  description?: string;
  avatarUrl?: string;
  systemPrompt?: string;
  modelConfigId?: string;
  modelName?: string;
  temperature?: number;
  maxTokens?: number;
  status: string;
}

export interface AgentListItem {
  id: string;
  name: string;
  description?: string;
  avatarUrl?: string;
  status: string;
  modelName?: string;
  createdAt?: string;
  publishVersion?: number;
}

export interface AgentCreateRequest {
  name: string;
  description?: string;
  systemPrompt?: string;
  modelConfigId?: string;
  modelName?: string;
  temperature?: number;
  maxTokens?: number;
  enableMemory?: boolean;
  enableShortTermMemory?: boolean;
  enableLongTermMemory?: boolean;
  longTermMemoryTopK?: number;
}

export interface AgentUpdateRequest {
  name: string;
  description?: string;
  avatarUrl?: string;
  systemPrompt?: string;
  modelConfigId?: string;
  modelName?: string;
  temperature?: number;
  maxTokens?: number;
  enableMemory?: boolean;
  enableShortTermMemory?: boolean;
  enableLongTermMemory?: boolean;
  longTermMemoryTopK?: number;
}

export async function getAgentsPaged(params?: {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  status?: string;
}): Promise<PagedResult<AgentListItem>> {
  const pageIndex = params?.pageIndex ?? 1;
  const pageSize = params?.pageSize ?? 20;
  const query = new URLSearchParams({
    pageIndex: String(pageIndex),
    pageSize: String(pageSize),
  });

  if (params?.keyword) {
    query.set("keyword", params.keyword);
  }

  if (params?.status) {
    query.set("status", params.status);
  }

  const response = await requestApi<ApiResponse<PagedResult<AgentListItem>>>(`/agents?${query.toString()}`);
  if (!response.data) {
    throw new Error(response.message || "Failed to query agents");
  }

  return response.data;
}

export async function getAgentById(id: string): Promise<AgentDetail> {
  const response = await requestApi<ApiResponse<AgentDetail>>(`/agents/${id}`);
  if (!response.data) {
    throw new Error(response.message || "Failed to query agent");
  }
  return response.data;
}

export async function createAgent(request: AgentCreateRequest): Promise<string> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/agents", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!response.data) {
    throw new Error(response.message || "Failed to create agent");
  }
  return response.data.id;
}

export async function updateAgent(id: string, request: AgentUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agents/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!response.success) {
    throw new Error(response.message || "Failed to update agent");
  }
}

export async function deleteAgent(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agents/${id}`, {
    method: "DELETE",
  });
  if (!response.success) {
    throw new Error(response.message || "Failed to delete agent");
  }
}
