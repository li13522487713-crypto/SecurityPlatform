import { extractResourceId, requestApi } from "@/services/api-core";
import type { ApiResponse, PagedResult } from "@atlas/shared-react-core/types";

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
  defaultWorkflowId?: string;
  defaultWorkflowName?: string;
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
  defaultWorkflowId?: string;
  defaultWorkflowName?: string;
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
  defaultWorkflowId?: string;
  defaultWorkflowName?: string;
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
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>("/agents", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  const agentId = extractResourceId(response.data);
  if (!response.data || !agentId) {
    throw new Error(response.message || "Failed to create agent");
  }
  return agentId;
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

export async function bindAgentWorkflow(
  id: string,
  workflowId?: string
): Promise<{ workflowId?: string; workflowName?: string }> {
  const response = await requestApi<ApiResponse<{ workflowId?: string | number; workflowName?: string }>>(
    `/draft-agents/${id}/workflow-bindings`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        workflowId: workflowId ? Number(workflowId) : null
      })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "Failed to bind workflow");
  }

  return {
    workflowId: response.data.workflowId !== undefined ? String(response.data.workflowId) : undefined,
    workflowName: response.data.workflowName
  };
}

export async function deleteAgent(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agents/${id}`, {
    method: "DELETE",
  });
  if (!response.success) {
    throw new Error(response.message || "Failed to delete agent");
  }
}
