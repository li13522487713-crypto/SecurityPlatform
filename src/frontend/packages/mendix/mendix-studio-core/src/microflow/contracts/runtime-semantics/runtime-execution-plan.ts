import type {
  MicroflowCaseValue,
  MicroflowDataType,
  MicroflowDiscriminatedRuntimeP0ActionDto,
  MicroflowVariableDiagnostic,
  MicroflowVariableKind,
  MicroflowVariableScope,
  MicroflowVariableSource,
} from "@atlas/microflow/schema";
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
  variableDeclarations: MicroflowExecutionVariableDeclaration[];
  actionOutputs: MicroflowExecutionVariableDeclaration[];
  loopVariables: MicroflowExecutionVariableDeclaration[];
  systemVariables: MicroflowExecutionVariableDeclaration[];
  errorContextVariables: MicroflowExecutionVariableDeclaration[];
  variableScopes: MicroflowExecutionVariableScope[];
  variableDiagnostics: MicroflowVariableDiagnostic[];
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

export interface MicroflowExecutionVariableDeclaration {
  name: string;
  dataType: MicroflowDataType;
  kind?: MicroflowVariableKind;
  source: MicroflowVariableSource;
  scope: MicroflowVariableScope;
  readonly: boolean;
  objectId?: string;
  actionId?: string;
  flowId?: string;
  loopObjectId?: string;
}

export interface MicroflowExecutionVariableScope {
  key: string;
  scope: MicroflowVariableScope;
  variableNames: string[];
}

export interface MicroflowExecutionNode {
  objectId: string;
  actionId?: string;
  kind: string;
  actionKind?: string;
  officialType: string;
  caption?: string;
  /** 精简快照（无嵌套子集合/FlowGram）；P0 强类型见 p0ActionRuntime。 */
  config: {
    objectKind: string;
    officialType: string;
    caption?: string;
  };
  /** P0 动作的结构化运行期 config；非 P0 为 undefined。 */
  p0ActionRuntime?: MicroflowDiscriminatedRuntimeP0ActionDto;
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
