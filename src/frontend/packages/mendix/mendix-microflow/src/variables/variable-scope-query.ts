import type { MicroflowMetadataCatalog } from "../metadata";
import type { MicroflowAction, MicroflowDataType, MicroflowSchema, MicroflowVariableIndex, MicroflowVariableSymbol } from "../schema/types";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import { findObjectWithCollection } from "../schema/utils/object-utils";
import { buildVariableIndex } from "./variable-index";
import { getVariablesBeforeObject, type MicroflowVariableQueryOptions } from "./variable-scope-engine";

export interface MicroflowExpressionScopeContext {
  objectId: string;
  actionId?: string;
  fieldPath?: string;
  collectionId?: string;
  options?: MicroflowVariableQueryOptions;
}

function stringField(action: MicroflowAction, key: string): string | undefined {
  const value = (action as Record<string, unknown>)[key];
  return typeof value === "string" && value.trim() ? value : undefined;
}

function normalizeScopedVariableName(name?: string): string | undefined {
  if (!name?.trim()) {
    return undefined;
  }
  return name.startsWith("$.")
    ? name.slice(2)
    : name.startsWith("$")
      ? name.slice(1)
      : name;
}

function listItemTypeFromVariable(index: MicroflowVariableIndex, ...candidateNames: Array<string | undefined>): MicroflowDataType | undefined {
  for (const name of candidateNames) {
    if (!name) {
      continue;
    }
    const listSymbol = (index.byName?.[name] ?? []).find(symbol => symbol.dataType.kind === "list");
    if (listSymbol?.dataType.kind === "list") {
      return listSymbol.dataType.itemType;
    }
  }
  return undefined;
}

function contextualExpressionVariables(
  schema: MicroflowSchema,
  index: MicroflowVariableIndex,
  context: MicroflowExpressionScopeContext,
): MicroflowVariableSymbol[] {
  const location = findObjectWithCollection(schema, context.objectId);
  const object = location?.object;
  if (!location || !object || object.kind !== "actionActivity") {
    return [];
  }
  const action = object.action;
  const buildLocalSymbol = (itemVariableName: string, itemType: MicroflowDataType): MicroflowVariableSymbol => ({
    id: `localVariable:${object.id}:${action.id}:${itemVariableName}`,
    name: itemVariableName,
    displayName: itemVariableName,
    kind: "localVariable",
    dataType: itemType,
    source: { kind: "localVariable", objectId: object.id, actionId: action.id },
    scope: { kind: "collection", collectionId: location.collectionId, startObjectId: object.id },
    readonly: false,
    visibility: "definite",
  });

  if (action.kind === "filterList") {
    if (context.fieldPath !== "action.conditionExpression" && context.fieldPath !== "action.filterExpression") {
      return [];
    }
    const itemVariableName = normalizeScopedVariableName(
      stringField(action, "itemVariableName")
        ?? stringField(action, "itemVariable")
        ?? stringField(action, "objectVariableName"),
    );
    if (!itemVariableName) {
      return [];
    }
    const itemType = listItemTypeFromVariable(index, stringField(action, "sourceListVariableName"), stringField(action, "listVariableName"))
      ?? (((action as Record<string, unknown>).itemType as MicroflowDataType | undefined) ?? { kind: "unknown", reason: "filterList item type" });
    return [buildLocalSymbol(itemVariableName, itemType)];
  }

  if (action.kind === "changeList" && context.fieldPath === "action.conditionExpression" && action.operation === "removeWhere") {
    const itemVariableName = normalizeScopedVariableName(
      stringField(action, "objectVariableName")
        ?? stringField(action, "itemVariableName")
        ?? "item",
    );
    if (!itemVariableName) {
      return [];
    }
    const itemType = listItemTypeFromVariable(index, action.targetListVariableName, stringField(action, "sourceListVariableName"))
      ?? { kind: "unknown", reason: "changeList removeWhere item type" };
    return [buildLocalSymbol(itemVariableName, itemType)];
  }

  if (action.kind === "listOperation") {
    const isFilterExpression =
      action.operation === "filter"
      && (context.fieldPath === "action.filterExpression" || context.fieldPath === "action.expression");
    const isMapExpression =
      action.operation === "map"
      && context.fieldPath === "action.expression";
    const isSortExpression =
      action.operation === "sort"
      && (context.fieldPath === "action.sortExpression" || context.fieldPath?.startsWith("action.sortKeys.") === true);
    if (!isFilterExpression && !isMapExpression && !isSortExpression) {
      return [];
    }
    const itemVariableName = normalizeScopedVariableName(
      stringField(action, "objectVariableName")
        ?? stringField(action, "itemVariableName")
        ?? stringField(action, "itemVariable")
        ?? "item",
    );
    if (!itemVariableName) {
      return [];
    }
    const itemType = listItemTypeFromVariable(index, action.leftListVariableName, action.sourceListVariableName)
      ?? { kind: "unknown", reason: "listOperation item type" };
    return [buildLocalSymbol(itemVariableName, itemType)];
  }

  if (action.kind === "sortList") {
    const isSortExpression =
      context.fieldPath === "action.sortExpression"
      || context.fieldPath?.startsWith("action.sortKeys.") === true;
    if (!isSortExpression) {
      return [];
    }
    const itemVariableName = normalizeScopedVariableName(
      stringField(action, "objectVariableName")
        ?? stringField(action, "itemVariableName")
        ?? stringField(action, "itemVariable")
        ?? "item",
    );
    if (!itemVariableName) {
      return [];
    }
    const itemType = listItemTypeFromVariable(index, stringField(action, "sourceListVariableName"), stringField(action, "listVariableName"))
      ?? { kind: "unknown", reason: "sortList item type" };
    return [buildLocalSymbol(itemVariableName, itemType)];
  }

  return [];
}

function mergeVisibleVariables(base: MicroflowVariableSymbol[], contextual: MicroflowVariableSymbol[]): MicroflowVariableSymbol[] {
  const seen = new Set<string>();
  const merged: MicroflowVariableSymbol[] = [];
  for (const symbol of [...contextual, ...base]) {
    const key = symbol.name;
    if (seen.has(key)) {
      continue;
    }
    seen.add(key);
    merged.push(symbol);
  }
  return merged;
}

export function getAvailableVariablesAtField(
  schema: MicroflowSchema,
  index: MicroflowVariableIndex,
  objectId: string,
  _fieldPath?: string,
  options?: MicroflowVariableQueryOptions,
): MicroflowVariableSymbol[];
export function getAvailableVariablesAtField(
  schema: MicroflowSchema,
  objectId: string,
  _fieldPath: string,
  metadata?: MicroflowMetadataCatalog,
  options?: MicroflowVariableQueryOptions,
): MicroflowVariableSymbol[];
export function getAvailableVariablesAtField(
  schema: MicroflowSchema,
  indexOrObjectId: MicroflowVariableIndex | string,
  objectIdOrFieldPath?: string,
  fieldPathOrMetadata?: string | MicroflowMetadataCatalog,
  options?: MicroflowVariableQueryOptions,
): MicroflowVariableSymbol[] {
  if (typeof indexOrObjectId === "string") {
    return getVariablesBeforeObject(schema, indexOrObjectId, typeof fieldPathOrMetadata === "object" ? fieldPathOrMetadata : EMPTY_MICROFLOW_METADATA_CATALOG);
  }
  return getVariablesBeforeObject(schema, indexOrObjectId, String(objectIdOrFieldPath), options);
}

export function getVariablesForExpression(
  schema: MicroflowSchema,
  context: MicroflowExpressionScopeContext,
  metadata: MicroflowMetadataCatalog = EMPTY_MICROFLOW_METADATA_CATALOG,
): MicroflowVariableSymbol[] {
  const index = buildVariableIndex(schema, metadata);
  return mergeVisibleVariables(
    getAvailableVariablesAtField(schema, index, context.objectId, context.fieldPath, { ...context.options, collectionId: context.collectionId ?? context.options?.collectionId }),
    contextualExpressionVariables(schema, index, context),
  );
}

export function getVariablesForExpressionFromIndex(
  schema: MicroflowSchema,
  index: MicroflowVariableIndex,
  context: MicroflowExpressionScopeContext,
): MicroflowVariableSymbol[] {
  return mergeVisibleVariables(
    getAvailableVariablesAtField(schema, index, context.objectId, context.fieldPath, { ...context.options, collectionId: context.collectionId ?? context.options?.collectionId }),
    contextualExpressionVariables(schema, index, context),
  );
}

export function resolveVariableReference(
  schema: MicroflowSchema,
  context: MicroflowExpressionScopeContext,
  variableName: string,
  metadata: MicroflowMetadataCatalog = EMPTY_MICROFLOW_METADATA_CATALOG,
): MicroflowVariableSymbol | null {
  const index = buildVariableIndex(schema, metadata);
  return resolveVariableReferenceFromIndex(schema, index, context, variableName);
}

export function resolveVariableReferenceFromIndex(
  schema: MicroflowSchema,
  index: MicroflowVariableIndex,
  context: MicroflowExpressionScopeContext,
  variableName: string,
): MicroflowVariableSymbol | null {
  const normalized = variableName.startsWith("$.")
    ? variableName.slice(2)
    : variableName.startsWith("$")
      ? variableName.slice(1)
      : variableName;
  return getVariablesForExpressionFromIndex(schema, index, context).find(symbol =>
    symbol.name === variableName ||
    symbol.name === normalized ||
    `$${symbol.name}` === variableName ||
    `$.${symbol.name}` === variableName
  ) ?? null;
}
