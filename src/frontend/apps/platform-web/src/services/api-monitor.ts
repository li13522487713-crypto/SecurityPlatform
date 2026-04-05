import type { ApiResponse, PagedResult } from "@atlas/shared-core";
import { requestApi } from "@/services/api-core";

export interface QueueStat {
  queueName: string;
  pending: number;
  processing: number;
  completed: number;
  failed: number;
  deadLettered: number;
}

export interface QueueMsg {
  id: number;
  queueName: string;
  messageType: string;
  status: string;
  retryCount: number;
  errorMessage?: string;
  enqueuedAt: string;
  completedAt?: string;
}

export interface QueueMessagePage {
  pageIndex: number;
  pageSize: number;
  total: number;
  items: QueueMsg[];
}

export interface ScheduledJobDto {
  id: string;
  name: string;
  cronExpression: string;
  queue: string;
  isEnabled: boolean;
  lastRunAt?: string;
  lastRunStatus?: string;
  nextRunAt?: string;
}

export interface ScheduledJobExecutionDto {
  jobId: string;
  createdAt?: string;
  startedAt?: string;
  finishedAt?: string;
  durationMilliseconds?: number;
  state?: string;
  errorMessage?: string;
}

export async function getMessageQueueQueues(): Promise<QueueStat[]> {
  const response = await requestApi<ApiResponse<QueueStat[]>>("/admin/message-queue/queues");
  return response.data ?? [];
}

export async function getMessageQueueStats(): Promise<QueueStat | null> {
  const response = await requestApi<ApiResponse<QueueStat>>("/admin/message-queue/stats");
  return response.data ?? null;
}

export async function getMessageQueueMessages(
  queueName: string,
  params: { pageIndex: number; pageSize: number; status?: string }
): Promise<QueueMessagePage | null> {
  const query = new URLSearchParams({
    pageIndex: String(params.pageIndex),
    pageSize: String(params.pageSize)
  });
  if (params.status) {
    query.set("status", params.status);
  }

  const response = await requestApi<ApiResponse<QueueMessagePage>>(
    `/admin/message-queue/queues/${encodeURIComponent(queueName)}/messages?${query.toString()}`
  );
  return response.data ?? null;
}

export async function retryMessageQueueDeadLetters(queueName: string): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `/admin/message-queue/queues/${encodeURIComponent(queueName)}/dead-letters/retry`,
    { method: "POST" }
  );
}

export async function purgeMessageQueueDeadLetters(queueName: string): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `/admin/message-queue/queues/${encodeURIComponent(queueName)}/dead-letters`,
    { method: "DELETE" }
  );
}

export async function getScheduledJobsPaged(params: {
  pageIndex: number;
  pageSize: number;
}): Promise<PagedResult<ScheduledJobDto>> {
  const query = new URLSearchParams({
    pageIndex: String(params.pageIndex),
    pageSize: String(params.pageSize)
  });
  const response = await requestApi<ApiResponse<PagedResult<ScheduledJobDto>>>(`/scheduled-jobs?${query.toString()}`);
  if (!response.data) {
    throw new Error(response.message || "加载定时任务失败");
  }
  return response.data;
}

export async function triggerScheduledJob(jobId: string): Promise<void> {
  await requestApi<ApiResponse<object>>(`/scheduled-jobs/${jobId}/trigger`, { method: "POST" });
}

export async function disableScheduledJob(jobId: string): Promise<void> {
  await requestApi<ApiResponse<object>>(`/scheduled-jobs/${jobId}/disable`, { method: "PUT" });
}

export async function enableScheduledJob(jobId: string): Promise<void> {
  await requestApi<ApiResponse<object>>(`/scheduled-jobs/${jobId}/enable`, { method: "PUT" });
}

export async function getScheduledJobExecutionsPaged(
  jobId: string,
  params: { pageIndex: number; pageSize: number }
): Promise<PagedResult<ScheduledJobExecutionDto>> {
  const query = new URLSearchParams({
    pageIndex: String(params.pageIndex),
    pageSize: String(params.pageSize)
  });
  const response = await requestApi<ApiResponse<PagedResult<ScheduledJobExecutionDto>>>(
    `/scheduled-jobs/${jobId}/executions?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "加载执行历史失败");
  }
  return response.data;
}
