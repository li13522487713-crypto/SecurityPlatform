import type {
  MicroflowCaseValue,
  MicroflowDataType,
} from "../schema";
import type { MicroflowRuntimeErrorCode } from "./runtime-error-codes";

export type MicroflowRunStatus =
  | "idle"
  | "validating"
  | "running"
  | "success"
  | "failed"
  | "unsupported"
  | "cancelled";

export type MicroflowTraceFrameStatus =
  | "running"
  | "success"
  | "failed"
  | "unsupported"
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

export type MicroflowGatewayBranchTraceStatus = "executed" | "completed" | "failed" | "skipped";

export interface MicroflowGatewayBranchTrace {
  flowId: string;
  branchId: string;
  targetObjectId?: string;
  selected: boolean;
  status: MicroflowGatewayBranchTraceStatus;
}

export function extractGatewayBranchTrace(frame: Pick<MicroflowTraceFrame, "output">): MicroflowGatewayBranchTrace[] {
  const branchTrace = frame.output?.branchTrace;
  if (!Array.isArray(branchTrace)) {
    return [];
  }
  return branchTrace.flatMap(item => {
    if (!item || typeof item !== "object") {
      return [];
    }
    const candidate = item as unknown as Record<string, unknown>;
    const flowId = typeof candidate.flowId === "string" ? candidate.flowId : undefined;
    const branchId = typeof candidate.branchId === "string" ? candidate.branchId : flowId;
    const status = typeof candidate.status === "string" ? candidate.status : undefined;
    if (!flowId || !branchId || !["executed", "completed", "failed", "skipped"].includes(status ?? "")) {
      return [];
    }
    return [{
      flowId,
      branchId,
      targetObjectId: typeof candidate.targetObjectId === "string" ? candidate.targetObjectId : undefined,
      selected: candidate.selected === true,
      status: status as MicroflowGatewayBranchTraceStatus,
    }];
  });
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
  nodeKind?: string;
  actionKind?: string;
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
  inputVariables?: Record<string, MicroflowRuntimeVariableValue>;
  actionInput?: Record<string, unknown>;
  evaluatedExpressions?: unknown[];
  output?: Record<string, unknown> & { branchTrace?: MicroflowGatewayBranchTrace[] };
  outputVariables?: Record<string, MicroflowRuntimeVariableValue>;
  variableDelta?: {
    added?: string[];
    changed?: string[];
    removed?: string[];
  };
  handoffPayload?: Record<string, unknown>;
  transactionEffect?: Record<string, unknown>;
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
  persistedAt?: string;
  finalized?: boolean;
  traceFrameCount?: number;
  hasHydratedTrace?: boolean;
}

export interface MicroflowTestRunOptions {
  /** Backend runtime scenario option. Production Run Panel does not expose this. */
  simulateRestError?: boolean;
  allowRealHttp?: boolean;
  /** Backend runtime scenario option. Production Run Panel does not expose this. */
  decisionBooleanResult?: boolean;
  /** Backend runtime scenario option. Production Run Panel does not expose this. */
  enumerationCaseValue?: string;
  /** Backend runtime scenario option. Production Run Panel does not expose this. */
  objectTypeCase?: string;
  /** Backend runtime scenario option. Production Run Panel does not expose this. */
  loopIterations?: number;
  maxSteps?: number;
  connectorCapabilities?: string[];
  disableExpressionEvaluation?: boolean;
}

export interface MicroflowTestRunInput {
  parameters: Record<string, unknown>;
  options?: MicroflowTestRunOptions;
  sampleId?: string;
}

export interface MicroflowTestRunSample {
  id: string;
  name: string;
  parameters: Record<string, unknown>;
  expectedResult?: unknown;
  lastResult?: unknown;
  lastStatus?: MicroflowRunStatus;
  lastRunId?: string;
  lastRunAt?: string;
  previousResult?: unknown;
  updatedAt: string;
}
