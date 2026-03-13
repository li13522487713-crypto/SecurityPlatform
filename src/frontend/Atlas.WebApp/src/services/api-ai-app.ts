import { requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";

export type AiAppStatus = 0 | 1;

export interface AiAppListItem {
  id: number;
  name: string;
  description?: string;
  icon?: string;
  agentId?: number;
  workflowId?: number;
  promptTemplateId?: number;
  status: AiAppStatus;
  publishVersion: number;
  createdAt: string;
  updatedAt?: string;
  publishedAt?: string;
}

export interface AiAppPublishRecordItem {
  id: number;
  appId: number;
  version: string;
  releaseNote?: string;
  publishedByUserId: number;
  createdAt: string;
}

export interface AiAppDetail extends AiAppListItem {
  publishRecords: AiAppPublishRecordItem[];
}

export interface AiAppCreateRequest {
  name: string;
  description?: string;
  icon?: string;
  agentId?: number;
  workflowId?: number;
  promptTemplateId?: number;
}

export interface AiAppUpdateRequest extends AiAppCreateRequest {}

export interface AiAppVersionCheckResult {
  appId: number;
  currentPublishVersion: number;
  latestVersion?: string;
  latestPublishedAt?: string;
}

export interface AiAppResourceCopyTaskProgress {
  taskId: number;
  appId: number;
  sourceAppId: number;
  status: 0 | 1 | 2 | 3;
  totalItems: number;
  copiedItems: number;
  errorMessage?: string;
  createdAt: string;
  updatedAt?: string;
}

export async function getAiAppsPaged(request: PagedRequest, keyword?: string) {
  const query = toQuery(request, { keyword });
  const response = await requestApi<ApiResponse<PagedResult<AiAppListItem>>>(`/ai-apps?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询应用列表失败");
  }

  return response.data;
}

export async function getAiAppById(id: number) {
  const response = await requestApi<ApiResponse<AiAppDetail>>(`/ai-apps/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询应用详情失败");
  }

  return response.data;
}

export async function createAiApp(request: AiAppCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/ai-apps", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "创建应用失败");
  }

  return Number(response.data.id);
}

export async function updateAiApp(id: number, request: AiAppUpdateRequest) {
  const response = await requestApi<ApiResponse<object>>(`/ai-apps/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新应用失败");
  }
}

export async function deleteAiApp(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-apps/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除应用失败");
  }
}

export async function publishAiApp(id: number, releaseNote?: string) {
  const response = await requestApi<ApiResponse<object>>(`/ai-apps/${id}/publish`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ releaseNote })
  });
  if (!response.success) {
    throw new Error(response.message || "发布应用失败");
  }
}

export async function checkAiAppVersion(id: number) {
  const response = await requestApi<ApiResponse<AiAppVersionCheckResult>>(`/ai-apps/${id}/version-check`);
  if (!response.data) {
    throw new Error(response.message || "检查版本失败");
  }

  return response.data;
}

export async function submitAiAppResourceCopy(id: number, sourceAppId: number) {
  const response = await requestApi<ApiResponse<{ taskId: string }>>(`/ai-apps/${id}/resource-copy-tasks`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ sourceAppId })
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "提交资源复制任务失败");
  }

  return Number(response.data.taskId);
}

export async function getAiAppLatestResourceCopyTask(id: number) {
  const response = await requestApi<ApiResponse<AiAppResourceCopyTaskProgress>>(
    `/ai-apps/${id}/resource-copy-tasks/latest`
  );
  if (!response.data) {
    throw new Error(response.message || "查询资源复制进度失败");
  }

  return response.data;
}
