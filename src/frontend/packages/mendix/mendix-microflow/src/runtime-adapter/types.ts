import type {
  CreateMicroflowInput,
  MicroflowListQuery,
  MicroflowReference,
  MicroflowResource,
  MicroflowRuntimeDto,
  MicroflowSchema,
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
  schema: MicroflowSchema;
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
  schema: MicroflowSchema;
}

export interface ValidateMicroflowResponse {
  valid: boolean;
  issues: MicroflowValidationIssue[];
}

export interface TestRunMicroflowRequest {
  microflowId?: string;
  input: Record<string, unknown>;
  schema: MicroflowSchema;
  options?: MicroflowTestRunOptions;
}

export interface TestRunMicroflowResponse {
  runId: string;
  status: "succeeded" | "failed" | "cancelled";
  startedAt: string;
  durationMs: number;
  frames: MicroflowTraceFrame[];
  error?: MicroflowRuntimeError;
  session: MicroflowRunSession;
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
  loadMicroflow(id: string): Promise<MicroflowSchema>;
  validateMicroflow(request: ValidateMicroflowRequest): Promise<ValidateMicroflowResponse>;
  testRunMicroflow(request: TestRunMicroflowRequest): Promise<TestRunMicroflowResponse>;
  cancelMicroflowRun(runId: string): Promise<void>;
  getMicroflowRunTrace(runId: string): Promise<MicroflowTraceFrame[]>;
  publishMicroflow(id: string, payload?: PublishMicroflowPayload): Promise<PublishMicroflowResponse>;
  duplicateMicroflow(id: string): Promise<MicroflowResource>;
  deleteMicroflow(id: string): Promise<void>;
  archiveMicroflow(id: string): Promise<MicroflowResource>;
  toggleFavorite(id: string, favorite: boolean): Promise<MicroflowResource>;
  getMicroflowReferences(id: string): Promise<MicroflowReference[]>;
  getTrace(runId: string): Promise<MicroflowTraceFrame[]>;
}

export interface MicroflowSaveService {
  saveMicroflow(schema: MicroflowSchema): Promise<SaveMicroflowResponse>;
}

export interface MicroflowLoadService {
  loadMicroflow(id: string): Promise<MicroflowSchema>;
}

export interface MicroflowValidateService {
  validateMicroflow(schema: MicroflowSchema): Promise<ValidateMicroflowResponse>;
}

export interface MicroflowTestRunService {
  testRunMicroflow(id: string, input: Record<string, unknown>, schema?: MicroflowSchema): Promise<TestRunMicroflowResponse>;
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
