import type {
  MicroflowAction,
  MicroflowCaseValue,
  MicroflowDataType,
  MicroflowDesignSchema,
  MicroflowGlobalVariable,
  MicroflowTypeRef,
  MicroflowVariable,
  MicroflowVariableSymbol,
  MicroflowWorkflowEdgeJSON,
  MicroflowWorkflowNodeJSON,
} from "../schema/types";
import type { FlowGramMicroflowEdgeData, FlowGramMicroflowNodeData } from "../flowgram/FlowGramMicroflowTypes";
import { toRuntimeP0ActionPayload } from "../node-registry/action-registry";
import { resolveActionRuntimeSupportLevel } from "./runtime-action-support";
import type { MicroflowRuntimeSupportLevel, MicroflowUnsupportedActionReason } from "./runtime-action-support";
import type {
  MicroflowExecutionFlow,
  MicroflowExecutionGateway,
  MicroflowExecutionLoopCollection,
  MicroflowExecutionNode,
  MicroflowExecutionParameter,
  MicroflowExecutionPlan,
  MicroflowExecutionVariableDeclaration,
  MicroflowExecutionVariableScope,
  MicroflowRuntimeMetadataRefDto,
  MicroflowUnsupportedActionDescriptor,
} from "./runtime-execution-plan";
import { flowControlFlow, nodeRuntimeBehavior } from "./runtime-execution-plan";

const DESIGN_SCHEMA_VERSION = "flowgram.microflow.v1";
const ROOT_COLLECTION_ID = "root-collection";

function assertDesignSchema(schema: MicroflowDesignSchema): void {
  if (schema.schemaVersion !== DESIGN_SCHEMA_VERSION || !Array.isArray(schema.workflow?.nodes) || !Array.isArray(schema.workflow?.edges)) {
    throw new Error("Microflow runtime plan only accepts flowgram.microflow.v1 design schema.");
  }
}

function nodeData(node: MicroflowWorkflowNodeJSON): Partial<FlowGramMicroflowNodeData> & Record<string, unknown> {
  return (node.data ?? {}) as Partial<FlowGramMicroflowNodeData> & Record<string, unknown>;
}

function edgeData(edge: MicroflowWorkflowEdgeJSON): Partial<FlowGramMicroflowEdgeData> & Record<string, unknown> {
  return (edge.data ?? {}) as Partial<FlowGramMicroflowEdgeData> & Record<string, unknown>;
}

function readString(value: unknown): string | undefined {
  return typeof value === "string" && value.trim().length > 0 ? value : undefined;
}

function readNumber(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

function nodeObjectId(node: MicroflowWorkflowNodeJSON): string {
  return readString(nodeData(node).objectId) ?? node.id;
}

function nodeKind(node: MicroflowWorkflowNodeJSON): string {
  return readString(nodeData(node).objectKind) ?? node.type;
}

function nodeOfficialType(node: MicroflowWorkflowNodeJSON): string {
  return readString(nodeData(node).officialType) ?? node.type;
}

function nodeCaption(node: MicroflowWorkflowNodeJSON): string | undefined {
  return readString(nodeData(node).title) ?? readString(nodeData(node).caption) ?? readString(nodeData(node).label);
}

function nodeCollectionId(node: MicroflowWorkflowNodeJSON): string {
  return readString(nodeData(node).collectionId) ?? readString(node.meta?.collectionId) ?? ROOT_COLLECTION_ID;
}

function nodeParentObjectId(node: MicroflowWorkflowNodeJSON): string | undefined {
  return readString(nodeData(node).parentObjectId) ?? readString(node.meta?.parentObjectId);
}

function nodeAction(node: MicroflowWorkflowNodeJSON): MicroflowAction | undefined {
  const action = nodeData(node).action;
  return action && typeof action === "object" ? action as MicroflowAction : undefined;
}

function nodeActionKind(node: MicroflowWorkflowNodeJSON): string | undefined {
  return readString(nodeData(node).actionKind) ?? nodeAction(node)?.kind;
}

function edgeFlowId(edge: MicroflowWorkflowEdgeJSON): string {
  return readString(edgeData(edge).flowId) ?? readString(edge.id) ?? `${edge.sourceNodeID}-${edge.targetNodeID}`;
}

function edgeKind(edge: MicroflowWorkflowEdgeJSON): MicroflowExecutionFlow["edgeKind"] {
  const raw = readString(edgeData(edge).edgeKind);
  if (raw === "decisionCondition" || raw === "objectTypeCondition" || raw === "loopBody" || raw === "errorHandler" || raw === "annotation") {
    return raw;
  }
  return "sequence";
}

function flowKind(edge: MicroflowWorkflowEdgeJSON): MicroflowExecutionFlow["kind"] {
  return readString(edgeData(edge).flowKind) === "annotation" || edgeKind(edge) === "annotation" ? "annotation" : "sequence";
}

function edgeCollectionId(edge: MicroflowWorkflowEdgeJSON, nodesById: Map<string, MicroflowWorkflowNodeJSON>): string | undefined {
  return readString(edgeData(edge).collectionId) ?? (nodesById.get(edge.sourceNodeID) ? nodeCollectionId(nodesById.get(edge.sourceNodeID)!) : undefined);
}

function edgeParentLoopObjectId(edge: MicroflowWorkflowEdgeJSON, nodesById: Map<string, MicroflowWorkflowNodeJSON>): string | undefined {
  return readString(edgeData(edge).parentLoopObjectId) ?? (nodesById.get(edge.sourceNodeID) ? nodeParentObjectId(nodesById.get(edge.sourceNodeID)!) : undefined);
}

function toDataType(type?: MicroflowTypeRef): MicroflowDataType {
  if (!type) {
    return { kind: "unknown", reason: "missing type" };
  }
  if (type.kind === "primitive") {
    const name = type.name.toLowerCase();
    if (name === "boolean") return { kind: "boolean" };
    if (name === "integer") return { kind: "integer" };
    if (name === "long") return { kind: "long" };
    if (name === "decimal") return { kind: "decimal" };
    if (name === "datetime" || name === "dateTime".toLowerCase()) return { kind: "dateTime" };
    if (name === "string") return { kind: "string" };
    return { kind: "unknown", reason: `Unsupported primitive type ${type.name}.` };
  }
  if (type.kind === "entity" || type.kind === "object") {
    return { kind: "object", entityQualifiedName: type.entity ?? type.name };
  }
  if (type.kind === "list") {
    return { kind: "list", itemType: toDataType(type.itemType) };
  }
  if (type.kind === "void") {
    return { kind: "void" };
  }
  return { kind: "unknown", reason: type.name };
}

function addUniqueRef(refs: MicroflowRuntimeMetadataRefDto[], ref: MicroflowRuntimeMetadataRefDto): void {
  if (!ref.qualifiedName || refs.some(item => item.refKind === ref.refKind && item.qualifiedName === ref.qualifiedName)) {
    return;
  }
  refs.push(ref);
}

function collectRefsFromAction(action: MicroflowAction | undefined, refs: MicroflowRuntimeMetadataRefDto[]): void {
  if (!action) {
    return;
  }
  switch (action.kind) {
    case "retrieve":
      if (action.retrieveSource.kind === "database" && action.retrieveSource.entityQualifiedName) {
        addUniqueRef(refs, { refKind: "entity", qualifiedName: action.retrieveSource.entityQualifiedName });
      }
      if (action.retrieveSource.kind === "association" && action.retrieveSource.associationQualifiedName) {
        addUniqueRef(refs, { refKind: "association", qualifiedName: action.retrieveSource.associationQualifiedName });
      }
      break;
    case "createObject":
      addUniqueRef(refs, { refKind: "entity", qualifiedName: action.entityQualifiedName });
      break;
    case "changeMembers":
      action.memberChanges.forEach(change => change.memberQualifiedName && addUniqueRef(refs, { refKind: "attribute", qualifiedName: change.memberQualifiedName }));
      break;
    case "callMicroflow":
      addUniqueRef(refs, { refKind: "microflow", qualifiedName: action.targetMicroflowId || action.targetMicroflowQualifiedName || "invalid" });
      break;
    default:
      break;
  }
}

function buildErrorHandling(node: MicroflowWorkflowNodeJSON) {
  const data = nodeData(node);
  const action = nodeAction(node);
  const errorHandlingType = action?.errorHandlingType ?? readString(data.errorHandlingType);
  return errorHandlingType ? { errorHandlingType, scopeObjectId: nodeObjectId(node) } : undefined;
}

function toExecutionNode(node: MicroflowWorkflowNodeJSON, unsupported: MicroflowUnsupportedActionDescriptor[], refs: MicroflowRuntimeMetadataRefDto[]): MicroflowExecutionNode {
  const action = nodeAction(node);
  const actionKind = nodeActionKind(node);
  const support = actionKind
    ? resolveActionRuntimeSupportLevel(actionKind as Parameters<typeof resolveActionRuntimeSupportLevel>[0])
    : { supportLevel: "supported" as MicroflowRuntimeSupportLevel, message: "Supported object." };
  if (action && support.supportLevel !== "supported" && support.reason) {
    unsupported.push({
      objectId: nodeObjectId(node),
      actionId: action.id,
      actionKind: action.kind,
      reason: support.reason as MicroflowUnsupportedActionReason,
      message: support.message,
      supportLevel: support.supportLevel,
    });
  }
  collectRefsFromAction(action, refs);
  const kind = nodeKind(node);
  return {
    objectId: nodeObjectId(node),
    actionId: action?.id,
    kind,
    actionKind,
    officialType: nodeOfficialType(node),
    caption: nodeCaption(node),
    config: {
      ...nodeData(node),
      objectKind: kind,
      officialType: nodeOfficialType(node),
      caption: nodeCaption(node),
    },
    p0ActionRuntime: action ? toRuntimeP0ActionPayload(action) ?? undefined : undefined,
    supportLevel: support.supportLevel,
    runtimeBehavior: nodeRuntimeBehavior(kind, support.supportLevel),
    errorHandling: buildErrorHandling(node),
    collectionId: nodeCollectionId(node),
    parentLoopObjectId: nodeParentObjectId(node),
  };
}

function toExecutionFlow(edge: MicroflowWorkflowEdgeJSON, nodesById: Map<string, MicroflowWorkflowNodeJSON>): MicroflowExecutionFlow {
  const kind = edgeKind(edge);
  const isErrorHandler = edgeData(edge).isErrorHandler === true || kind === "errorHandler";
  return {
    flowId: edgeFlowId(edge),
    kind: flowKind(edge),
    edgeKind: kind,
    originObjectId: edge.sourceNodeID,
    destinationObjectId: edge.targetNodeID,
    caseValues: Array.isArray(edgeData(edge).caseValues) ? edgeData(edge).caseValues as MicroflowCaseValue[] : [],
    isErrorHandler,
    originConnectionIndex: readNumber(edgeData(edge).originConnectionIndex),
    destinationConnectionIndex: readNumber(edgeData(edge).destinationConnectionIndex),
    collectionId: edgeCollectionId(edge, nodesById),
    parentLoopObjectId: edgeParentLoopObjectId(edge, nodesById),
    branchOrder: readNumber(edgeData(edge).branchOrder),
    controlFlow: flowControlFlow(kind, isErrorHandler),
    runtimeIgnored: kind === "annotation",
  };
}

function toParameter(parameter: MicroflowDesignSchema["parameters"][number]): MicroflowExecutionParameter {
  return {
    id: parameter.id,
    name: parameter.name,
    dataType: parameter.dataType,
    required: parameter.required ?? false,
  };
}

function toVariableSymbol(variable: MicroflowVariable): MicroflowVariableSymbol {
  const scope = { kind: variable.scope === "node" ? "collection" : "global", collectionId: ROOT_COLLECTION_ID } as const;
  const isErrorContextScope = variable.scope === "latestError" || variable.scope === "errorContext";
  const errorVariable = variable.name === "$latestHttpResponse" || variable.name === "$latestSoapFault" || variable.name === "$latestError"
    ? variable.name
    : "$latestError";
  return {
    id: variable.id,
    name: variable.name,
    displayName: variable.name,
    kind: isErrorContextScope ? "errorContext" : "localVariable",
    dataType: toDataType(variable.type),
    type: variable.type,
    source: isErrorContextScope ? { kind: "errorContext", flowId: "", errorVariable } : { kind: "modeledOnly", objectId: variable.id },
    scope,
    readonly: false,
  };
}

function parameterSymbol(parameter: MicroflowDesignSchema["parameters"][number]): MicroflowVariableSymbol {
  return {
    id: parameter.id,
    name: parameter.name,
    displayName: parameter.name,
    kind: "parameter",
    dataType: parameter.dataType,
    type: parameter.type,
    source: { kind: "parameter", parameterId: parameter.id },
    scope: { kind: "global", collectionId: ROOT_COLLECTION_ID },
    readonly: true,
  };
}

function globalVariableSymbol(variable: MicroflowGlobalVariable): MicroflowVariableSymbol {
  return {
    id: variable.id,
    name: variable.name,
    displayName: variable.name,
    kind: "globalVariable",
    dataType: variable.dataType,
    source: { kind: "globalVariable", variableId: variable.id },
    scope: { kind: "global", collectionId: ROOT_COLLECTION_ID },
    readonly: false,
    documentation: variable.description,
  };
}

function sourceObjectId(symbol: MicroflowVariableSymbol): string | undefined {
  return "objectId" in symbol.source ? symbol.source.objectId : symbol.source.kind === "errorContext" ? symbol.source.sourceObjectId : undefined;
}

function sourceActionId(symbol: MicroflowVariableSymbol): string | undefined {
  return "actionId" in symbol.source ? symbol.source.actionId : undefined;
}

function sourceFlowId(symbol: MicroflowVariableSymbol): string | undefined {
  return symbol.source.kind === "errorContext" ? symbol.source.flowId : undefined;
}

function toVariableDeclaration(symbol: MicroflowVariableSymbol): MicroflowExecutionVariableDeclaration {
  return {
    name: symbol.name,
    dataType: symbol.dataType,
    kind: symbol.kind,
    source: symbol.source,
    scope: symbol.scope,
    readonly: symbol.readonly,
    objectId: sourceObjectId(symbol),
    actionId: sourceActionId(symbol),
    flowId: sourceFlowId(symbol),
    loopObjectId: symbol.scope.loopObjectId,
  };
}

function scopeKey(scope: MicroflowVariableSymbol["scope"]): string {
  return [scope.kind ?? "collection", scope.collectionId, scope.startObjectId, scope.loopObjectId, scope.errorHandlerFlowId, scope.branchFlowId].filter(Boolean).join(":");
}

function toVariableScopes(symbols: MicroflowVariableSymbol[]): MicroflowExecutionVariableScope[] {
  const scopes = new Map<string, MicroflowExecutionVariableScope>();
  for (const symbol of symbols) {
    const key = scopeKey(symbol.scope);
    const current = scopes.get(key) ?? { key, scope: symbol.scope, variableNames: [] };
    current.variableNames.push(symbol.name);
    scopes.set(key, current);
  }
  return [...scopes.values()];
}

function toLoopCollections(nodes: MicroflowWorkflowNodeJSON[], flows: MicroflowExecutionFlow[]): MicroflowExecutionLoopCollection[] {
  return nodes
    .filter(node => nodeKind(node) === "loopedActivity")
    .map(node => {
      const loopObjectId = nodeObjectId(node);
      const collectionId = readString(nodeData(node).bodyCollectionId) ?? `${loopObjectId}-body`;
      const childNodes = nodes.filter(candidate => nodeParentObjectId(candidate) === loopObjectId || nodeCollectionId(candidate) === collectionId);
      const childNodeIds = childNodes.map(nodeObjectId);
      return {
        loopObjectId,
        collectionId,
        startNodeId: childNodes.find(candidate => nodeKind(candidate) === "startEvent")?.id ?? childNodeIds[0],
        nodeIds: childNodeIds,
        flowIds: flows.filter(flow => flow.collectionId === collectionId || flow.parentLoopObjectId === loopObjectId).map(flow => flow.flowId),
      };
    });
}

function gatewayRole(incomingCount: number, outgoingCount: number): MicroflowExecutionGateway["role"] {
  const isSplit = outgoingCount > 1;
  const isMerge = incomingCount > 1;
  if (isSplit && isMerge) {
    return "splitMerge";
  }
  if (isSplit) {
    return "split";
  }
  if (isMerge) {
    return "merge";
  }
  return "passThrough";
}

function toGateways(nodes: MicroflowExecutionNode[], flows: MicroflowExecutionFlow[]): MicroflowExecutionGateway[] {
  return nodes
    .filter(node => node.kind === "parallelGateway" || node.kind === "inclusiveGateway")
    .map(node => {
      const incoming = flows
        .filter(flow => flow.destinationObjectId === node.objectId)
        .sort((left, right) => left.flowId.localeCompare(right.flowId));
      const outgoing = flows
        .filter(flow => flow.originObjectId === node.objectId)
        .sort((left, right) => (left.branchOrder ?? Number.MAX_SAFE_INTEGER) - (right.branchOrder ?? Number.MAX_SAFE_INTEGER) || left.flowId.localeCompare(right.flowId));
      return {
        objectId: node.objectId,
        kind: node.kind,
        collectionId: node.collectionId,
        role: gatewayRole(incoming.length, outgoing.length),
        incomingFlowIds: incoming.map(flow => flow.flowId),
        outgoingFlowIds: outgoing.map(flow => flow.flowId),
        branchFlowIds: outgoing.map(flow => flow.flowId),
      };
    });
}

export function toExecutionPlan(schema: MicroflowDesignSchema, options?: { resourceId?: string; version?: string }): MicroflowExecutionPlan {
  assertDesignSchema(schema);
  const nodesById = new Map(schema.workflow.nodes.map(node => [node.id, node]));
  const unsupportedActions: MicroflowUnsupportedActionDescriptor[] = [];
  const metadataRefs: MicroflowRuntimeMetadataRefDto[] = [];
  const nodes = schema.workflow.nodes.map(node => toExecutionNode(node, unsupportedActions, metadataRefs));
  const flows = schema.workflow.edges
    .map(edge => toExecutionFlow(edge, nodesById));
  const normalFlows = flows.filter(flow => flow.controlFlow === "normal");
  const decisionFlows = flows.filter(flow => flow.controlFlow === "decision");
  const objectTypeFlows = flows.filter(flow => flow.controlFlow === "objectType");
  const errorHandlerFlows = flows.filter(flow => flow.controlFlow === "errorHandler");
  const ignoredFlows = flows.filter(flow => flow.controlFlow === "ignored");
  const start = schema.workflow.nodes.find(node => nodeKind(node) === "startEvent")
    ?? schema.workflow.nodes.find(node => !["annotation", "parameterObject"].includes(nodeKind(node)));
  const endNodeIds = schema.workflow.nodes.filter(node => nodeKind(node) === "endEvent").map(nodeObjectId);
  const variableSymbols = [
    ...schema.parameters.map(parameterSymbol),
    ...(schema.globalVariables ?? []).map(globalVariableSymbol),
    ...(schema.variables ?? []).map(toVariableSymbol),
  ];
  const variableDeclarations = variableSymbols.map(toVariableDeclaration);
  return {
    id: `plan-${schema.id}`,
    schemaId: schema.id,
    resourceId: options?.resourceId,
    version: options?.version ?? schema.schemaVersion,
    parameters: schema.parameters.map(toParameter),
    variableDeclarations,
    actionOutputs: variableDeclarations.filter(item => item.source.kind === "actionOutput" || item.source.kind === "microflowReturn" || item.source.kind === "restResponse" || item.source.kind === "createVariable" || item.source.kind === "modeledOnly"),
    loopVariables: variableDeclarations.filter(item => item.source.kind === "loopIterator" || item.scope.kind === "loop"),
    systemVariables: variableDeclarations.filter(item => item.source.kind === "system"),
    errorContextVariables: variableDeclarations.filter(item => item.source.kind === "errorContext" || item.scope.kind === "errorHandler"),
    variableScopes: toVariableScopes(variableSymbols),
    variableDiagnostics: [],
    nodes,
    flows,
    normalFlows,
    decisionFlows,
    objectTypeFlows,
    errorHandlerFlows,
    ignoredFlows,
    loopCollections: toLoopCollections(schema.workflow.nodes, flows),
    gateways: toGateways(nodes, flows),
    startNodeId: start ? nodeObjectId(start) : "missing-start",
    endNodeIds,
    metadataRefs,
    unsupportedActions,
    createdAt: new Date().toISOString(),
  };
}

export function toExecutionPlanFromSchema(schema: MicroflowDesignSchema, options?: { resourceId?: string; version?: string }): MicroflowExecutionPlan {
  return toExecutionPlan(schema, options);
}
