import type { MicroflowDataType, MicroflowSchema } from "../schema/types";
import type { MicroflowVariableIndex } from "../schema/types";
import { getAvailableVariablesAtObject } from "./variable-scope-engine";
import { dataTypeLabel } from "./variable-type-utils";

/** Editor/debug panel: available variables at an object (schema-derived previews). */
export interface MicroflowEditorVariableSnapshot {
  objectId: string;
  variables: Array<{
    name: string;
    type: MicroflowDataType;
    valuePreview?: string;
    source: string;
  }>;
}

export function buildInitialVariableSnapshot(schema: MicroflowSchema, index: MicroflowVariableIndex, objectId: string): MicroflowEditorVariableSnapshot {
  return {
    objectId,
    variables: getAvailableVariablesAtObject(schema, index, objectId).map(symbol => ({
      name: symbol.name,
      type: symbol.dataType,
      valuePreview: dataTypeLabel(symbol.dataType),
      source: symbol.source.kind,
    })),
  };
}

export function getDebugVisibleVariablesForObject(schema: MicroflowSchema, index: MicroflowVariableIndex, objectId: string): MicroflowEditorVariableSnapshot {
  return buildInitialVariableSnapshot(schema, index, objectId);
}
