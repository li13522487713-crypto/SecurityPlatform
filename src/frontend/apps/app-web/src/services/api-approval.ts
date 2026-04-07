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

export interface ApprovalHistoryEvent {
  id: string;
  eventType: number;
  actorUserId?: number | null;
  payloadJson?: string | null;
  occurredAt: string;
}

export interface ApprovalInstanceDetail {
  id: string;
  definitionId?: number | null;
  flowName: string;
  title: string;
  status: number;
  dataJson?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
}

export async function getMyTasksPaged(pagedRequest: PagedRequest, status?: number): Promise<PagedResult<ApprovalTaskItem>> {
  const params = new URLSearchParams(toQuery(pagedRequest));
  if (status !== undefined) params.append("status", status.toString());
  const response = await requestApi<ApiResponse<PagedResult<ApprovalTaskItem>>>(`/approval/tasks/my?${params}`);
  if (!response.data) throw new Error(response.message || "Query failed");
  return response.data;
}

export async function getMyInstancesPaged(pagedRequest: PagedRequest, status?: number): Promise<PagedResult<ApprovalInstanceItem>> {
  const params = new URLSearchParams(toQuery(pagedRequest));
  if (status !== undefined) params.append("status", status.toString());
  const response = await requestApi<ApiResponse<PagedResult<ApprovalInstanceItem>>>(`/approval/instances/my?${params}`);
  if (!response.data) throw new Error(response.message || "Query failed");
  return response.data;
}

export async function getMyCopyRecordsPaged(pagedRequest: PagedRequest, isRead?: boolean): Promise<PagedResult<ApprovalCopyRecord>> {
  const params = new URLSearchParams(toQuery(pagedRequest));
  if (isRead !== undefined) params.append("isRead", String(isRead));
  const response = await requestApi<ApiResponse<PagedResult<ApprovalCopyRecord>>>(`/approval/copy-records/my-copies?${params}`);
  if (!response.data) throw new Error(response.message || "Query failed");
  return response.data;
}

export async function getApprovalInstanceById(id: string): Promise<ApprovalInstanceDetail> {
  const response = await requestApi<ApiResponse<ApprovalInstanceDetail>>(`/approval/instances/${id}`);
  if (!response.data) throw new Error(response.message || "Query failed");
  return response.data;
}

export async function getApprovalInstanceHistory(
  id: string,
  pagedRequest: PagedRequest
): Promise<PagedResult<ApprovalHistoryEvent>> {
  const params = new URLSearchParams(toQuery(pagedRequest));
  const response = await requestApi<ApiResponse<PagedResult<ApprovalHistoryEvent>>>(
    `/approval/instances/${id}/history?${params.toString()}`
  );
  if (!response.data) throw new Error(response.message || "Query failed");
  return response.data;
}

export async function cancelApprovalInstance(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<void>>(`/approval/instances/${id}/cancellation`, {
    method: "POST"
  });
  if (!response.success) throw new Error(response.message || "Cancel failed");
}

export async function suspendInstance(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<void>>(`/approval/instances/${id}/suspension`, {
    method: "POST"
  });
  if (!response.success) throw new Error(response.message || "Suspend failed");
}

export async function activateInstance(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<void>>(`/approval/instances/${id}/activation`, {
    method: "POST"
  });
  if (!response.success) throw new Error(response.message || "Activate failed");
}
