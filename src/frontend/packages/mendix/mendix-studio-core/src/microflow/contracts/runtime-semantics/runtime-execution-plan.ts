import type { MicroflowCaseValue, MicroflowDataType } from "@atlas/microflow/schema";
import type { MicroflowRuntimeErrorHandlingDto, MicroflowRuntimeMetadataRefDto } from "../runtime-dto-contract";

/**
 * 后端从 Runtime DTO 或 Authoring 稳定字段编译得到的执行计划；不得包含 FlowGram / 画布 JSON。
 * 与 runtime-execution-plan-contract.md 一致。
 */
export interface MicroflowExecutionPlan {
  id: string;
  schemaId: string;
  resourceId?: string;
  version?: string;
  parameters: MicroflowExecutionParameter[];
  nodes: MicroflowExecutionNode[];
  flows: MicroflowExecutionFlow[];
  startNodeId: string;
  endNodeIds: string[];
  metadataRefs: MicroflowRuntimeMetadataRefDto[];
  unsupportedActions: MicroflowUnsupportedActionDescriptor[];
  createdAt: string;
}

export interface MicroflowExecutionParameter {
  id: string;
  name: string;
  dataType: MicroflowDataType;
  required: boolean;
}

export interface MicroflowExecutionNode {
  objectId: string;
  actionId?: string;
  kind: string;
  actionKind?: string;
  officialType: string;
  caption?: string;
  config: unknown;
  errorHandling?: MicroflowRuntimeErrorHandlingDto;
  collectionId?: string;
  parentLoopObjectId?: string;
}

export type MicroflowExecutionFlowKind = "sequence" | "annotation";

export type MicroflowExecutionFlowEdgeKind =
  | "sequence"
  | "decisionCondition"
  | "objectTypeCondition"
  | "errorHandler"
  | "annotation";

export interface MicroflowExecutionFlow {
  flowId: string;
  kind: MicroflowExecutionFlowKind;
  edgeKind: MicroflowExecutionFlowEdgeKind;
  originObjectId: string;
  destinationObjectId: string;
  caseValues: MicroflowCaseValue[];
  isErrorHandler: boolean;
}

export type MicroflowUnsupportedActionReason =
  | "unsupported"
  | "modeledOnly"
  | "requiresConnector"
  | "deprecated"
  | "nanoflowOnly"
  | "notImplemented";

export interface MicroflowUnsupportedActionDescriptor {
  objectId: string;
  actionId: string;
  actionKind: string;
  reason: MicroflowUnsupportedActionReason;
  message: string;
  supportLevel: MicroflowRuntimeSupportLevel;
}

export type MicroflowRuntimeSupportLevel =
  | "supported"
  | "modeledOnly"
  | "unsupported"
  | "requiresConnector"
  | "nanoflowOnly"
  | "deprecated";

export type { MicroflowObject };
