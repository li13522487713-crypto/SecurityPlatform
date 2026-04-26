import type { MicroflowMetadataCatalog } from "../metadata";
import type { MicroflowSchema, MicroflowVariableSymbol } from "../schema/types";
import { mockMicroflowMetadataCatalog } from "../metadata";
import { getVariablesBeforeObject } from "./variable-scope-engine";

export interface MicroflowExpressionScopeContext {
  objectId: string;
  actionId?: string;
  fieldPath?: string;
}

export function getAvailableVariablesAtField(
  schema: MicroflowSchema,
  objectId: string,
  _fieldPath: string,
  metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog
): MicroflowVariableSymbol[] {
  return getVariablesBeforeObject(schema, objectId, metadata);
}

export function getVariablesForExpression(
  schema: MicroflowSchema,
  context: MicroflowExpressionScopeContext,
  metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog
): MicroflowVariableSymbol[] {
  return getAvailableVariablesAtField(schema, context.objectId, context.fieldPath ?? "", metadata);
}

export function resolveVariableReference(
  schema: MicroflowSchema,
  context: MicroflowExpressionScopeContext,
  variableName: string,
  metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog
): MicroflowVariableSymbol | null {
  const normalized = variableName.startsWith("$") ? variableName.slice(1) : variableName;
  return getVariablesForExpression(schema, context, metadata).find(symbol => symbol.name === variableName || symbol.name === normalized || `$${symbol.name}` === variableName) ?? null;
}
