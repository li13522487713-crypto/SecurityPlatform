import type { MicroflowDataType, MicroflowSchema } from "../schema/types";
import type { MicroflowVariableIndex } from "../schema/types";
import { getAvailableVariablesAtObject } from "./variable-scope-engine";
import { dataTypeLabel } from "./variable-type-utils";

export interface MicroflowVariableSnapshot {
  objectId: string;
  variables: Array<{
    name: string;
    type: MicroflowDataType;
    valuePreview?: string;
    source: string;
  }>;
}

export function buildInitialVariableSnapshot(schema: MicroflowSchema, index: MicroflowVariableIndex, objectId: string): MicroflowVariableSnapshot {
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

export function getDebugVisibleVariablesForObject(schema: MicroflowSchema, index: MicroflowVariableIndex, objectId: string): MicroflowVariableSnapshot {
  return buildInitialVariableSnapshot(schema, index, objectId);
}
