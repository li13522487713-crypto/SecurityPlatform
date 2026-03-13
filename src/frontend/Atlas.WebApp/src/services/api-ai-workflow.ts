import { requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";

export interface AiWorkflowDefinitionDto {
  id: number;
  name: string;
  description?: string;
  status: string;
  publishVersion: number;
  creatorId: number;
  createdAt: string;
  updatedAt?: string;
  publishedAt?: string;
}

export interface AiWorkflowDetailDto extends AiWorkflowDefinitionDto {
  canvasJson: string;
  definitionJson: string;
}

export interface AiWorkflowNodeTypeDto {
  key: string;
  name: string;
  category: string;
  description: string;
}

export async function getAiWorkflowsPaged(request: PagedRequest, keyword?: string) {
  const query = toQuery(request, { keyword });
  const response = await requestApi<ApiResponse<PagedResult<AiWorkflowDefinitionDto>>>(`/ai-workflows?${query}`);
  if (!response.data) throw new Error(response.message || "查询工作流失败");
  return response.data;
}

export async function getAiWorkflowById(id: number) {
  const response = await requestApi<ApiResponse<AiWorkflowDetailDto>>(`/ai-workflows/${id}`);
  if (!response.data) throw new Error(response.message || "查询工作流失败");
  return response.data;
}

export async function createAiWorkflow(request: {
  name: string;
  description?: string;
  canvasJson: string;
  definitionJson: string;
}) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/ai-workflows", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) throw new Error(response.message || "创建工作流失败");
  return Number(response.data.id);
}

export async function saveAiWorkflow(id: number, request: { canvasJson: string; definitionJson: string }) {
  const response = await requestApi<ApiResponse<object>>(`/ai-workflows/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "保存工作流失败");
}

export async function updateAiWorkflowMeta(id: number, request: { name: string; description?: string }) {
  const response = await requestApi<ApiResponse<object>>(`/ai-workflows/${id}/meta`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新元信息失败");
}

export async function deleteAiWorkflow(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-workflows/${id}`, { method: "DELETE" });
  if (!response.success) throw new Error(response.message || "删除工作流失败");
}

export async function copyAiWorkflow(id: number) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/ai-workflows/${id}/copy`, { method: "POST" });
  if (!response.success || !response.data) throw new Error(response.message || "复制工作流失败");
  return Number(response.data.id);
}

export async function publishAiWorkflow(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-workflows/${id}/publish`, { method: "POST" });
  if (!response.success) throw new Error(response.message || "发布工作流失败");
}

export async function validateAiWorkflow(id: number) {
  const response = await requestApi<ApiResponse<{ isValid: boolean; errors: string[] }>>(`/ai-workflows/${id}/validate`, {
    method: "POST"
  });
  if (!response.data) throw new Error(response.message || "校验失败");
  return response.data;
}

export async function runAiWorkflow(id: number, inputs: Record<string, unknown>) {
  const response = await requestApi<ApiResponse<{ executionId: string }>>(`/ai-workflows/${id}/run`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ inputs })
  });
  if (!response.data) throw new Error(response.message || "执行失败");
  return response.data;
}

export async function cancelAiWorkflowExecution(executionId: string) {
  const response = await requestApi<ApiResponse<object>>(`/ai-workflows/executions/${executionId}/cancel`, { method: "POST" });
  if (!response.success) throw new Error(response.message || "取消执行失败");
}

export async function getAiWorkflowExecutionProgress(executionId: string) {
  const response = await requestApi<ApiResponse<{
    executionId: string;
    workflowId: string;
    version: number;
    status: string;
    createdAt: string;
    completedAt?: string;
  }>>(`/ai-workflows/executions/${executionId}/progress`);
  if (!response.data) throw new Error(response.message || "查询执行进度失败");
  return response.data;
}

export async function getAiWorkflowExecutionNodes(executionId: string) {
  const response = await requestApi<ApiResponse<Array<{
    pointerId: string;
    stepId: number;
    stepName?: string;
    status: string;
    startTime?: string;
    endTime?: string;
    outcome?: unknown;
  }>>>(`/ai-workflows/executions/${executionId}/nodes`);
  if (!response.data) throw new Error(response.message || "查询节点历史失败");
  return response.data;
}

export async function getAiWorkflowNodeTypes() {
  const response = await requestApi<ApiResponse<AiWorkflowNodeTypeDto[]>>(`/ai-workflows/node-types`);
  if (!response.data) throw new Error(response.message || "查询节点类型失败");
  return response.data;
}
