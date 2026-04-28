import type {
  MicroflowFlow,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowSchema,
  MicroflowSequenceFlow,
  MicroflowVariableGraphAnalysis,
} from "../schema/types";
import { collectFlowsRecursive, findObjectWithCollection, getObjectCollectionById } from "../schema/utils/object-utils";

export interface MicroflowGraphEdge {
  flowId: string;
  fromObjectId: string;
  toObjectId: string;
  collectionId: string;
  edgeKind: "sequence" | "decisionCondition" | "objectTypeCondition" | "loopEntry";
}

export interface MicroflowGraph {
  collectionId: string;
  objects: MicroflowObject[];
  flows: MicroflowFlow[];
  normalEdges: MicroflowGraphEdge[];
  errorHandlerEdges: Array<{ flowId: string; fromObjectId: string; toObjectId: string; collectionId: string }>;
  annotationFlowIds: string[];
  startObjectIds: string[];
}

function collectionFlows(schema: MicroflowSchema, collection: MicroflowObjectCollection): MicroflowFlow[] {
  return collection.id === schema.objectCollection.id ? schema.flows : (collection.flows ?? []);
}

function collectCollections(
  collection: MicroflowObjectCollection,
  parentLoopObjectId?: string,
): Array<{ collection: MicroflowObjectCollection; parentLoopObjectId?: string }> {
  return [
    { collection, parentLoopObjectId },
    ...collection.objects.flatMap(object => object.kind === "loopedActivity"
      ? collectCollections(object.objectCollection, object.id)
      : []),
  ];
}

function isDecisionFlow(flow: MicroflowSequenceFlow): boolean {
  return flow.editor.edgeKind === "decisionCondition" || flow.editor.edgeKind === "objectTypeCondition";
}

function toNormalEdge(flow: MicroflowSequenceFlow, collectionId: string): MicroflowGraphEdge | null {
  if (flow.isErrorHandler) {
    return null;
  }
  return {
    flowId: flow.id,
    fromObjectId: flow.originObjectId,
    toObjectId: flow.destinationObjectId,
    collectionId,
    edgeKind: flow.editor.edgeKind === "decisionCondition" || flow.editor.edgeKind === "objectTypeCondition"
      ? flow.editor.edgeKind
      : "sequence",
  };
}

function loopEntryEdges(collection: MicroflowObjectCollection, collectionId: string): MicroflowGraphEdge[] {
  return collection.objects
    .filter(object => object.kind === "loopedActivity")
    .flatMap(object => object.objectCollection.objects
      .filter(child => child.kind === "startEvent")
      .map(child => ({
        flowId: `loop-entry:${object.id}:${child.id}`,
        fromObjectId: object.id,
        toObjectId: child.id,
        collectionId,
        edgeKind: "loopEntry" as const,
      })));
}

export function buildMicroflowGraph(schema: MicroflowSchema, collectionId = schema.objectCollection.id): MicroflowGraph {
  const collection = getObjectCollectionById(schema, collectionId) ?? schema.objectCollection;
  const flows = collectionFlows(schema, collection);
  const normalEdges = flows
    .filter((flow): flow is MicroflowSequenceFlow => flow.kind === "sequence")
    .map(flow => toNormalEdge(flow, collection.id))
    .filter((edge): edge is MicroflowGraphEdge => Boolean(edge))
    .concat(loopEntryEdges(collection, collection.id));
  return {
    collectionId: collection.id,
    objects: collection.objects,
    flows,
    normalEdges,
    errorHandlerEdges: flows
      .filter((flow): flow is MicroflowSequenceFlow => flow.kind === "sequence" && flow.isErrorHandler)
      .map(flow => ({ flowId: flow.id, fromObjectId: flow.originObjectId, toObjectId: flow.destinationObjectId, collectionId: collection.id })),
    annotationFlowIds: flows.filter(flow => flow.kind === "annotation").map(flow => flow.id),
    startObjectIds: collection.objects.filter(object => object.kind === "startEvent").map(object => object.id),
  };
}

export function buildVariableGraphAnalysis(schema: MicroflowSchema): MicroflowVariableGraphAnalysis {
  const collections = collectCollections(schema.objectCollection);
  const graphs = collections.map(item => buildMicroflowGraph(schema, item.collection.id));
  return {
    collectionIds: collections.map(item => item.collection.id),
    objectIds: collections.flatMap(item => item.collection.objects.map(object => object.id)),
    normalEdges: graphs.flatMap(graph => graph.normalEdges),
    errorHandlerEdges: graphs.flatMap(graph => graph.errorHandlerEdges),
    annotationFlowIds: graphs.flatMap(graph => graph.annotationFlowIds),
    startObjectIdsByCollection: Object.fromEntries(graphs.map(graph => [graph.collectionId, graph.startObjectIds])),
  };
}

export function getObjectSuccessors(schema: MicroflowSchema, objectId: string, collectionId?: string): string[] {
  const graph = buildMicroflowGraph(schema, collectionId ?? findContainingCollection(schema, objectId)?.id);
  return graph.normalEdges.filter(edge => edge.fromObjectId === objectId).map(edge => edge.toObjectId);
}

export function getObjectPredecessors(schema: MicroflowSchema, objectId: string, collectionId?: string): string[] {
  const graph = buildMicroflowGraph(schema, collectionId ?? findContainingCollection(schema, objectId)?.id);
  return graph.normalEdges.filter(edge => edge.toObjectId === objectId).map(edge => edge.fromObjectId);
}

export function getOutgoingNormalFlows(schema: MicroflowSchema, objectId: string, collectionId?: string): MicroflowSequenceFlow[] {
  return buildMicroflowGraph(schema, collectionId ?? findContainingCollection(schema, objectId)?.id).flows
    .filter((flow): flow is MicroflowSequenceFlow => flow.kind === "sequence" && !flow.isErrorHandler && flow.originObjectId === objectId && !isDecisionFlow(flow));
}

export function getOutgoingDecisionFlows(schema: MicroflowSchema, objectId: string, collectionId?: string): MicroflowSequenceFlow[] {
  return buildMicroflowGraph(schema, collectionId ?? findContainingCollection(schema, objectId)?.id).flows
    .filter((flow): flow is MicroflowSequenceFlow => flow.kind === "sequence" && !flow.isErrorHandler && flow.originObjectId === objectId && isDecisionFlow(flow));
}

export function getOutgoingErrorHandlerFlows(schema: MicroflowSchema, objectId: string, collectionId?: string): MicroflowSequenceFlow[] {
  return buildMicroflowGraph(schema, collectionId ?? findContainingCollection(schema, objectId)?.id).flows
    .filter((flow): flow is MicroflowSequenceFlow => flow.kind === "sequence" && flow.isErrorHandler && flow.originObjectId === objectId);
}

export function getIncomingFlows(schema: MicroflowSchema, objectId: string, collectionId?: string): MicroflowSequenceFlow[] {
  return buildMicroflowGraph(schema, collectionId ?? findContainingCollection(schema, objectId)?.id).flows
    .filter((flow): flow is MicroflowSequenceFlow => flow.kind === "sequence" && flow.destinationObjectId === objectId);
}

export function findContainingCollection(schema: MicroflowSchema, objectId: string): MicroflowObjectCollection | undefined {
  return findObjectWithCollection(schema, objectId)?.collection;
}

export function findParentLoopObject(schema: MicroflowSchema, collectionId: string): MicroflowObject | undefined {
  const match = collectCollections(schema.objectCollection).find(item => item.collection.id === collectionId);
  return match?.parentLoopObjectId ? findObjectWithCollection(schema, match.parentLoopObjectId)?.object : undefined;
}

function reachableWithEdges(edges: MicroflowGraphEdge[], fromObjectId: string, toObjectId: string): boolean {
  if (fromObjectId === toObjectId) {
    return true;
  }
  const queue = [fromObjectId];
  const visited = new Set<string>();
  while (queue.length) {
    const current = queue.shift();
    if (!current || visited.has(current)) {
      continue;
    }
    visited.add(current);
    for (const edge of edges.filter(item => item.fromObjectId === current)) {
      if (edge.toObjectId === toObjectId) {
        return true;
      }
      queue.push(edge.toObjectId);
    }
  }
  return false;
}

export function isReachableByNormalFlow(schema: MicroflowSchema, fromObjectId: string | undefined, toObjectId: string): boolean {
  if (!fromObjectId) {
    return true;
  }
  const edges = buildVariableGraphAnalysis(schema).normalEdges;
  return reachableWithEdges(edges, fromObjectId, toObjectId);
}

export function isReachableByErrorHandlerFlow(schema: MicroflowSchema, flowId: string, toObjectId: string): boolean {
  const analysis = buildVariableGraphAnalysis(schema);
  const errorEdge = analysis.errorHandlerEdges.find(edge => edge.flowId === flowId);
  if (!errorEdge) {
    return false;
  }
  return errorEdge.toObjectId === toObjectId || reachableWithEdges(analysis.normalEdges, errorEdge.toObjectId, toObjectId);
}

export function getReachableObjectsFromStart(schema: MicroflowSchema, collectionId = schema.objectCollection.id): string[] {
  const graph = buildMicroflowGraph(schema, collectionId);
  const starts = graph.startObjectIds;
  return graph.objects
    .map(object => object.id)
    .filter(objectId => starts.some(start => reachableWithEdges(graph.normalEdges, start, objectId)));
}

export function getAllPathsToObject(schema: MicroflowSchema, objectId: string, collectionId?: string): string[][] {
  const graph = buildMicroflowGraph(schema, collectionId ?? findContainingCollection(schema, objectId)?.id);
  const starts = graph.startObjectIds.length ? graph.startObjectIds : graph.objects.map(object => object.id);
  const paths: string[][] = [];
  const walk = (current: string, path: string[]) => {
    if (path.includes(current)) {
      return;
    }
    const nextPath = [...path, current];
    if (current === objectId) {
      paths.push(nextPath);
      return;
    }
    for (const edge of graph.normalEdges.filter(item => item.fromObjectId === current)) {
      walk(edge.toObjectId, nextPath);
    }
  };
  starts.forEach(start => walk(start, []));
  return paths.slice(0, 100);
}

export function getDominatingObjectsApprox(schema: MicroflowSchema, objectId: string, collectionId?: string): string[] {
  const paths = getAllPathsToObject(schema, objectId, collectionId);
  if (!paths.length) {
    return [];
  }
  return paths[0].filter(candidate => paths.every(path => path.includes(candidate)));
}

export function getMergePredecessorBranches(schema: MicroflowSchema, mergeObjectId: string, collectionId?: string): string[] {
  return getIncomingFlows(schema, mergeObjectId, collectionId)
    .filter(flow => !flow.isErrorHandler)
    .map(flow => flow.originObjectId);
}

export function collectLoopInternalObjects(schema: MicroflowSchema, loopObjectId: string): MicroflowObject[] {
  const loop = findObjectWithCollection(schema, loopObjectId)?.object;
  if (loop?.kind !== "loopedActivity") {
    return [];
  }
  const collect = (collection: MicroflowObjectCollection): MicroflowObject[] => collection.objects.flatMap(object =>
    object.kind === "loopedActivity" ? [object, ...collect(object.objectCollection)] : [object]);
  return collect(loop.objectCollection);
}

export function collectExecutableFlowsRecursive(schema: MicroflowSchema): MicroflowSequenceFlow[] {
  return collectFlowsRecursive(schema).filter((flow): flow is MicroflowSequenceFlow => flow.kind === "sequence" && !flow.isErrorHandler);
}
