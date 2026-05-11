import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import type { MicroflowSchema, MicroflowMetadataCatalog, MicroflowVariableIndex, MicroflowVariableSymbol } from "../schema/types";
import { buildVariableIndex } from "../variables";
import { getAvailableVariablesAtField } from "../variables/variable-scope-query";
import { getVariablesForExpressionFromIndex } from "../variables/variable-scope-query";
import { toMendixDataType, type MicroflowVariable } from "../types/mendix-types";
import { getVariableSymbols } from "../variables";

export interface VariableScopeContext {
  objectId: string;
  actionId?: string;
  fieldPath?: string;
  collectionId?: string;
  metadata?: MicroflowMetadataCatalog;
  includeMaybe?: boolean;
  includeUnavailable?: boolean;
  includeSystem?: boolean;
  includeErrorContext?: boolean;
}

interface RuntimeVariableScopeOptions {
  includeMaybe?: boolean;
  includeUnavailable?: boolean;
  includeSystem?: boolean;
  includeErrorContext?: boolean;
}

function normalizeName(name: string): string {
  return name.startsWith("$") ? name.slice(1) : name;
}

function toMicroflowVariable(symbol: MicroflowVariableSymbol): MicroflowVariable {
  return {
    name: symbol.name.startsWith("$") ? symbol.name : `$${symbol.name}`,
    type: toMendixDataType(symbol.dataType),
    dataType: symbol.dataType,
    optional: symbol.readonly,
    documentation: symbol.documentation,
    entityType: symbol.dataType.kind === "object" ? symbol.dataType.entityQualifiedName : undefined,
    attributes: [],
  };
}

function queryFromContext(context?: VariableScopeContext): RuntimeVariableScopeOptions {
  return {
    includeMaybe: context?.includeMaybe,
    includeUnavailable: context?.includeUnavailable,
    includeSystem: context?.includeSystem,
    includeErrorContext: context?.includeErrorContext,
  };
}

export function computeAvailableVariables(
  schema: MicroflowSchema,
  context: VariableScopeContext,
  metadata: MicroflowMetadataCatalog = EMPTY_MICROFLOW_METADATA_CATALOG,
): MicroflowVariable[] {
  const variableIndex = buildVariableIndex(schema, metadata);
  const symbols = getAvailableVariablesAtField(
    schema,
    variableIndex,
    context.objectId,
    context.fieldPath,
    queryFromContext(context),
  );
  return symbols.map(toMicroflowVariable);
}

export function computeAvailableVariablesWithIndex(
  schema: MicroflowSchema,
  context: VariableScopeContext,
  variableIndex: MicroflowVariableIndex,
): MicroflowVariable[] {
  const symbols = getAvailableVariablesAtField(
    schema,
    variableIndex,
    context.objectId,
    context.fieldPath,
    queryFromContext(context),
  );
  return symbols.map(toMicroflowVariable);
}

export function getVariableScopeForExpression(
  schema: MicroflowSchema,
  context: VariableScopeContext,
  metadata: MicroflowMetadataCatalog = EMPTY_MICROFLOW_METADATA_CATALOG,
): MicroflowVariable[] {
  return computeAvailableVariables(schema, context, metadata);
}

export function getVariablesBeforeObject(
  schema: MicroflowSchema,
  objectId: string,
  metadata?: MicroflowMetadataCatalog,
): MicroflowVariable[] {
  return getVariablesForExpressionFromIndex(schema, buildVariableIndex(schema, metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG), {
    objectId,
  }).map(toMicroflowVariable);
}

export function getLoopIteratorVariables(schema: MicroflowSchema, objectId: string, metadata?: MicroflowMetadataCatalog): MicroflowVariable[] {
  const index = buildVariableIndex(schema, metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG);
  return getVariableSymbols(index)
    .filter(symbol => symbol.kind === "loopIterator")
    .filter(symbol => symbol.scope.kind === "loop" && symbol.scope.loopObjectId === objectId)
    .map(toMicroflowVariable);
}

export function getMicroflowParameters(schema: MicroflowSchema, metadata?: MicroflowMetadataCatalog): MicroflowVariable[] {
  const index = buildVariableIndex(schema, metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG);
  return getVariableSymbols(index)
    .filter(symbol => symbol.kind === "parameter")
    .map(toMicroflowVariable);
}

export function isVariableDefined(schema: MicroflowSchema, objectId: string, variableName: string, metadata?: MicroflowMetadataCatalog): boolean {
  const index = buildVariableIndex(schema, metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG);
  const normalized = normalizeName(variableName);
  return index.all?.some(symbol => normalizeName(symbol.name) === normalized) ?? false;
}

