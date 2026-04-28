import type {
  CreateMicroflowInput,
  MicroflowListQuery,
  MicroflowReference,
  MicroflowResource,
  MicroflowRuntimeDto,
  MicroflowAuthoringSchema,
  MicroflowValidationIssue,
  PublishMicroflowPayload
} from "../schema/types";
import type {
  MicroflowRunSession,
  MicroflowRuntimeError,
  MicroflowTestRunOptions,
  MicroflowTraceFrame,
} from "../debug/trace-types";

export interface SaveMicroflowRequest {
  schema: MicroflowAuthoringSchema;
  comment?: string;
}

export interface SaveMicroflowResponse {
  microflowId: string;
  version: string;
  savedAt: string;
  nodeCount: number;
  edgeCount: number;
}

export interface ValidateMicroflowRequest {
  schema: MicroflowAuthoringSchema;
}

export interface ValidateMicroflowResponse {
  valid: boolean;
  issues: MicroflowValidationIssue[];
}

export interface TestRunMicroflowRequest {
  microflowId?: string;
  input: Record<string, unknown>;
  schema?: MicroflowAuthoringSchema;
  schemaId?: string;
  version?: string;
  debug?: boolean;
  correlationId?: string;
  options?: MicroflowTestRunOptions;
}

export interface TestRunMicroflowResponse {
  runId: string;
  status: "succeeded" | "failed" | "unsupported" | "cancelled";
  startedAt: string;
  durationMs: number;
  frames: MicroflowTraceFrame[];
  error?: MicroflowRuntimeError;
  session: MicroflowRunSession;
}

export type MicroflowRunHistoryStatus = "success" | "failed" | "unsupported" | "cancelled";

export interface MicroflowRunHistoryQuery {
  pageIndex?: number;
  pageSize?: number;
  status?: "all" | MicroflowRunHistoryStatus;
}

export interface MicroflowRunHistoryItem {
  runId: string;
  microflowId: string;
  status: MicroflowRunHistoryStatus;
  durationMs: number;
  startedAt: string;
  completedAt?: string;
  errorMessage?: string;
  summary?: string;
}

export interface ListMicroflowRunsResponse {
  items: MicroflowRunHistoryItem[];
  total: number;
}

export interface PublishMicroflowResponse {
  microflowId: string;
  publishedVersion: string;
  publishedAt: string;
  resource?: MicroflowResource;
}

export type { MicroflowRuntimeDto };

export interface MicroflowApiClient {
  listMicroflows(query?: MicroflowListQuery): Promise<MicroflowResource[]>;
  createMicroflow(input: CreateMicroflowInput): Promise<MicroflowResource>;
  getMicroflow(id: string): Promise<MicroflowResource>;
  saveMicroflow(request: SaveMicroflowRequest): Promise<SaveMicroflowResponse>;
  loadMicroflow(id: string): Promise<MicroflowAuthoringSchema>;
  validateMicroflow(request: ValidateMicroflowRequest): Promise<ValidateMicroflowResponse>;
  testRunMicroflow(request: TestRunMicroflowRequest): Promise<TestRunMicroflowResponse>;
  cancelMicroflowRun(runId: string): Promise<{ runId: string; status: "cancelled" | "success" | "failed" } | void>;
  getMicroflowRunSession(runId: string): Promise<MicroflowRunSession>;
  getMicroflowRunTrace(runId: string): Promise<MicroflowTraceFrame[]>;
  listMicroflowRuns(microflowId: string, query?: MicroflowRunHistoryQuery): Promise<ListMicroflowRunsResponse>;
  getMicroflowRunDetail(microflowId: string, runId: string): Promise<MicroflowRunSession>;
  publishMicroflow(id: string, payload?: PublishMicroflowPayload): Promise<PublishMicroflowResponse>;
  duplicateMicroflow(id: string): Promise<MicroflowResource>;
  deleteMicroflow(id: string): Promise<void>;
  archiveMicroflow(id: string): Promise<MicroflowResource>;
  toggleFavorite(id: string, favorite: boolean): Promise<MicroflowResource>;
  getMicroflowReferences(id: string): Promise<MicroflowReference[]>;
  getTrace(runId: string): Promise<MicroflowTraceFrame[]>;
}

export interface MicroflowSaveService {
  saveMicroflow(schema: MicroflowAuthoringSchema): Promise<SaveMicroflowResponse>;
}

export interface MicroflowLoadService {
  loadMicroflow(id: string): Promise<MicroflowAuthoringSchema>;
}

export interface MicroflowValidateService {
  validateMicroflow(schema: MicroflowAuthoringSchema): Promise<ValidateMicroflowResponse>;
}

export interface MicroflowTestRunService {
  testRunMicroflow(id: string, input: Record<string, unknown>, schema?: MicroflowAuthoringSchema): Promise<TestRunMicroflowResponse>;
}

export interface MicroflowPublishService {
  publishMicroflow(id: string): Promise<PublishMicroflowResponse>;
}

export interface MicroflowTraceService {
  getTrace(runId: string): Promise<MicroflowTraceFrame[]>;
}

export type {
  MicroflowRunSession,
  MicroflowRuntimeError,
  MicroflowTestRunOptions,
  MicroflowTraceFrame,
};
