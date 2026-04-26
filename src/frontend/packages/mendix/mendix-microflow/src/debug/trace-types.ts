import type {
  MicroflowAuthoringSchema,
  MicroflowCaseValue,
  MicroflowDataType,
  MicroflowVariableIndex,
} from "../schema";
import type { MicroflowMetadataCatalog } from "../metadata";

export type MicroflowRunStatus =
  | "idle"
  | "validating"
  | "running"
  | "success"
  | "failed"
  | "cancelled";

export type MicroflowTraceFrameStatus =
  | "running"
  | "success"
  | "failed"
  | "skipped";

export interface MicroflowRuntimeVariableValue {
  name: string;
  type: MicroflowDataType;
  valuePreview: string;
  rawValue?: unknown;
  source?: string;
}

export interface MicroflowRuntimeError {
  code: string;
  message: string;
  objectId?: string;
  actionId?: string;
  flowId?: string;
  details?: string;
}

export interface MicroflowRuntimeLog {
  id: string;
  timestamp: string;
  level: "trace" | "debug" | "info" | "warning" | "error" | "critical";
  objectId?: string;
  actionId?: string;
  message: string;
}

export interface MicroflowTraceFrame {
  id: string;
  frameId?: string;
  runId: string;
  objectId: string;
  nodeId?: string;
  objectTitle?: string;
  nodeTitle?: string;
  actionId?: string;
  collectionId?: string;
  incomingFlowId?: string;
  outgoingFlowId?: string;
  incomingEdgeId?: string;
  outgoingEdgeId?: string;
  selectedCaseValue?: MicroflowCaseValue;
  loopIteration?: {
    loopObjectId: string;
    index: number;
    iteratorVariableName?: string;
    iteratorValuePreview?: string;
  };
  status: MicroflowTraceFrameStatus;
  startedAt: string;
  endedAt?: string;
  durationMs: number;
  input?: Record<string, unknown>;
  output?: Record<string, unknown>;
  error?: MicroflowRuntimeError;
  variablesSnapshot?: Record<string, MicroflowRuntimeVariableValue>;
  message?: string;
}

export interface MicroflowVariableSnapshot {
  frameId: string;
  objectId: string;
  variables: MicroflowRuntimeVariableValue[];
}

export interface MicroflowRunSession {
  id: string;
  schemaId: string;
  startedAt: string;
  endedAt?: string;
  status: MicroflowRunStatus;
  input: Record<string, unknown>;
  output?: unknown;
  error?: MicroflowRuntimeError;
  trace: MicroflowTraceFrame[];
  logs: MicroflowRuntimeLog[];
  variables: MicroflowVariableSnapshot[];
}

export interface MicroflowTestRunOptions {
  simulateRestError?: boolean;
  decisionBooleanResult?: boolean;
  enumerationCaseValue?: string;
  objectTypeCase?: string;
  loopIterations?: number;
}

export interface MicroflowTestRunInput {
  parameters: Record<string, unknown>;
  options?: MicroflowTestRunOptions;
}

export interface MockTestRunMicroflowInput {
  schema: MicroflowAuthoringSchema;
  metadata: MicroflowMetadataCatalog;
  variableIndex: MicroflowVariableIndex;
  parameters: Record<string, unknown>;
  options?: MicroflowTestRunOptions;
}
