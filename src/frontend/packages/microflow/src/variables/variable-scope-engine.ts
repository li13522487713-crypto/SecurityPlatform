import type { MicroflowMetadataCatalog } from "../metadata";
import type { MicroflowObject, MicroflowObjectCollection, MicroflowSchema, MicroflowVariableSymbol } from "../schema/types";
import { mockMicroflowMetadataCatalog } from "../metadata";
import { buildVariableIndex } from "./variable-index";

function flattenObjects(collection: MicroflowObjectCollection, parentLoopId?: string): Array<{ object: MicroflowObject; loopObjectId?: string }> {
  return collection.objects.flatMap(object => object.kind === "loopedActivity"
    ? [{ object, loopObjectId: parentLoopId }, ...flattenObjects(object.objectCollection, object.id)]
    : [{ object, loopObjectId: parentLoopId }]);
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
    for (const flow of schema.flows) {
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

function loopForObject(schema: MicroflowSchema, objectId: string): string | undefined {
  return flattenObjects(schema.objectCollection).find(item => item.object.id === objectId)?.loopObjectId;
}

function isInErrorScope(schema: MicroflowSchema, symbol: MicroflowVariableSymbol, objectId: string): boolean {
  const flowId = symbol.scope.errorHandlerFlowId;
  if (!flowId) {
    return true;
  }
  const flow = schema.flows.find(item => item.id === flowId);
  return flow?.kind === "sequence" ? reachable(schema, flow.destinationObjectId, objectId) : false;
}

export function isVariableVisibleAtObject(schema: MicroflowSchema, symbol: MicroflowVariableSymbol, objectId: string, includeCurrentObject: boolean): boolean {
  if (symbol.scope.loopObjectId && loopForObject(schema, objectId) !== symbol.scope.loopObjectId && objectId !== symbol.scope.loopObjectId) {
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

export function getVariableSymbols(schema: MicroflowSchema, metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog): MicroflowVariableSymbol[] {
  const index = buildVariableIndex(schema, metadata);
  return Object.values(index.parameters)
    .concat(Object.values(index.localVariables))
    .concat(Object.values(index.objectOutputs))
    .concat(Object.values(index.listOutputs))
    .concat(Object.values(index.loopVariables))
    .concat(Object.values(index.errorVariables))
    .concat(Object.values(index.systemVariables));
}

export function getVariablesBeforeObject(schema: MicroflowSchema, objectId: string, metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog): MicroflowVariableSymbol[] {
  return getVariableSymbols(schema, metadata).filter(symbol => isVariableVisibleAtObject(schema, symbol, objectId, false));
}

export function getVariablesAfterObject(schema: MicroflowSchema, objectId: string, metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog): MicroflowVariableSymbol[] {
  return getVariableSymbols(schema, metadata).filter(symbol => isVariableVisibleAtObject(schema, symbol, objectId, true));
}
