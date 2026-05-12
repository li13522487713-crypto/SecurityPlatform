import type { MicroflowDesignSchema } from "@atlas/microflow";
import type { MicroflowRunCallStackFrame, MicroflowRunSession, MicroflowRuntimeLog, MicroflowTestRunOptions, MicroflowTraceFrame } from "@atlas/microflow";

import type { MicroflowApiResponse } from "./api-envelope";

/**
 * POST /api/microflows/{id}/test-run
 * 若提供 `schema`：以**草稿**执行；未提供时后端使用已保存的 schema/版本（P0 可仅实现草稿运行）。
 */
export interface TestRunMicroflowApiRequest {
  schema?: MicroflowDesignSchema;
  input: Record<string, unknown>;
  options?: MicroflowTestRunOptions;
  debugSessionId?: string;
}

export interface TestRunMicroflowApiResponse {
  session: MicroflowRunSession;
}

export type TestRunMicroflowApiEnvelope = MicroflowApiResponse<TestRunMicroflowApiResponse>;

/**
 * GET /api/microflows/runs/{runId}
 */
export type GetMicroflowRunApiResponse = MicroflowApiResponse<MicroflowRunSession>;

/**
 * GET /api/microflows/{id}/runs?pageIndex=1&pageSize=20&status=all
 */
export interface MicroflowRunHistoryItemDto {
  runId: string;
  microflowId: string;
  schemaId?: string;
  status: "success" | "failed" | "unsupported" | "cancelled";
  errorCode?: string;
  durationMs: number;
  startedAt: string;
  completedAt?: string;
  finalized?: boolean;
  parentRunId?: string;
  rootRunId?: string;
  callFrameId?: string;
  callDepth?: number;
  correlationId?: string;
  traceFrameCount?: number;
  logCount?: number;
  childRunIds?: string[];
  callStack?: string[];
  callStackFrames?: MicroflowRunCallStackFrame[];
  errorMessage?: string;
  summary?: string;
}

export interface ListMicroflowRunsResponseDto {
  items: MicroflowRunHistoryItemDto[];
  total: number;
}

export type ListMicroflowRunsApiResponse = MicroflowApiResponse<ListMicroflowRunsResponseDto>;

/**
 * GET /api/microflows/{id}/runs/{runId}
 */
export type GetMicroflowRunDetailApiResponse = MicroflowApiResponse<MicroflowRunSession>;

/**
 * GET /api/microflows/runs/{runId}/trace
 * Trace 帧须包含 `objectId`、（可选）`flowId` 通过 flow 相关字段、以及 `actionId` 之一（见 @atlas/microflow 的 `MicroflowTraceFrame`）。
 */
export interface GetMicroflowRunTraceResponse {
  runId: string;
  trace: MicroflowTraceFrame[];
  logs: MicroflowRuntimeLog[];
}

export type GetMicroflowRunTraceApiResponse = MicroflowApiResponse<GetMicroflowRunTraceResponse>;

/**
 * POST /api/microflows/runs/{runId}/cancel
 */
export type CancelMicroflowRunResponse = MicroflowApiResponse<{
  runId: string;
  status: "cancelled";
}>;

/**
 * POST /api/v1/microflows/runs:enqueue
 */
export interface EnqueueMicroflowRunRequestDto {
  resourceId: string;
  request: {
    schemaId?: string;
    input: Record<string, unknown>;
    inputs?: Record<string, unknown>;
    debug?: boolean;
  };
}

export interface EnqueueMicroflowRunResponseDto {
  runId: string;
  resourceId: string;
  status: "queued" | "running" | "success" | "failed" | "cancelled";
  startedAt: string;
}

export type EnqueueMicroflowRunApiResponse = MicroflowApiResponse<EnqueueMicroflowRunResponseDto>;

/**
 * GET /api/v1/microflows/runs/{runId}/status
 */
export interface MicroflowRunStatusResponseDto {
  runId: string;
  resourceId: string;
  status: "queued" | "running" | "success" | "failed" | "cancelled";
  startedAt: string;
  endedAt?: string;
  durationMs: number;
  finalized: boolean;
  errorCode?: string;
  errorMessage?: string;
}

export type GetMicroflowRunStatusApiResponse = MicroflowApiResponse<MicroflowRunStatusResponseDto>;

/**
 * POST /api/v1/microflows/runs/{runId}:retry
 */
export interface RetryMicroflowRunResponseDto {
  previousRunId: string;
  newRunId: string;
  status: "queued" | "running" | "success" | "failed" | "cancelled";
  startedAt: string;
}

export type RetryMicroflowRunApiResponse = MicroflowApiResponse<RetryMicroflowRunResponseDto>;

/**
 * POST /api/v1/microflows/runtime/retention:run
 */
export interface RunRetentionRequestDto {
  retentionDays?: number;
  batchSize?: number;
  dryRun?: boolean;
  resourceId?: string;
}

export interface RunRetentionResultDto {
  cutoffAt: string;
  dryRun: boolean;
  candidateRunCount: number;
  deletedRunCount: number;
  deletedTraceCount: number;
  deletedLogCount: number;
  sampleRunIds: string[];
}

export type RunRetentionApiResponse = MicroflowApiResponse<RunRetentionResultDto>;
