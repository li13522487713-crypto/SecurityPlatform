import type {
  MicroflowCaseValue,
  MicroflowDataType,
  MicroflowDiscriminatedRuntimeP0ActionDto,
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
  objectTypeFlows: MicroflowExecutionFlow[];
  errorHandlerFlows: MicroflowExecutionFlow[];
  ignoredFlows: MicroflowExecutionFlow[];
  loopCollections: MicroflowExecutionLoopCollection[];
  gateways: MicroflowExecutionGateway[];
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
    [key: string]: unknown;
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
  | "loopBody"
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

export type MicroflowExecutionGatewayRole = "split" | "merge" | "splitMerge" | "passThrough";

export interface MicroflowExecutionGateway {
  objectId: string;
  kind: "parallelGateway" | "inclusiveGateway" | string;
  collectionId?: string;
  role: MicroflowExecutionGatewayRole;
  incomingFlowIds: string[];
  outgoingFlowIds: string[];
  branchFlowIds: string[];
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

export function nodeRuntimeBehavior(objectKind: string, supportLevel: MicroflowRuntimeSupportLevel): MicroflowExecutionRuntimeBehavior {
  if (supportLevel !== "supported") {
    return "unsupported";
  }
  if (objectKind === "annotation" || objectKind === "parameterObject") {
    return "ignored";
  }
  if (["endEvent", "errorEvent", "breakEvent", "continueEvent"].includes(objectKind)) {
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
  if (edgeKind === "loopBody") {
    return "ignored";
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
  const nodesById = new Map(plan.nodes.map(node => [node.objectId, node]));
  const flowIds = new Set(plan.flows.map(flow => flow.flowId));
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
    if ((flow.edgeKind === "annotation" || flow.runtimeIgnored) && flow.controlFlow !== "ignored") {
      issues.push({ code: "RUNTIME_IGNORED_FLOW_IN_CONTROL_PLAN", message: "Annotation/ignored flow must be marked as ignored control flow.", flowId: flow.flowId, severity: "error" });
    }
    if (flow.controlFlow === "decision") {
      const source = nodesById.get(flow.originObjectId);
      if (!source || !["exclusiveSplit", "inclusiveGateway"].includes(source.kind)) {
        issues.push({ code: "RUNTIME_DECISION_FLOW_SOURCE_INVALID", message: "Decision flow source must be ExclusiveSplit or InclusiveGateway.", flowId: flow.flowId, objectId: flow.originObjectId, severity: "error" });
      }
    }
  }
  for (const node of plan.nodes) {
    if (node.actionKind && node.supportLevel === "supported" && !node.p0ActionRuntime) {
      issues.push({ code: "RUNTIME_P0_CONFIG_MISSING", message: "Supported action node is missing p0ActionRuntime.", objectId: node.objectId, severity: "error" });
    }
  }
  for (const gateway of plan.gateways ?? []) {
    const node = nodesById.get(gateway.objectId);
    if (!node) {
      issues.push({ code: "RUNTIME_GATEWAY_NODE_NOT_FOUND", message: "ExecutionPlan gateway objectId does not reference a node.", objectId: gateway.objectId, severity: "error" });
      continue;
    }
    if (!["parallelGateway", "inclusiveGateway"].includes(node.kind)) {
      issues.push({ code: "RUNTIME_GATEWAY_KIND_INVALID", message: "ExecutionPlan gateway must reference a Parallel or Inclusive gateway node.", objectId: gateway.objectId, severity: "error" });
    }
    for (const flowId of new Set([...gateway.incomingFlowIds, ...gateway.outgoingFlowIds, ...gateway.branchFlowIds])) {
      if (!flowIds.has(flowId)) {
        issues.push({ code: "RUNTIME_GATEWAY_FLOW_NOT_FOUND", message: "ExecutionPlan gateway references an unknown flow.", objectId: gateway.objectId, flowId, severity: "error" });
      }
    }
    if ((gateway.role === "split" || gateway.role === "splitMerge") && gateway.branchFlowIds.length < 2) {
      issues.push({ code: "RUNTIME_GATEWAY_SPLIT_BRANCH_MISSING", message: "ExecutionPlan gateway split must expose at least two branch flows.", objectId: gateway.objectId, severity: "error" });
    }
    if ((gateway.role === "merge" || gateway.role === "splitMerge") && gateway.incomingFlowIds.length === 0) {
      issues.push({ code: "RUNTIME_GATEWAY_MERGE_INCOMING_MISSING", message: "ExecutionPlan gateway merge must expose incoming flows.", objectId: gateway.objectId, severity: "error" });
    }
  }
  return { valid: issues.every(issue => issue.severity !== "error"), issues };
}
