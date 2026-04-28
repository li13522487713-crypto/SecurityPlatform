import type { MicroflowMetadataCatalog } from "../metadata";
import type { MicroflowSchema, MicroflowVariableIndex, MicroflowVariableSymbol } from "../schema/types";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import { buildVariableIndex } from "./variable-index";
import { getVariablesBeforeObject, type MicroflowVariableQueryOptions } from "./variable-scope-engine";

export interface MicroflowExpressionScopeContext {
  objectId: string;
  actionId?: string;
  fieldPath?: string;
  collectionId?: string;
  options?: MicroflowVariableQueryOptions;
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
  return getAvailableVariablesAtField(schema, context.objectId, context.fieldPath ?? "", metadata, context.options);
}

export function getVariablesForExpressionFromIndex(
  schema: MicroflowSchema,
  index: MicroflowVariableIndex,
  context: MicroflowExpressionScopeContext,
): MicroflowVariableSymbol[] {
  return getAvailableVariablesAtField(schema, index, context.objectId, context.fieldPath, { ...context.options, collectionId: context.collectionId ?? context.options?.collectionId });
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
  const normalized = variableName.startsWith("$") ? variableName.slice(1) : variableName;
  return getVariablesForExpressionFromIndex(schema, index, context).find(symbol =>
    symbol.name === variableName ||
    symbol.name === normalized ||
    `$${symbol.name}` === variableName
  ) ?? null;
}
