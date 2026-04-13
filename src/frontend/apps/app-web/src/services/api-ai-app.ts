import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi, toQuery } from "./api-core";

export interface AiAppListItem {
  id: string;
  name: string;
  description?: string;
  icon?: string;
  agentId?: string;
  workflowId?: string;
  promptTemplateId?: string;
  status: string;
  publishVersion: number;
  createdAt: string;
  updatedAt?: string;
  publishedAt?: string;
}

export interface AiAppUpdateRequest {
  name: string;
  description?: string;
  icon?: string;
  agentId?: string;
  workflowId?: string;
  promptTemplateId?: string;
}

export async function getAiAppsPaged(request: PagedRequest): Promise<PagedResult<AiAppListItem>> {
  const response = await requestApi<ApiResponse<PagedResult<AiAppListItem>>>(`/ai-apps?${toQuery(request)}`);
  if (!response.data) {
    throw new Error(response.message || "查询应用失败");
  }
  return response.data;
}

export async function updateAiApp(id: string, request: AiAppUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-apps/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify({
      ...request,
      agentId: request.agentId ? Number(request.agentId) : null,
      workflowId: request.workflowId ? Number(request.workflowId) : null,
      promptTemplateId: request.promptTemplateId ? Number(request.promptTemplateId) : null
    })
  });
  if (!response.success) {
    throw new Error(response.message || "更新应用失败");
  }
}

export async function deleteAiApp(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-apps/${encodeURIComponent(id)}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除应用失败");
  }
}
