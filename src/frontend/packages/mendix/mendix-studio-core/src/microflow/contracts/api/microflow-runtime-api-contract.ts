import type { MicroflowAuthoringSchema } from "@atlas/microflow";
import type { MicroflowRunSession, MicroflowRuntimeLog, MicroflowTestRunOptions, MicroflowTraceFrame } from "@atlas/microflow";

import type { MicroflowApiResponse } from "./api-envelope";

/**
 * POST /api/microflows/{id}/test-run
 * 若提供 `schema`：以**草稿**执行；未提供时后端使用已保存的 schema/版本（P0 可仅实现草稿运行）。
 */
export interface TestRunMicroflowApiRequest {
  schema?: MicroflowAuthoringSchema;
  input: Record<string, unknown>;
  options?: MicroflowTestRunOptions;
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
