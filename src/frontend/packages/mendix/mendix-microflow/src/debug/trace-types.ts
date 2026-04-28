import type {
  MicroflowAuthoringSchema,
  MicroflowCaseValue,
  MicroflowDataType,
  MicroflowVariableIndex,
} from "../schema";
import type { MicroflowMetadataCatalog } from "../metadata";
import type { MicroflowRuntimeErrorCode } from "./runtime-error-codes";

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
  rawValueJson?: string;
  source?: string;
  readonly?: boolean;
  scopeKind?: string;
}

export interface MicroflowRuntimeError {
  code: MicroflowRuntimeErrorCode;
  message: string;
  objectId?: string;
  actionId?: string;
  flowId?: string;
  microflowId?: string;
  callStack?: string[];
  details?: string;
  cause?: string;
}

export interface MicroflowErrorHandlingSummary {
  totalHandled?: number;
  totalUnhandled?: number;
  maxDepthReached?: number;
}

export interface MicroflowRuntimeLog {
  id: string;
  timestamp: string;
  level: "trace" | "debug" | "info" | "warning" | "error" | "critical";
  objectId?: string;
  actionId?: string;
  message: string;
  logNodeName?: string;
  traceId?: string;
  variablesPreview?: unknown;
  structuredFieldsJson?: string;
}

export interface MicroflowRuntimeTransactionSummary {
  transactionId?: string;
  status: "none" | "active" | "committed" | "rolledBack" | "failed";
  changedObjectCount: number;
  committedObjectCount: number;
  rolledBackObjectCount: number;
  logCount: number;
  diagnosticsCount: number;
}

export interface MicroflowTraceFrame {
  id: string;
  frameId?: string;
  runId: string;
  microflowId?: string;
  parentRunId?: string;
  rootRunId?: string;
  callFrameId?: string;
  callDepth?: number;
  callerObjectId?: string;
  callerActionId?: string;
  objectId: string;
  nodeId?: string;
  nodeName?: string;
  nodeType?: string;
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
  /** 本步是否经 ErrorHandler 控制流连线进入或离开（与 runtime-trace 契约一致） */
  errorHandlerVisited?: boolean;
}

/** Per-frame variable rows attached to a {@link MicroflowRunSession} (not editor variable previews). */
export interface MicroflowRunSessionVariableSnapshot {
  frameId: string;
  objectId: string;
  variables: MicroflowRuntimeVariableValue[];
}

export interface MicroflowRunSession {
  id: string;
  schemaId: string;
  resourceId?: string;
  version?: string;
  parentRunId?: string;
  rootRunId?: string;
  callFrameId?: string;
  callDepth?: number;
  correlationId?: string;
  callStack?: string[];
  startedAt: string;
  endedAt?: string;
  status: MicroflowRunStatus;
  input: Record<string, unknown>;
  output?: unknown;
  error?: MicroflowRuntimeError;
  trace: MicroflowTraceFrame[];
  logs: MicroflowRuntimeLog[];
  variables: MicroflowRunSessionVariableSnapshot[];
  transactionSummary?: MicroflowRuntimeTransactionSummary;
  errorHandlingSummary?: MicroflowErrorHandlingSummary;
  childRuns?: MicroflowRunSession[];
  childRunIds?: string[];
}

export interface MicroflowTestRunOptions {
  simulateRestError?: boolean;
  allowRealHttp?: boolean;
  decisionBooleanResult?: boolean;
  enumerationCaseValue?: string;
  objectTypeCase?: string;
  loopIterations?: number;
  maxSteps?: number;
  disableExpressionEvaluation?: boolean;
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
