import type { ApiResponse, PagedResult, PagedRequest } from "@atlas/shared-core";
import { requestApi, toQuery } from "./api-core";

export interface ApprovalTaskItem {
  id: string;
  instanceId: string;
  flowName: string;
  title: string;
  currentNodeName: string;
  status: number;
  createdAt: string;
  slaRemainingMinutes?: number;
}

export interface ApprovalInstanceItem {
  id: string;
  flowName: string;
  title: string;
  status: number;
  createdAt: string;
  completedAt?: string;
}

export interface ApprovalCopyRecord {
  id: string;
  instanceId: string;
  flowName: string;
  title: string;
  isRead: boolean;
  createdAt: string;
}

export async function getMyTasksPaged(pagedRequest: PagedRequest, status?: number): Promise<PagedResult<ApprovalTaskItem>> {
  const params = new URLSearchParams(toQuery(pagedRequest));
  if (status !== undefined) params.append("status", status.toString());
  const response = await requestApi<ApiResponse<PagedResult<ApprovalTaskItem>>>(`/approval/tasks/my?${params}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getMyInstancesPaged(pagedRequest: PagedRequest, status?: number): Promise<PagedResult<ApprovalInstanceItem>> {
  const params = new URLSearchParams(toQuery(pagedRequest));
  if (status !== undefined) params.append("status", status.toString());
  const response = await requestApi<ApiResponse<PagedResult<ApprovalInstanceItem>>>(`/approval/instances/my?${params}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getMyCopyRecordsPaged(pagedRequest: PagedRequest, isRead?: boolean): Promise<PagedResult<ApprovalCopyRecord>> {
  const params = new URLSearchParams(toQuery(pagedRequest));
  if (isRead !== undefined) params.append("isRead", String(isRead));
  const response = await requestApi<ApiResponse<PagedResult<ApprovalCopyRecord>>>(`/approval/copy-records/my-copies?${params}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}
