import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { extractResourceId, requestApi, resolveAppHostPrefix, toQuery } from "./api-core";
import type {
  AgentCreateRequest,
  AgentDetail,
  AgentListItem,
  AgentUpdateRequest
} from "./api-agent";

export type AiAssistantFunctionType = "sql" | "workflow";

export interface AiAssistantGenerateResponse {
  result: string;
  explanation: string;
}

const endpointMap: Record<AiAssistantFunctionType, string> = {
  sql: "/api/v1/ai/generate-sql",
  workflow: "/api/v1/ai/suggest-workflow",
};

export interface AiAssistantPublicationListItem {
  id: number;
  agentId: number;
  version: number;
  isActive: boolean;
  embedToken: string;
  embedTokenExpiresAt: string;
  releaseNote?: string;
  publishedByUserId: number;
  createdAt: string;
  updatedAt?: string;
  revokedAt?: string;
}

export interface AiAssistantPublishRequest {
  releaseNote?: string;
}

export interface AiAssistantPublishResult {
  publicationId: number;
  agentId: number;
  version: number;
  embedToken: string;
  embedTokenExpiresAt: string;
}

export interface AiAssistantPublicationListItem {
  id: number;
  agentId: number;
  version: number;
  isActive: boolean;
  embedToken: string;
  embedTokenExpiresAt: string;
  releaseNote?: string;
  publishedByUserId: number;
  createdAt: string;
  updatedAt?: string;
  revokedAt?: string;
}

export interface AiAssistantWorkflowBinding {
  workflowId?: string;
  workflowName?: string;
}

function assistantBase(): string {
  return "/ai-assistants";
}

export async function getAiAssistantsPaged(params?: {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  status?: string;
  workspaceId?: string;
}): Promise<PagedResult<AgentListItem>> {
  const request: PagedRequest = {
    pageIndex: params?.pageIndex ?? 1,
    pageSize: params?.pageSize ?? 20
  };
  const response = await requestApi<ApiResponse<PagedResult<AgentListItem>>>(
    `${assistantBase()}?${toQuery(request, {
      keyword: params?.keyword,
      status: params?.status,
      workspaceId: params?.workspaceId
    })}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询智能体失败");
  }
  return response.data;
}

export async function getAiAssistantById(id: string): Promise<AgentDetail> {
  const response = await requestApi<ApiResponse<AgentDetail>>(`${assistantBase()}/${encodeURIComponent(id)}`);
  if (!response.data) {
    throw new Error(response.message || "查询智能体详情失败");
  }
  return response.data;
}

export async function createAiAssistant(request: AgentCreateRequest): Promise<string> {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>(assistantBase(), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  const id = extractResourceId(response.data);
  if (!response.success || !id) {
    throw new Error(response.message || "创建智能体失败");
  }
  return id;
}

export async function createAiAssistantInWorkspace(request: AgentCreateRequest, workspaceId?: string): Promise<string> {
  return createAiAssistant({
    ...request,
    workspaceId: workspaceId ? Number(workspaceId) : undefined
  } as AgentCreateRequest);
}

export async function updateAiAssistant(id: string, request: AgentUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`${assistantBase()}/${encodeURIComponent(id)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新智能体失败");
  }
}

export async function deleteAiAssistant(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`${assistantBase()}/${encodeURIComponent(id)}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除智能体失败");
  }
}

export async function bindAiAssistantWorkflow(id: string, workflowId?: string): Promise<AiAssistantWorkflowBinding> {
  const response = await requestApi<ApiResponse<{ workflowId?: number | string; workflowName?: string }>>(
    `${assistantBase()}/${encodeURIComponent(id)}/workflow-bindings`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        workflowId: workflowId ? Number(workflowId) : null
      })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "绑定智能体工作流失败");
  }

  return {
    workflowId: response.data.workflowId !== undefined ? String(response.data.workflowId) : undefined,
    workflowName: response.data.workflowName
  };
}

export async function getAiAssistantPublications(id: string): Promise<AiAssistantPublicationListItem[]> {
  const response = await requestApi<ApiResponse<AiAssistantPublicationListItem[]>>(
    `${assistantBase()}/${encodeURIComponent(id)}/publications`
  );
  if (!response.data) {
    throw new Error(response.message || "查询智能体发布记录失败");
  }
  return response.data;
}

export async function publishAiAssistant(id: string, request: AiAssistantPublishRequest = {}): Promise<AiAssistantPublishResult> {
  const response = await requestApi<ApiResponse<AiAssistantPublishResult>>(
    `${assistantBase()}/${encodeURIComponent(id)}/publish`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "发布智能体失败");
  }
  return response.data;
}

export async function regenerateAiAssistantEmbedToken(id: string): Promise<AiAssistantPublishResult> {
  const response = await requestApi<ApiResponse<AiAssistantPublishResult>>(
    `${assistantBase()}/${encodeURIComponent(id)}/embed-token`,
    {
      method: "POST"
    }
  );
  if (!response.data) {
    throw new Error(response.message || "刷新智能体嵌入令牌失败");
  }
  return response.data;
}

export async function generateByAiAssistant(
  appKey: string,
  type: AiAssistantFunctionType,
  description: string
): Promise<AiAssistantGenerateResponse | null> {
  const base = resolveAppHostPrefix(appKey);
  const response = await requestApi<ApiResponse<AiAssistantGenerateResponse>>(
    `${base}${endpointMap[type]}`,
    {
      method: "POST",
      body: JSON.stringify({ description }),
    }
  );
  if (!response.success) throw new Error(response.message || "Request failed");
  return response.data ?? null;
}
