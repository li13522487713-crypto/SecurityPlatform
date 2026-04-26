import type { MicroflowMetadataCatalog } from "../metadata";
import type { MicroflowSchema, MicroflowVariableIndex, MicroflowVariableSymbol } from "../schema/types";
import { mockMicroflowMetadataCatalog } from "../metadata";
import { buildVariableIndex } from "./variable-index";
import { getVariablesBeforeObject } from "./variable-scope-engine";

export interface MicroflowExpressionScopeContext {
  objectId: string;
  actionId?: string;
  fieldPath?: string;
}

export function getAvailableVariablesAtField(
  schema: MicroflowSchema,
  index: MicroflowVariableIndex,
  objectId: string,
  _fieldPath?: string,
): MicroflowVariableSymbol[];
export function getAvailableVariablesAtField(
  schema: MicroflowSchema,
  objectId: string,
  _fieldPath: string,
  metadata?: MicroflowMetadataCatalog,
): MicroflowVariableSymbol[];
export function getAvailableVariablesAtField(
  schema: MicroflowSchema,
  indexOrObjectId: MicroflowVariableIndex | string,
  objectIdOrFieldPath?: string,
  fieldPathOrMetadata?: string | MicroflowMetadataCatalog,
): MicroflowVariableSymbol[] {
  if (typeof indexOrObjectId === "string") {
    return getVariablesBeforeObject(schema, indexOrObjectId, typeof fieldPathOrMetadata === "object" ? fieldPathOrMetadata : mockMicroflowMetadataCatalog);
  }
  return getVariablesBeforeObject(schema, indexOrObjectId, String(objectIdOrFieldPath));
}

export function getVariablesForExpression(
  schema: MicroflowSchema,
  context: MicroflowExpressionScopeContext,
  metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog
): MicroflowVariableSymbol[] {
  return getAvailableVariablesAtField(schema, context.objectId, context.fieldPath ?? "", metadata);
}

export function getVariablesForExpressionFromIndex(
  schema: MicroflowSchema,
  index: MicroflowVariableIndex,
  context: MicroflowExpressionScopeContext,
): MicroflowVariableSymbol[] {
  return getAvailableVariablesAtField(schema, index, context.objectId, context.fieldPath);
}

export function resolveVariableReference(
  schema: MicroflowSchema,
  context: MicroflowExpressionScopeContext,
  variableName: string,
  metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog
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
