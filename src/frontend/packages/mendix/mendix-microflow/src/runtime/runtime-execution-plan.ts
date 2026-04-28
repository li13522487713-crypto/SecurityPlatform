import type {
  MicroflowCaseValue,
  MicroflowDataType,
  MicroflowDiscriminatedRuntimeP0ActionDto,
  MicroflowObject,
  MicroflowVariableDiagnostic,
  MicroflowVariableKind,
  MicroflowVariableScope,
  MicroflowVariableSource,
} from "../schema";
import type { MicroflowRuntimeSupportLevel, MicroflowUnsupportedActionReason } from "./runtime-action-support";

export interface MicroflowRuntimeMetadataRefDto {
  refKind: "entity" | "enumeration" | "microflow" | "page" | "workflow" | "association" | "attribute";
  qualifiedName: string;
}

export interface MicroflowRuntimeErrorHandlingDto {
  errorHandlingType: string;
  scopeObjectId: string;
}

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
  normalFlows: MicroflowExecutionFlow[];
  decisionFlows: MicroflowExecutionFlow[];
  errorHandlerFlows: MicroflowExecutionFlow[];
  loopCollections: MicroflowExecutionLoopCollection[];
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

export type MicroflowExecutionRuntimeBehavior = "executable" | "terminal" | "ignored" | "unsupported";

export interface MicroflowExecutionNode {
  objectId: string;
  actionId?: string;
  kind: string;
  actionKind?: string;
  officialType: string;
  caption?: string;
  config: {
    objectKind: string;
    officialType: string;
    caption?: string;
  };
  p0ActionRuntime?: MicroflowDiscriminatedRuntimeP0ActionDto;
  supportLevel: MicroflowRuntimeSupportLevel;
  runtimeBehavior: MicroflowExecutionRuntimeBehavior;
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

export type MicroflowExecutionControlFlow = "normal" | "decision" | "objectType" | "errorHandler" | "ignored";

export interface MicroflowExecutionFlow {
  flowId: string;
  kind: MicroflowExecutionFlowKind;
  edgeKind: MicroflowExecutionFlowEdgeKind;
  originObjectId: string;
  destinationObjectId: string;
  caseValues: MicroflowCaseValue[];
  isErrorHandler: boolean;
  originConnectionIndex?: number;
  destinationConnectionIndex?: number;
  collectionId?: string;
  parentLoopObjectId?: string;
  branchOrder?: number;
  controlFlow: MicroflowExecutionControlFlow;
  runtimeIgnored?: boolean;
}

export interface MicroflowExecutionLoopCollection {
  loopObjectId: string;
  collectionId: string;
  startNodeId?: string;
  nodeIds: string[];
  flowIds: string[];
}

export interface MicroflowUnsupportedActionDescriptor {
  objectId: string;
  actionId: string;
  actionKind: string;
  reason: MicroflowUnsupportedActionReason;
  message: string;
  supportLevel: MicroflowRuntimeSupportLevel;
}

export interface MicroflowExecutionPlanValidationIssue {
  code: string;
  message: string;
  objectId?: string;
  flowId?: string;
  severity: "error" | "warning" | "info";
}

export interface MicroflowExecutionPlanValidationResult {
  valid: boolean;
  issues: MicroflowExecutionPlanValidationIssue[];
}

export function nodeRuntimeBehavior(object: MicroflowObject, supportLevel: MicroflowRuntimeSupportLevel): MicroflowExecutionRuntimeBehavior {
  if (supportLevel !== "supported") {
    return "unsupported";
  }
  if (object.kind === "annotation" || object.kind === "parameterObject") {
    return "ignored";
  }
  if (["endEvent", "errorEvent", "breakEvent", "continueEvent"].includes(object.kind)) {
    return "terminal";
  }
  return "executable";
}

export function flowControlFlow(edgeKind: MicroflowExecutionFlowEdgeKind, isErrorHandler: boolean): MicroflowExecutionControlFlow {
  if (edgeKind === "annotation") {
    return "ignored";
  }
  if (isErrorHandler || edgeKind === "errorHandler") {
    return "errorHandler";
  }
  if (edgeKind === "decisionCondition") {
    return "decision";
  }
  if (edgeKind === "objectTypeCondition") {
    return "objectType";
  }
  return "normal";
}

export function validateExecutionPlan(plan: MicroflowExecutionPlan): MicroflowExecutionPlanValidationResult {
  const issues: MicroflowExecutionPlanValidationIssue[] = [];
  const nodeIds = new Set(plan.nodes.map(node => node.objectId));
  if (!nodeIds.has(plan.startNodeId)) {
    issues.push({ code: "RUNTIME_START_NOT_FOUND", message: "ExecutionPlan startNodeId does not reference a node.", objectId: plan.startNodeId, severity: "error" });
  }
  for (const flow of plan.flows) {
    if (!nodeIds.has(flow.originObjectId)) {
      issues.push({ code: "RUNTIME_FLOW_ORIGIN_NOT_FOUND", message: "Execution flow originObjectId does not reference a node.", flowId: flow.flowId, objectId: flow.originObjectId, severity: "error" });
    }
    if (!nodeIds.has(flow.destinationObjectId)) {
      issues.push({ code: "RUNTIME_FLOW_DESTINATION_NOT_FOUND", message: "Execution flow destinationObjectId does not reference a node.", flowId: flow.flowId, objectId: flow.destinationObjectId, severity: "error" });
    }
    if (flow.edgeKind === "annotation" || flow.runtimeIgnored) {
      issues.push({ code: "RUNTIME_IGNORED_FLOW_IN_CONTROL_PLAN", message: "Annotation/ignored flow should not be present in control-flow plan.", flowId: flow.flowId, severity: "error" });
    }
  }
  for (const node of plan.nodes) {
    if (node.actionKind && node.supportLevel === "supported" && !node.p0ActionRuntime) {
      issues.push({ code: "RUNTIME_P0_CONFIG_MISSING", message: "Supported action node is missing p0ActionRuntime.", objectId: node.objectId, severity: "error" });
    }
  }
  return { valid: issues.every(issue => issue.severity !== "error"), issues };
}
