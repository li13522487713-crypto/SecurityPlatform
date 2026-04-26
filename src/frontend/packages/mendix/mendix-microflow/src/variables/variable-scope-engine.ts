import type { MicroflowMetadataCatalog } from "../metadata";
import type {
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowSchema,
  MicroflowVariableIndex,
  MicroflowVariableSymbol,
  MicroflowVariableVisibility,
} from "../schema/types";
import { collectFlowsRecursive } from "../schema/utils/object-utils";
import { mockMicroflowMetadataCatalog } from "../metadata";
import { buildVariableIndex } from "./variable-index";

function flattenObjects(
  collection: MicroflowObjectCollection,
  parentLoopId?: string,
  ancestorLoopObjectIds: string[] = [],
): Array<{ object: MicroflowObject; loopObjectId?: string; ancestorLoopObjectIds: string[] }> {
  return collection.objects.flatMap(object => object.kind === "loopedActivity"
    ? [
        { object, loopObjectId: parentLoopId, ancestorLoopObjectIds },
        ...flattenObjects(object.objectCollection, object.id, [...ancestorLoopObjectIds, object.id]),
      ]
    : [{ object, loopObjectId: parentLoopId, ancestorLoopObjectIds }]);
}

function reachable(schema: MicroflowSchema, fromObjectId: string | undefined, toObjectId: string): boolean {
  if (!fromObjectId) {
    return true;
  }
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
    for (const flow of collectFlowsRecursive(schema)) {
      if (flow.kind !== "sequence" || flow.originObjectId !== current) {
        continue;
      }
      if (flow.destinationObjectId === toObjectId) {
        return true;
      }
      queue.push(flow.destinationObjectId);
    }
  }
  return false;
}

function loopAncestorsForObject(schema: MicroflowSchema, objectId: string): string[] {
  const location = flattenObjects(schema.objectCollection).find(item => item.object.id === objectId);
  return location ? [...location.ancestorLoopObjectIds, ...(location.loopObjectId && !location.ancestorLoopObjectIds.includes(location.loopObjectId) ? [location.loopObjectId] : [])] : [];
}

function isInErrorScope(schema: MicroflowSchema, symbol: MicroflowVariableSymbol, objectId: string): boolean {
  const flowId = symbol.scope.errorHandlerFlowId;
  if (!flowId) {
    return true;
  }
  const flow = collectFlowsRecursive(schema).find(item => item.id === flowId);
  return flow?.kind === "sequence" ? reachable(schema, flow.destinationObjectId, objectId) : false;
}

function hasDecisionAncestor(schema: MicroflowSchema, sourceObjectId: string | undefined, targetObjectId: string): boolean {
  if (!sourceObjectId) {
    return false;
  }
  const source = flattenObjects(schema.objectCollection).find(item => item.object.id === sourceObjectId)?.object;
  if (source?.kind === "exclusiveSplit" || source?.kind === "inheritanceSplit") {
    return true;
  }
  return collectFlowsRecursive(schema).some(flow =>
    flow.kind === "sequence" &&
    flow.destinationObjectId === sourceObjectId &&
    reachable(schema, flow.originObjectId, targetObjectId) &&
    hasDecisionAncestor(schema, flow.originObjectId, targetObjectId)
  );
}

export function getVariableSymbols(index: MicroflowVariableIndex): MicroflowVariableSymbol[];
export function getVariableSymbols(schema: MicroflowSchema, metadata?: MicroflowMetadataCatalog): MicroflowVariableSymbol[];
export function getVariableSymbols(
  schemaOrIndex: MicroflowSchema | MicroflowVariableIndex,
  metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog
): MicroflowVariableSymbol[] {
  if ("objectCollection" in schemaOrIndex) {
    const index = buildVariableIndex(schemaOrIndex, metadata);
    return index.all ?? [];
  }
  return schemaOrIndex.all ?? Object.values(schemaOrIndex.parameters)
    .concat(Object.values(schemaOrIndex.localVariables))
    .concat(Object.values(schemaOrIndex.objectOutputs))
    .concat(Object.values(schemaOrIndex.listOutputs))
    .concat(Object.values(schemaOrIndex.loopVariables))
    .concat(Object.values(schemaOrIndex.errorVariables))
    .concat(Object.values(schemaOrIndex.systemVariables));
}

export function isVariableVisibleAtObject(
  schema: MicroflowSchema,
  symbol: MicroflowVariableSymbol,
  objectId: string,
  includeCurrentObject: boolean
): boolean {
  if (symbol.scope.loopObjectId && !loopAncestorsForObject(schema, objectId).includes(symbol.scope.loopObjectId)) {
    return false;
  }
  if (!isInErrorScope(schema, symbol, objectId)) {
    return false;
  }
  if (!symbol.scope.startObjectId) {
    return true;
  }
  if (!includeCurrentObject && symbol.scope.startObjectId === objectId) {
    return false;
  }
  return reachable(schema, symbol.scope.startObjectId, objectId);
}

export function getVariableVisibilityAtObject(
  schema: MicroflowSchema,
  index: MicroflowVariableIndex,
  variableName: string,
  objectId: string
): MicroflowVariableVisibility {
  const symbols = (index.byName?.[variableName] ?? []).filter(symbol => isVariableVisibleAtObject(schema, symbol, objectId, true));
  if (!symbols.length) {
    return "unavailable";
  }
  return symbols.some(symbol => symbol.visibility === "maybe" || hasDecisionAncestor(schema, symbol.scope.startObjectId, objectId))
    ? "maybe"
    : "definite";
}

export function isVariableVisibleAtObjectByName(schema: MicroflowSchema, index: MicroflowVariableIndex, variableName: string, objectId: string): boolean {
  return getVariableVisibilityAtObject(schema, index, variableName, objectId) !== "unavailable";
}

function normalizeSymbols(schema: MicroflowSchema, index: MicroflowVariableIndex, objectId: string, includeCurrentObject: boolean): MicroflowVariableSymbol[] {
  const ancestors = loopAncestorsForObject(schema, objectId);
  return getVariableSymbols(index)
    .filter(symbol => isVariableVisibleAtObject(schema, symbol, objectId, includeCurrentObject))
    .sort((left, right) => {
      const leftDepth = left.scope.loopObjectId ? ancestors.indexOf(left.scope.loopObjectId) : -1;
      const rightDepth = right.scope.loopObjectId ? ancestors.indexOf(right.scope.loopObjectId) : -1;
      return rightDepth - leftDepth;
    })
    .map(symbol => ({
      ...symbol,
      visibility: getVariableVisibilityAtObject(schema, index, symbol.name, objectId),
    }));
}

export function getVariablesBeforeObject(schema: MicroflowSchema, index: MicroflowVariableIndex, objectId: string): MicroflowVariableSymbol[];
export function getVariablesBeforeObject(schema: MicroflowSchema, objectId: string, metadata?: MicroflowMetadataCatalog): MicroflowVariableSymbol[];
export function getVariablesBeforeObject(
  schema: MicroflowSchema,
  indexOrObjectId: MicroflowVariableIndex | string,
  objectIdOrMetadata?: string | MicroflowMetadataCatalog
): MicroflowVariableSymbol[] {
  if (typeof indexOrObjectId === "string") {
    const index = buildVariableIndex(schema, typeof objectIdOrMetadata === "object" ? objectIdOrMetadata : mockMicroflowMetadataCatalog);
    return normalizeSymbols(schema, index, indexOrObjectId, false);
  }
  return normalizeSymbols(schema, indexOrObjectId, String(objectIdOrMetadata), false);
}

export function getVariablesAfterObject(schema: MicroflowSchema, index: MicroflowVariableIndex, objectId: string): MicroflowVariableSymbol[];
export function getVariablesAfterObject(schema: MicroflowSchema, objectId: string, metadata?: MicroflowMetadataCatalog): MicroflowVariableSymbol[];
export function getVariablesAfterObject(
  schema: MicroflowSchema,
  indexOrObjectId: MicroflowVariableIndex | string,
  objectIdOrMetadata?: string | MicroflowMetadataCatalog
): MicroflowVariableSymbol[] {
  if (typeof indexOrObjectId === "string") {
    const index = buildVariableIndex(schema, typeof objectIdOrMetadata === "object" ? objectIdOrMetadata : mockMicroflowMetadataCatalog);
    return normalizeSymbols(schema, index, indexOrObjectId, true);
  }
  return normalizeSymbols(schema, indexOrObjectId, String(objectIdOrMetadata), true);
}

export function getAvailableVariablesAtObject(schema: MicroflowSchema, index: MicroflowVariableIndex, objectId: string): MicroflowVariableSymbol[] {
  return getVariablesBeforeObject(schema, index, objectId);
}
