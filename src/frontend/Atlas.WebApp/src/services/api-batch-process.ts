import { requestApi } from './api-core'
import type { ApiResponse, PagedRequest } from '@/types/api'

// ─── Enums ───────────────────────────────────────────────

export enum BatchJobStatus {
  Draft = 0,
  Active = 1,
  Paused = 2,
  Archived = 3,
}

export enum JobExecutionStatus {
  Pending = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Cancelled = 4,
}

export enum ShardExecutionStatus {
  Pending = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Retrying = 4,
}

export enum DeadLetterStatus {
  Pending = 0,
  Retrying = 1,
  Resolved = 2,
  Abandoned = 3,
}

export enum ShardStrategy {
  PrimaryKeyRange = 0,
  TimeWindow = 1,
}

// ─── Types ───────────────────────────────────────────────

export interface BatchJobCreateRequest {
  name: string
  description?: string
  dataSourceType: string
  dataSourceConfig: string
  shardStrategyType: ShardStrategy
  shardConfig: string
  batchSize: number
  maxConcurrency: number
  retryPolicy: string
  timeoutSeconds: number
  cronExpression?: string
}

export interface BatchJobUpdateRequest extends BatchJobCreateRequest {}

export interface BatchJobDefinitionResponse {
  id: string
  name: string
  description: string | null
  dataSourceType: string
  dataSourceConfig: string
  shardStrategyType: ShardStrategy
  shardConfig: string
  batchSize: number
  maxConcurrency: number
  retryPolicy: string
  timeoutSeconds: number
  cronExpression: string | null
  status: BatchJobStatus
  createdAt: string
  updatedAt: string
  createdBy: string
}

export interface BatchJobDefinitionListItem {
  id: string
  name: string
  description: string | null
  shardStrategyType: ShardStrategy
  batchSize: number
  maxConcurrency: number
  status: BatchJobStatus
  createdAt: string
  updatedAt: string
  createdBy: string
}

export interface BatchJobExecutionListItem {
  id: string
  jobDefinitionId: string
  status: JobExecutionStatus
  createdAt: string
  startedAt: string | null
  completedAt: string | null
  totalShards: number
  completedShards: number
  failedShards: number
  processedRecords: number
  failedRecords: number
  triggeredBy: string
}

export interface BatchJobExecutionResponse extends BatchJobExecutionListItem {
  totalRecords: number
  errorMessage: string | null
}

export interface ShardExecutionResponse {
  id: string
  jobExecutionId: string
  shardIndex: number
  shardKey: string
  status: ShardExecutionStatus
  startedAt: string | null
  completedAt: string | null
  processedRecords: number
  failedRecords: number
  lastCheckpointId: string | null
  retryCount: number
  errorMessage: string | null
}

export interface BatchDeadLetterListItem {
  id: string
  jobExecutionId: string
  recordKey: string
  errorType: string
  errorMessage: string
  retryCount: number
  maxRetries: number
  status: DeadLetterStatus
  createdAt: string
  lastRetryAt: string | null
}

export interface BatchDeadLetterResponse extends BatchDeadLetterListItem {
  shardExecutionId: string
  batchExecutionId: string | null
  recordPayload: string
  errorStackTrace: string | null
}

interface PagedData<T> {
  items: T[]
  total: number
  pageIndex: number
  pageSize: number
}

// ─── Batch Jobs API ──────────────────────────────────────

export const getBatchJobsPaged = (params: PagedRequest & { status?: BatchJobStatus }) =>
  requestApi<ApiResponse<PagedData<BatchJobDefinitionListItem>>>(
    `/batch-jobs?pageIndex=${params.pageIndex}&pageSize=${params.pageSize}${params.keyword ? `&keyword=${encodeURIComponent(params.keyword)}` : ''}${params.status != null ? `&status=${params.status}` : ''}`,
  )

export const getBatchJobById = (id: string) =>
  requestApi<ApiResponse<BatchJobDefinitionResponse>>(`/batch-jobs/${id}`)

export const createBatchJob = (body: BatchJobCreateRequest) =>
  requestApi<ApiResponse<{ id: string }>>('/batch-jobs', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const updateBatchJob = (id: string, body: BatchJobUpdateRequest) =>
  requestApi<ApiResponse<void>>(`/batch-jobs/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const activateBatchJob = (id: string) =>
  requestApi<ApiResponse<void>>(`/batch-jobs/${id}/activate`, { method: 'POST' })

export const pauseBatchJob = (id: string) =>
  requestApi<ApiResponse<void>>(`/batch-jobs/${id}/pause`, { method: 'POST' })

export const archiveBatchJob = (id: string) =>
  requestApi<ApiResponse<void>>(`/batch-jobs/${id}/archive`, { method: 'POST' })

export const triggerBatchJob = (id: string) =>
  requestApi<ApiResponse<{ executionId: string }>>(`/batch-jobs/${id}/trigger`, { method: 'POST' })

export const getBatchJobExecutionsPaged = (jobId: string, params: PagedRequest & { status?: JobExecutionStatus }) =>
  requestApi<ApiResponse<PagedData<BatchJobExecutionListItem>>>(
    `/batch-jobs/${jobId}/executions?pageIndex=${params.pageIndex}&pageSize=${params.pageSize}${params.status != null ? `&status=${params.status}` : ''}`,
  )

export const getBatchJobExecutionById = (executionId: string) =>
  requestApi<ApiResponse<BatchJobExecutionResponse>>(`/batch-jobs/executions/${executionId}`)

export const getBatchJobExecutionShards = (executionId: string) =>
  requestApi<ApiResponse<ShardExecutionResponse[]>>(`/batch-jobs/executions/${executionId}/shards`)

export const cancelBatchJobExecution = (executionId: string) =>
  requestApi<ApiResponse<void>>(`/batch-jobs/executions/${executionId}/cancel`, { method: 'POST' })

// ─── Dead Letters API ────────────────────────────────────

export const getDeadLettersPaged = (params: PagedRequest & { jobExecutionId?: string; status?: DeadLetterStatus }) =>
  requestApi<ApiResponse<PagedData<BatchDeadLetterListItem>>>(
    `/batch-dead-letters?pageIndex=${params.pageIndex}&pageSize=${params.pageSize}${params.jobExecutionId ? `&jobExecutionId=${params.jobExecutionId}` : ''}${params.status != null ? `&status=${params.status}` : ''}`,
  )

export const getDeadLetterById = (id: string) =>
  requestApi<ApiResponse<BatchDeadLetterResponse>>(`/batch-dead-letters/${id}`)

export const retryDeadLetter = (id: string) =>
  requestApi<ApiResponse<void>>(`/batch-dead-letters/${id}/retry`, { method: 'POST' })

export const retryDeadLettersBatch = (ids: string[]) =>
  requestApi<ApiResponse<void>>('/batch-dead-letters/batch-retry', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ids: ids.map(Number) }),
  })

export const abandonDeadLetter = (id: string) =>
  requestApi<ApiResponse<void>>(`/batch-dead-letters/${id}/abandon`, { method: 'POST' })

export const abandonDeadLettersBatch = (ids: string[]) =>
  requestApi<ApiResponse<void>>('/batch-dead-letters/batch-abandon', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ids: ids.map(Number) }),
  })
