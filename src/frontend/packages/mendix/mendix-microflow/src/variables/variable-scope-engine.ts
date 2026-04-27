import type { MicroflowMetadataCatalog } from "../metadata";
import type {
  MicroflowDataType,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowSchema,
  MicroflowVariableIndex,
  MicroflowVariableSymbol,
  MicroflowVariableVisibility,
} from "../schema/types";
import { findObjectWithCollection } from "../schema/utils/object-utils";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import { buildVariableIndex } from "./variable-index";
import {
  getDominatingObjectsApprox,
  isReachableByErrorHandlerFlow,
  isReachableByNormalFlow,
} from "./microflow-graph-analysis";

export interface MicroflowVariableQueryOptions {
  includeMaybe?: boolean;
  includeUnavailable?: boolean;
  includeSystem?: boolean;
  includeErrorContext?: boolean;
  allowedTypeKinds?: MicroflowDataType["kind"][];
  allowedTypes?: MicroflowDataType[];
  readonlyOnly?: boolean;
  writableOnly?: boolean;
  collectionId?: string;
}

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

function loopAncestorsForObject(schema: MicroflowSchema, objectId: string): string[] {
  const location = flattenObjects(schema.objectCollection).find(item => item.object.id === objectId);
  return location ? [...location.ancestorLoopObjectIds, ...(location.loopObjectId && !location.ancestorLoopObjectIds.includes(location.loopObjectId) ? [location.loopObjectId] : [])] : [];
}

function isInErrorScope(schema: MicroflowSchema, symbol: MicroflowVariableSymbol, objectId: string): boolean {
  const flowId = symbol.scope.errorHandlerFlowId;
  if (!flowId) {
    return true;
  }
  return isReachableByErrorHandlerFlow(schema, flowId, objectId);
}

function isDominatingApprox(schema: MicroflowSchema, sourceObjectId: string | undefined, targetObjectId: string): boolean {
  if (!sourceObjectId) {
    return true;
  }
  if (sourceObjectId === targetObjectId) {
    return true;
  }
  const targetLocation = findObjectWithCollection(schema, targetObjectId);
  const sourceLocation = findObjectWithCollection(schema, sourceObjectId);
  if (!targetLocation || !sourceLocation) {
    return false;
  }
  if (targetLocation.collectionId !== sourceLocation.collectionId) {
    return true;
  }
  return getDominatingObjectsApprox(schema, targetObjectId, targetLocation.collectionId).includes(sourceObjectId);
}

export function getVariableSymbols(index: MicroflowVariableIndex): MicroflowVariableSymbol[];
export function getVariableSymbols(schema: MicroflowSchema, metadata?: MicroflowMetadataCatalog): MicroflowVariableSymbol[];
export function getVariableSymbols(
  schemaOrIndex: MicroflowSchema | MicroflowVariableIndex,
  metadata: MicroflowMetadataCatalog = EMPTY_MICROFLOW_METADATA_CATALOG,
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
  if (symbol.visibility === "unavailable") {
    return false;
  }
  if (symbol.scope.loopObjectId && !loopAncestorsForObject(schema, objectId).includes(symbol.scope.loopObjectId)) {
    return false;
  }
  if (symbol.scope.kind === "errorHandler" && !isInErrorScope(schema, symbol, objectId)) {
    return false;
  }
  if (symbol.scope.kind !== "errorHandler" && symbol.scope.errorHandlerFlowId) {
    return false;
  }
  if (!symbol.scope.startObjectId) {
    return true;
  }
  if (!includeCurrentObject && symbol.scope.startObjectId === objectId) {
    return false;
  }
  return symbol.scope.kind === "errorHandler"
    ? isInErrorScope(schema, symbol, objectId)
    : isReachableByNormalFlow(schema, symbol.scope.startObjectId, objectId);
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
  return symbols.some(symbol => symbol.visibility === "maybe" || !isDominatingApprox(schema, symbol.scope.startObjectId, objectId))
    ? "maybe"
    : "definite";
}

export function isVariableVisibleAtObjectByName(schema: MicroflowSchema, index: MicroflowVariableIndex, variableName: string, objectId: string): boolean {
  return getVariableVisibilityAtObject(schema, index, variableName, objectId) !== "unavailable";
}

function sameDataType(left: MicroflowDataType, right: MicroflowDataType): boolean {
  if (left.kind !== right.kind) {
    return false;
  }
  if (left.kind === "object" && right.kind === "object") {
    return left.entityQualifiedName === right.entityQualifiedName;
  }
  if (left.kind === "list" && right.kind === "list") {
    return sameDataType(left.itemType, right.itemType);
  }
  if (left.kind === "enumeration" && right.kind === "enumeration") {
    return left.enumerationQualifiedName === right.enumerationQualifiedName;
  }
  return true;
}

function matchesAllowedTypes(symbol: MicroflowVariableSymbol, allowedTypes?: MicroflowDataType[]): boolean {
  return !allowedTypes?.length || allowedTypes.some(type => sameDataType(symbol.dataType, type));
}

function filterByOptions(symbols: MicroflowVariableSymbol[], options?: MicroflowVariableQueryOptions): MicroflowVariableSymbol[] {
  return symbols
    .filter(symbol => options?.includeMaybe ?? true ? true : symbol.visibility !== "maybe")
    .filter(symbol => options?.includeUnavailable ? true : symbol.visibility !== "unavailable")
    .filter(symbol => options?.includeSystem ?? true ? true : symbol.kind !== "system")
    .filter(symbol => options?.includeErrorContext ?? true ? true : symbol.kind !== "errorContext" && symbol.kind !== "restResponse" && symbol.kind !== "soapFault")
    .filter(symbol => !options?.allowedTypeKinds?.length || options.allowedTypeKinds.includes(symbol.dataType.kind))
    .filter(symbol => matchesAllowedTypes(symbol, options?.allowedTypes))
    .filter(symbol => options?.readonlyOnly ? symbol.readonly : true)
    .filter(symbol => options?.writableOnly ? !symbol.readonly : true)
    .filter(symbol => options?.collectionId ? symbol.scope.collectionId === options.collectionId || symbol.scope.kind === "global" : true);
}

function normalizeSymbols(schema: MicroflowSchema, index: MicroflowVariableIndex, objectId: string, includeCurrentObject: boolean, options?: MicroflowVariableQueryOptions): MicroflowVariableSymbol[] {
  const ancestors = loopAncestorsForObject(schema, objectId);
  const visible = getVariableSymbols(index)
    .filter(symbol => isVariableVisibleAtObject(schema, symbol, objectId, includeCurrentObject))
    .sort((left, right) => {
      const leftDepth = left.scope.loopObjectId ? ancestors.indexOf(left.scope.loopObjectId) : -1;
      const rightDepth = right.scope.loopObjectId ? ancestors.indexOf(right.scope.loopObjectId) : -1;
      return rightDepth - leftDepth;
    })
    .map(symbol => ({
      ...symbol,
      visibility: getVariableVisibilityAtObject(schema, index, symbol.name, objectId),
      maybeReason: getVariableVisibilityAtObject(schema, index, symbol.name, objectId) === "maybe" ? "Variable is not definitely assigned on every normal path to this object." : symbol.maybeReason,
    }));
  return filterByOptions(visible, options);
}

export function getVariablesBeforeObject(schema: MicroflowSchema, index: MicroflowVariableIndex, objectId: string, options?: MicroflowVariableQueryOptions): MicroflowVariableSymbol[];
export function getVariablesBeforeObject(schema: MicroflowSchema, objectId: string, metadata?: MicroflowMetadataCatalog): MicroflowVariableSymbol[];
export function getVariablesBeforeObject(
  schema: MicroflowSchema,
  indexOrObjectId: MicroflowVariableIndex | string,
  objectIdOrMetadata?: string | MicroflowMetadataCatalog,
  options?: MicroflowVariableQueryOptions
): MicroflowVariableSymbol[] {
  if (typeof indexOrObjectId === "string") {
    const index = buildVariableIndex(schema, typeof objectIdOrMetadata === "object" ? objectIdOrMetadata : EMPTY_MICROFLOW_METADATA_CATALOG);
    return normalizeSymbols(schema, index, indexOrObjectId, false);
  }
  return normalizeSymbols(schema, indexOrObjectId, String(objectIdOrMetadata), false, options);
}

export function getVariablesAfterObject(schema: MicroflowSchema, index: MicroflowVariableIndex, objectId: string, options?: MicroflowVariableQueryOptions): MicroflowVariableSymbol[];
export function getVariablesAfterObject(schema: MicroflowSchema, objectId: string, metadata?: MicroflowMetadataCatalog): MicroflowVariableSymbol[];
export function getVariablesAfterObject(
  schema: MicroflowSchema,
  indexOrObjectId: MicroflowVariableIndex | string,
  objectIdOrMetadata?: string | MicroflowMetadataCatalog,
  options?: MicroflowVariableQueryOptions
): MicroflowVariableSymbol[] {
  if (typeof indexOrObjectId === "string") {
    const index = buildVariableIndex(schema, typeof objectIdOrMetadata === "object" ? objectIdOrMetadata : EMPTY_MICROFLOW_METADATA_CATALOG);
    return normalizeSymbols(schema, index, indexOrObjectId, true);
  }
  return normalizeSymbols(schema, indexOrObjectId, String(objectIdOrMetadata), true, options);
}

export function getAvailableVariablesAtObject(schema: MicroflowSchema, index: MicroflowVariableIndex, objectId: string, options?: MicroflowVariableQueryOptions): MicroflowVariableSymbol[] {
  return getVariablesBeforeObject(schema, index, objectId, options);
}

export function getVariableTypeAtObject(schema: MicroflowSchema, index: MicroflowVariableIndex, objectId: string, variableName: string): MicroflowDataType | undefined {
  return (index.byName?.[variableName] ?? [])
    .map(symbol => ({ ...symbol, visibility: getVariableVisibilityAtObject(schema, index, symbol.name, objectId) }))
    .find(symbol => symbol.visibility !== "unavailable")?.dataType;
}

export function getOutputVariablesForObject(index: MicroflowVariableIndex, objectId: string): MicroflowVariableSymbol[] {
  return index.byObjectId?.[objectId] ?? [];
}

export function getVariablesInScope(index: MicroflowVariableIndex, scopeKey: string): MicroflowVariableSymbol[] {
  return index.byScopeKey?.[scopeKey] ?? index.byCollectionId?.[scopeKey] ?? [];
}
