import type { ApiResponse, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi } from "./api-core";

export type WorkspaceTaskStatus = "pending" | "running" | "succeeded" | "failed";

export interface WorkspaceTaskItemDto {
  id: string;
  name: string;
  type: string;
  status: WorkspaceTaskStatus;
  startedAt: string;
  durationMs: number;
  ownerDisplayName: string;
}

export interface WorkspaceTaskLogEntryDto {
  timestamp: string;
  level: string;
  message: string;
}

export interface WorkspaceTaskDetailDto extends WorkspaceTaskItemDto {
  inputJson?: string | null;
  outputJson?: string | null;
  errorMessage?: string | null;
  logs: WorkspaceTaskLogEntryDto[];
}

export type WorkspaceEvaluationStatus = "pending" | "running" | "succeeded" | "failed";

export interface WorkspaceEvaluationItemDto {
  id: string;
  name: string;
  targetType: string;
  targetId: string;
  testsetId: string;
  status: WorkspaceEvaluationStatus;
  metricSummary: string;
  startedAt: string;
}

export interface WorkspaceEvaluationDetailDto extends WorkspaceEvaluationItemDto {
  totalCount: number;
  passCount: number;
  failCount: number;
  reportJson: string;
}

export interface WorkspaceTestsetItemDto {
  id: string;
  name: string;
  description?: string | null;
  workflowId?: string | null;
  rowCount: number;
  createdAt: string;
  updatedAt: string;
}

function workspaceBase(workspaceId: string): string {
  return `/workspaces/${encodeURIComponent(workspaceId)}`;
}

export async function listWorkspaceTasks(workspaceId: string, input?: {
  status?: WorkspaceTaskStatus;
  type?: string;
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
}): Promise<PagedResult<WorkspaceTaskItemDto>> {
  const params = new URLSearchParams({
    pageIndex: String(input?.pageIndex ?? 1),
    pageSize: String(input?.pageSize ?? 20)
  });
  if (input?.status) {
    params.set("status", input.status);
  }
  if (input?.type) {
    params.set("type", input.type);
  }
  if (input?.keyword) {
    params.set("keyword", input.keyword);
  }

  const response = await requestApi<ApiResponse<PagedResult<WorkspaceTaskItemDto>>>(
    `${workspaceBase(workspaceId)}/tasks?${params.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取任务列表失败");
  }
  return response.data;
}

export async function getWorkspaceTaskById(workspaceId: string, taskId: string): Promise<WorkspaceTaskDetailDto> {
  const response = await requestApi<ApiResponse<WorkspaceTaskDetailDto>>(
    `${workspaceBase(workspaceId)}/tasks/${encodeURIComponent(taskId)}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取任务详情失败");
  }
  return response.data;
}

export async function listWorkspaceEvaluations(workspaceId: string, input?: {
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
}): Promise<PagedResult<WorkspaceEvaluationItemDto>> {
  const params = new URLSearchParams({
    pageIndex: String(input?.pageIndex ?? 1),
    pageSize: String(input?.pageSize ?? 20)
  });
  if (input?.keyword) {
    params.set("keyword", input.keyword);
  }

  const response = await requestApi<ApiResponse<PagedResult<WorkspaceEvaluationItemDto>>>(
    `${workspaceBase(workspaceId)}/evaluations?${params.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取评测列表失败");
  }
  return response.data;
}

export async function getWorkspaceEvaluationById(workspaceId: string, evaluationId: string): Promise<WorkspaceEvaluationDetailDto> {
  const response = await requestApi<ApiResponse<WorkspaceEvaluationDetailDto>>(
    `${workspaceBase(workspaceId)}/evaluations/${encodeURIComponent(evaluationId)}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取评测详情失败");
  }
  return response.data;
}

export async function listWorkspaceTestsets(workspaceId: string, input?: {
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
}): Promise<PagedResult<WorkspaceTestsetItemDto>> {
  const params = new URLSearchParams({
    pageIndex: String(input?.pageIndex ?? 1),
    pageSize: String(input?.pageSize ?? 20)
  });
  if (input?.keyword) {
    params.set("keyword", input.keyword);
  }

  const response = await requestApi<ApiResponse<PagedResult<WorkspaceTestsetItemDto>>>(
    `${workspaceBase(workspaceId)}/testsets?${params.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取测试集列表失败");
  }
  return response.data;
}
