import type { MicroflowExpression, MicroflowDataType } from "../schema/types";

export function createMicroflowExpression(raw: string, inferredType?: MicroflowDataType): MicroflowExpression {
  return {
    raw,
    inferredType,
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: [],
  };
}
