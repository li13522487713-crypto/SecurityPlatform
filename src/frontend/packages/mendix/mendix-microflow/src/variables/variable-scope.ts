import type { MicroflowMetadataCatalog } from "../metadata";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import type { MicroflowSchema, MicroflowVariableIndex, MicroflowVariableScope, MicroflowVariableSymbol } from "../schema/types";
import { buildVariableIndex } from "./variable-index";
import { getVariablesAfterObject, getVariablesBeforeObject } from "./variable-scope-engine";

export type { MicroflowVariableScope };

export interface VariableScopeContext {
  schema: MicroflowSchema;
  metadata?: MicroflowMetadataCatalog;
}

export interface VariableScopeQueryOptions {
  includeMaybe?: boolean;
  includeUnavailable?: boolean;
  includeSystem?: boolean;
  includeErrorContext?: boolean;
}

export function buildScopeIndex(
  schema: MicroflowSchema,
  metadata: MicroflowMetadataCatalog = EMPTY_MICROFLOW_METADATA_CATALOG,
): MicroflowVariableIndex {
  return buildVariableIndex(schema, metadata);
}

export function normalizeVariableName(name: string): string {
  return name.startsWith("$") ? name.slice(1) : name;
}

export function getScopeVariables(
  schema: MicroflowSchema,
  objectId: string,
  metadata: MicroflowMetadataCatalog = EMPTY_MICROFLOW_METADATA_CATALOG,
): MicroflowVariableSymbol[] {
  const index = buildVariableIndex(schema, metadata);
  return getVariablesBeforeObject(schema, index, objectId, {
    includeMaybe: true,
    includeUnavailable: false,
    includeSystem: true,
    includeErrorContext: true,
  });
}

export function getVariablesAfter(
  schema: MicroflowSchema,
  objectId: string,
  metadata: MicroflowMetadataCatalog = EMPTY_MICROFLOW_METADATA_CATALOG,
): MicroflowVariableSymbol[] {
  const index = buildVariableIndex(schema, metadata);
  return getVariablesAfterObject(schema, index, objectId, {
    includeMaybe: true,
    includeUnavailable: false,
    includeSystem: true,
    includeErrorContext: true,
  });
}

export function collectLoopIteratorVariableNames(
  schema: MicroflowSchema,
  loopObjectId: string,
  metadata: MicroflowMetadataCatalog = EMPTY_MICROFLOW_METADATA_CATALOG,
): string[] {
  const index = buildVariableIndex(schema, metadata);
  return index.all
    ?.filter(item => item.kind === "loopIterator" && item.scope.kind === "loop" && item.scope.loopObjectId === loopObjectId)
    .map(item => item.name) ?? [];
}
