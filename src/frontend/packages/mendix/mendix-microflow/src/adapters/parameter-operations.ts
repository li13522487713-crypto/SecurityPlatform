import type { MicroflowParameter, MicroflowSchema } from "../schema/types";
import { removeMicroflowParameter, renameMicroflowParameter } from "../schema/utils";
import { addParameter as addParameterInternal, refreshDerivedState } from "./authoring-operations";

export function addParameter(schema: MicroflowSchema, parameter: MicroflowParameter, position: { x: number; y: number }): MicroflowSchema {
  return addParameterInternal(schema, parameter, position);
}

export function renameParameter(schema: MicroflowSchema, parameterId: string, nextName: string, options: { rewriteExpressions?: boolean } = {}): MicroflowSchema {
  return refreshDerivedState(renameMicroflowParameter(schema, parameterId, nextName, options));
}

export function deleteParameter(schema: MicroflowSchema, parameterId: string): MicroflowSchema {
  return refreshDerivedState(removeMicroflowParameter(schema, parameterId));
}
