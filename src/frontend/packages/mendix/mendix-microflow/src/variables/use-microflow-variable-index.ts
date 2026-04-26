import { useMemo } from "react";
import type { MicroflowMetadataCatalog } from "../metadata";
import type { MicroflowSchema } from "../schema/types";
import { buildVariableIndex } from "./variable-index";

export function useMicroflowVariableIndex(schema: MicroflowSchema, metadata: MicroflowMetadataCatalog) {
  const variableIndex = useMemo(() => buildVariableIndex(schema, metadata), [schema, metadata.version]);
  return {
    variableIndex,
    diagnostics: variableIndex.diagnostics ?? [],
    rebuild: () => buildVariableIndex(schema, metadata),
  };
}
