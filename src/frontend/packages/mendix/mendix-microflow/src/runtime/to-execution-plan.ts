import { toRuntimeDto } from "../adapters/microflow-adapters";
import { collectRuntimeFlows, collectRuntimeObjects, getStartEvent } from "../debug/trace-utils";
import type {
  MicroflowAction,
  MicroflowAnnotationFlow,
  MicroflowAuthoringSchema,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowFlow,
  MicroflowRuntimeDto,
  MicroflowSequenceFlow,
  MicroflowVariableSymbol,
} from "../schema/types";
import { tryMapP0ActionToDiscriminatedDto } from "./map-authoring-p0-runtime";
import { resolveActionRuntimeSupportLevel } from "./runtime-action-support";
import type { MicroflowRuntimeSupportLevel, MicroflowUnsupportedActionReason } from "./runtime-action-support";
import type {
  MicroflowExecutionFlow,
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

function addUniqueRef(refs: MicroflowRuntimeMetadataRefDto[], ref: MicroflowRuntimeMetadataRefDto): void {
  if (!ref.qualifiedName || refs.some(item => item.refKind === ref.refKind && item.qualifiedName === ref.qualifiedName)) {
    return;
  }
  refs.push(ref);
}

function collectRefsFromAction(action: MicroflowAction, refs: MicroflowRuntimeMetadataRefDto[]): void {
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
      addUniqueRef(refs, { refKind: "microflow", qualifiedName: action.targetMicroflowId || "invalid" });
      break;
    default:
      break;
  }
}

function collectMetadataRefsFromObjects(objects: MicroflowObject[], refs: MicroflowRuntimeMetadataRefDto[]): void {
  for (const object of objects) {
    if (object.kind === "actionActivity") {
      collectRefsFromAction(object.action, refs);
    }
    if (object.kind === "loopedActivity") {
      collectMetadataRefsFromObjects(collectRuntimeObjects(object.objectCollection), refs);
    }
  }
}

function mapFlowKind(flow: MicroflowSequenceFlow | MicroflowAnnotationFlow, isErrorHandler: boolean): MicroflowExecutionFlow["edgeKind"] {
  if (flow.kind === "annotation") {
    return "annotation";
  }
  if (isErrorHandler) {
    return "errorHandler";
  }
  return flow.editor.edgeKind === "decisionCondition" || flow.editor.edgeKind === "objectTypeCondition" || flow.editor.edgeKind === "errorHandler"
    ? flow.editor.edgeKind
    : "sequence";
}

function toExecutionFlow(navigation: { objectCollection: MicroflowRuntimeDto["objectCollection"]; flows: MicroflowRuntimeDto["flows"] }, flow: MicroflowSequenceFlow | MicroflowAnnotationFlow): MicroflowExecutionFlow {
  const location = findFlowLocation(navigation, flow.id);
  const edgeKind = mapFlowKind(flow, flow.kind === "sequence" && flow.isErrorHandler);
  const isErrorHandler = flow.kind === "sequence" && flow.isErrorHandler;
  return {
    flowId: flow.id,
    kind: flow.kind,
    edgeKind,
    originObjectId: flow.originObjectId,
    destinationObjectId: flow.destinationObjectId,
    caseValues: flow.caseValues ?? [],
    isErrorHandler,
    originConnectionIndex: flow.originConnectionIndex,
    destinationConnectionIndex: flow.destinationConnectionIndex,
    collectionId: location?.collectionId,
    parentLoopObjectId: location?.parentLoopObjectId,
    branchOrder: flow.kind === "sequence" ? flow.editor.branchOrder : undefined,
    controlFlow: flowControlFlow(edgeKind, isErrorHandler),
    runtimeIgnored: edgeKind === "annotation",
  };
}

function findFlowLocation(
  navigation: { objectCollection: MicroflowObjectCollection; flows: MicroflowFlow[] },
  flowId: string,
): { collectionId: string; parentLoopObjectId?: string } | undefined {
  if (navigation.flows.some(flow => flow.id === flowId)) {
    return { collectionId: navigation.objectCollection.id };
  }
  return findFlowLocationInCollection(navigation.objectCollection, flowId);
}

function findFlowLocationInCollection(
  collection: MicroflowObjectCollection,
  flowId: string,
  parentLoopObjectId?: string,
): { collectionId: string; parentLoopObjectId?: string } | undefined {
  if (collection.flows?.some(flow => flow.id === flowId)) {
    return { collectionId: collection.id, parentLoopObjectId };
  }
  for (const object of collection.objects) {
    if (object.kind === "loopedActivity") {
      const found = findFlowLocationInCollection(object.objectCollection, flowId, object.id);
      if (found) {
        return found;
      }
    }
  }
  return undefined;
}

function buildErrorHandling(object: MicroflowObject, objectId: string) {
  if (object.kind === "actionActivity") {
    return { errorHandlingType: object.action.errorHandlingType, scopeObjectId: objectId };
  }
  if (object.kind === "exclusiveSplit" || object.kind === "inheritanceSplit" || object.kind === "loopedActivity") {
    return { errorHandlingType: object.errorHandlingType, scopeObjectId: objectId };
  }
  return undefined;
}

function addNodesFromCollection(
  collection: MicroflowObjectCollection,
  collectionId: string,
  parentLoopObjectId: string | undefined,
  nodes: MicroflowExecutionNode[],
  unsupported: MicroflowUnsupportedActionDescriptor[],
  loopCollections: MicroflowExecutionLoopCollection[],
): void {
  const collectionNodeIds = collection.objects.map(object => object.id);
  const collectionFlowIds = collection.flows?.map(flow => flow.id) ?? [];
  for (const object of collection.objects) {
    let support: { supportLevel: MicroflowRuntimeSupportLevel; reason?: MicroflowUnsupportedActionReason; message: string } = {
      supportLevel: "supported",
      message: "Supported object.",
    };
    if (object.kind === "actionActivity") {
      support = resolveActionRuntimeSupportLevel(object.action.kind);
      if (support.supportLevel !== "supported" && support.reason) {
        unsupported.push({
          objectId: object.id,
          actionId: object.action.id,
          actionKind: object.action.kind,
          reason: support.reason,
          message: support.message,
          supportLevel: support.supportLevel,
        });
      }
    }
    const node: MicroflowExecutionNode = {
      objectId: object.id,
      actionId: object.kind === "actionActivity" ? object.action.id : undefined,
      kind: object.kind,
      actionKind: object.kind === "actionActivity" ? object.action.kind : undefined,
      officialType: object.officialType,
      caption: "caption" in object ? object.caption : undefined,
      config: { objectKind: object.kind, officialType: object.officialType, caption: "caption" in object ? object.caption : undefined },
      p0ActionRuntime: object.kind === "actionActivity" ? tryMapP0ActionToDiscriminatedDto(object.action) ?? undefined : undefined,
      supportLevel: support.supportLevel,
      runtimeBehavior: nodeRuntimeBehavior(object, support.supportLevel),
      errorHandling: buildErrorHandling(object, object.id),
      collectionId,
      parentLoopObjectId,
    };
    nodes.push(node);
    if (object.kind === "loopedActivity") {
      loopCollections.push({
        loopObjectId: object.id,
        collectionId: object.objectCollection.id,
        startNodeId: object.objectCollection.objects.find(candidate => candidate.kind === "startEvent")?.id ?? object.objectCollection.objects.find(candidate => candidate.kind !== "annotation" && candidate.kind !== "parameterObject")?.id,
        nodeIds: object.objectCollection.objects.map(candidate => candidate.id),
        flowIds: object.objectCollection.flows?.map(flow => flow.id) ?? [],
      });
      addNodesFromCollection(object.objectCollection, object.objectCollection.id, object.id, nodes, unsupported, loopCollections);
    }
  }
  if (parentLoopObjectId) {
    const existing = loopCollections.find(item => item.loopObjectId === parentLoopObjectId && item.collectionId === collectionId);
    if (existing) {
      existing.nodeIds = collectionNodeIds;
      existing.flowIds = collectionFlowIds;
    }
  }
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

export function toExecutionPlan(dto: MicroflowRuntimeDto, options?: { resourceId?: string; version?: string }): MicroflowExecutionPlan {
  const navigation = { flows: dto.flows, objectCollection: dto.objectCollection };
  const allObjects = collectRuntimeObjects(dto.objectCollection);
  const allFlows = collectRuntimeFlows(navigation)
    .filter((flow): flow is MicroflowSequenceFlow => flow.kind === "sequence")
    .map(flow => toExecutionFlow(navigation, flow));
  const normalFlows = allFlows.filter(flow => flow.controlFlow === "normal");
  const decisionFlows = allFlows.filter(flow => flow.controlFlow === "decision" || flow.controlFlow === "objectType");
  const errorHandlerFlows = allFlows.filter(flow => flow.controlFlow === "errorHandler");
  const start = getStartEvent(navigation);
  const endNodeIds = allObjects.filter(object => object.kind === "endEvent").map(object => object.id);
  const parameters: MicroflowExecutionParameter[] = dto.parameters.map(parameter => ({
    id: parameter.id,
    name: parameter.name,
    dataType: parameter.dataType,
    required: parameter.required ?? false,
  }));
  const nodes: MicroflowExecutionNode[] = [];
  const unsupportedActions: MicroflowUnsupportedActionDescriptor[] = [];
  const loopCollections: MicroflowExecutionLoopCollection[] = [];
  addNodesFromCollection(dto.objectCollection, dto.objectCollection.id, undefined, nodes, unsupportedActions, loopCollections);
  const variableSymbols = dto.variables.all ?? [];
  const variableDeclarations = variableSymbols.map(toVariableDeclaration);
  const metadataRefs: MicroflowRuntimeMetadataRefDto[] = [];
  collectMetadataRefsFromObjects(allObjects, metadataRefs);
  return {
    id: `plan-${dto.microflowId}`,
    schemaId: dto.microflowId,
    resourceId: options?.resourceId,
    version: options?.version ?? dto.schemaVersion,
    parameters,
    variableDeclarations,
    actionOutputs: variableDeclarations.filter(item => item.source.kind === "actionOutput" || item.source.kind === "microflowReturn" || item.source.kind === "restResponse" || item.source.kind === "createVariable" || item.source.kind === "modeledOnly"),
    loopVariables: variableDeclarations.filter(item => item.source.kind === "loopIterator" || item.scope.kind === "loop"),
    systemVariables: variableDeclarations.filter(item => item.source.kind === "system"),
    errorContextVariables: variableDeclarations.filter(item => item.source.kind === "errorContext" || item.scope.kind === "errorHandler"),
    variableScopes: toVariableScopes(variableSymbols),
    variableDiagnostics: dto.variables.diagnostics ?? [],
    nodes,
    flows: allFlows,
    normalFlows,
    decisionFlows,
    errorHandlerFlows,
    loopCollections,
    startNodeId: start?.id ?? "missing-start",
    endNodeIds,
    metadataRefs,
    unsupportedActions,
    createdAt: new Date().toISOString(),
  };
}

export function toExecutionPlanFromSchema(schema: MicroflowAuthoringSchema, options?: { resourceId?: string; version?: string }): MicroflowExecutionPlan {
  return toExecutionPlan(toRuntimeDto(schema), { resourceId: options?.resourceId, version: options?.version ?? schema.schemaVersion });
}
