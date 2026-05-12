import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import { getAssociationsForEntity, getEntityAttributes, getEntityByQualifiedName, getTargetEntityByAssociation, resolveStoredEntityQualifiedName } from "../metadata";
import type { MicroflowDataType, MicroflowSchema, MicroflowVariableIndex, MicroflowVariableSymbol } from "../schema/types";
import type { MicroflowMetadataCatalog } from "../metadata/metadata-catalog";
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
  allowedTypeKinds?: MicroflowDataType["kind"][];
  allowedTypes?: MicroflowDataType[];
  readonlyOnly?: boolean;
  writableOnly?: boolean;
}

interface RuntimeVariableScopeOptions {
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

function normalizeName(name: string): string {
  return name.startsWith("$") ? name.slice(1) : name;
}

function resolveEntityQualifiedName(dataType: MicroflowDataType, metadata: MicroflowMetadataCatalog): string | undefined {
  if (dataType.kind !== "object" || !dataType.entityQualifiedName) {
    return undefined;
  }
  return resolveStoredEntityQualifiedName(metadata, dataType.entityQualifiedName) ?? dataType.entityQualifiedName;
}

function collectObjectMemberTokens(entityQualifiedName: string | undefined, metadata: MicroflowMetadataCatalog): string[] {
  const resolvedEntity = getEntityByQualifiedName(metadata, entityQualifiedName);
  if (!resolvedEntity || !entityQualifiedName) {
    return [];
  }
  const tokens = new Set<string>();
  for (const attribute of getEntityAttributes(metadata, resolvedEntity.qualifiedName)) {
    tokens.add(attribute.name);
  }
  for (const association of getAssociationsForEntity(metadata, resolvedEntity.qualifiedName)) {
    tokens.add(association.name);
  }
  return [...tokens].sort((left, right) => left.localeCompare(right));
}

function resolveAssociationTarget(
  metadata: MicroflowMetadataCatalog,
  sourceEntityQualifiedName: string | undefined,
  segment: string,
): { associationQualifiedName: string; targetEntityQualifiedName: string } | null {
  if (!sourceEntityQualifiedName) {
    return null;
  }
  const segmentLower = segment.toLocaleLowerCase();
  const match = getAssociationsForEntity(metadata, sourceEntityQualifiedName).find(item => {
    return item.qualifiedName === segment
      || item.qualifiedName.toLocaleLowerCase().endsWith(`.${segmentLower}`)
      || item.qualifiedName.toLocaleLowerCase() === segmentLower;
  });
  if (!match) {
    return null;
  }
  const target = getTargetEntityByAssociation(metadata, match.qualifiedName, sourceEntityQualifiedName);
  return target ? { associationQualifiedName: match.qualifiedName, targetEntityQualifiedName: target.qualifiedName } : null;
}

function collectNestedMemberTokens(
  metadata: MicroflowMetadataCatalog,
  dataType: MicroflowDataType,
  path: string,
): string[] {
  const resolvedEntity = resolveEntityQualifiedName(dataType, metadata);
  if (!resolvedEntity) {
    return [];
  }
  const trimmed = path.trim();
  if (!trimmed) {
    return collectObjectMemberTokens(resolvedEntity, metadata);
  }

  const rawSegments = trimmed.split("/").filter(item => item.length > 0);
  if (!rawSegments.length) {
    return collectObjectMemberTokens(resolvedEntity, metadata);
  }

  let currentEntity = resolvedEntity;
  const segments = rawSegments.slice(0, Math.max(0, rawSegments.length - 1));
  for (const segment of segments) {
    const resolved = resolveAssociationTarget(metadata, currentEntity, segment);
    if (!resolved) {
      return [];
    }
    currentEntity = resolved.targetEntityQualifiedName;
  }

  return collectObjectMemberTokens(currentEntity, metadata);
}

function toMicroflowVariable(symbol: MicroflowVariableSymbol, metadata?: MicroflowMetadataCatalog): MicroflowVariable {
  const entityQualifiedName = resolveEntityQualifiedName(symbol.dataType, metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG);
  const withMetadata = metadata && metadata !== EMPTY_MICROFLOW_METADATA_CATALOG && symbol.dataType.kind === "object"
    ? collectNestedMemberTokens(metadata, symbol.dataType, "")
    : [];
  return {
    name: symbol.name.startsWith("$") ? symbol.name : `$${symbol.name}`,
    type: toMendixDataType(symbol.dataType),
    dataType: symbol.dataType,
    optional: symbol.readonly,
    documentation: symbol.documentation,
    entityType: entityQualifiedName,
    attributes: withMetadata,
  };
}

function queryFromContext(context?: VariableScopeContext): RuntimeVariableScopeOptions {
  return {
    includeMaybe: context?.includeMaybe,
    includeUnavailable: context?.includeUnavailable,
    includeSystem: context?.includeSystem,
    includeErrorContext: context?.includeErrorContext,
    allowedTypeKinds: context?.allowedTypeKinds,
    allowedTypes: context?.allowedTypes,
    readonlyOnly: context?.readonlyOnly,
    writableOnly: context?.writableOnly,
    collectionId: context?.collectionId,
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
  return symbols.map(symbol => toMicroflowVariable(symbol, metadata));
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
  return symbols.map(symbol => toMicroflowVariable(symbol));
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
  }).map(symbol => toMicroflowVariable(symbol, metadata));
}

export function getLoopIteratorVariables(schema: MicroflowSchema, objectId: string, metadata?: MicroflowMetadataCatalog): MicroflowVariable[] {
  const index = buildVariableIndex(schema, metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG);
  return getVariableSymbols(index)
    .filter(symbol => symbol.kind === "loopIterator")
    .filter(symbol => symbol.scope.kind === "loop" && symbol.scope.loopObjectId === objectId)
    .map(symbol => toMicroflowVariable(symbol, metadata));
}

export function getMicroflowParameters(schema: MicroflowSchema, metadata?: MicroflowMetadataCatalog): MicroflowVariable[] {
  const index = buildVariableIndex(schema, metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG);
  return getVariableSymbols(index)
    .filter(symbol => symbol.kind === "parameter")
    .map(symbol => toMicroflowVariable(symbol, metadata));
}

export function isVariableDefined(schema: MicroflowSchema, objectId: string, variableName: string, metadata?: MicroflowMetadataCatalog): boolean {
  const index = buildVariableIndex(schema, metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG);
  const normalized = normalizeName(variableName);
  return index.all?.some(symbol => normalizeName(symbol.name) === normalized) ?? false;
}
