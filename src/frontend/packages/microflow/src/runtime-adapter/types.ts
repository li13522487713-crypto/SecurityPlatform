import type {
  MicroflowRuntimeEdgeDto,
  MicroflowRuntimeNodeDto,
  MicroflowSchema,
  MicroflowValidationIssue
} from "../schema/types";

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
  microflowId: string;
  input: Record<string, unknown>;
  schema?: MicroflowSchema;
}

export interface MicroflowRuntimeError {
  code: string;
  message: string;
  nodeId?: string;
  details?: Record<string, unknown>;
}

export interface MicroflowTraceFrame {
  frameId: string;
  runId: string;
  nodeId: string;
  nodeTitle: string;
  startedAt: string;
  durationMs: number;
  input: Record<string, unknown>;
  output: Record<string, unknown>;
  error?: MicroflowRuntimeError;
}

export interface TestRunMicroflowResponse {
  runId: string;
  status: "succeeded" | "failed";
  startedAt: string;
  durationMs: number;
  frames: MicroflowTraceFrame[];
  error?: MicroflowRuntimeError;
}

export interface PublishMicroflowResponse {
  microflowId: string;
  publishedVersion: string;
  publishedAt: string;
}

export interface MicroflowRuntimeDto {
  microflowId: string;
  version: string;
  nodes: MicroflowRuntimeNodeDto[];
  edges: MicroflowRuntimeEdgeDto[];
}

export interface MicroflowApiClient {
  saveMicroflow(request: SaveMicroflowRequest): Promise<SaveMicroflowResponse>;
  loadMicroflow(id: string): Promise<MicroflowSchema>;
  validateMicroflow(request: ValidateMicroflowRequest): Promise<ValidateMicroflowResponse>;
  testRunMicroflow(request: TestRunMicroflowRequest): Promise<TestRunMicroflowResponse>;
  publishMicroflow(id: string): Promise<PublishMicroflowResponse>;
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
