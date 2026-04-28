import type { MicroflowRuntimeError, MicroflowRuntimeVariableValue } from "@atlas/microflow/debug";
import type { MicroflowRuntimeTransactionContext } from "./runtime-transaction-types";

/** 与 runtime-semantics-contract.md 中「执行总模型」一致。 */
export interface MicroflowRuntimeExecutionContext {
  runId: string;
  resourceId: string;
  schemaId: string;
  version?: string;
  mode: "testRun" | "publishedRun" | "previewRun";
  input: Record<string, unknown>;
  currentObjectId?: string;
  currentFlowId?: string;
  variables: Record<string, MicroflowRuntimeVariableValue>;
  callStack: MicroflowRuntimeCallFrame[];
  loopStack: MicroflowRuntimeLoopFrame[];
  errorStack: MicroflowRuntimeErrorFrame[];
  transaction: MicroflowRuntimeTransactionContext;
  security: MicroflowRuntimeSecurityContext;
  metadataVersion?: string;
  startedAt: string;
}

export interface MicroflowRuntimeCallFrame {
  microflowId: string;
  objectId?: string;
  actionId?: string;
  parentRunId?: string;
}

export interface MicroflowRuntimeLoopFrame {
  loopObjectId: string;
  iteratorVariableName?: string;
  currentIndex: number;
  currentItem?: unknown;
}

export interface MicroflowRuntimeErrorFrame {
  sourceObjectId: string;
  sourceActionId?: string;
  error: MicroflowRuntimeError;
  latestHttpResponse?: unknown;
  latestSoapFault?: unknown;
}

export interface MicroflowRuntimeSecurityContext {
  userId?: string;
  userName?: string;
  roles: string[];
  workspaceId?: string;
  tenantId?: string;
  applyEntityAccess: boolean;
}
