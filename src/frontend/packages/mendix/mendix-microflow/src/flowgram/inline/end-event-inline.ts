import { expressionRaw } from "../../expressions/expression-utils";
import { updateEndEventConfig } from "../../property-panel/utils/schema-patch";
import type { MicroflowAuthoringSchema, MicroflowDataType, MicroflowEndEvent, MicroflowExpression } from "../../schema";
import type { FlowGramMicroflowNodeData } from "../FlowGramMicroflowTypes";
import type { InlineEditorDraft } from "./useInlineEditorDraft";

function createExpression(raw: string, inferredType?: MicroflowDataType): MicroflowExpression {
  return {
    raw,
    inferredType,
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: [],
  };
}

export function buildEndEventDraft(data: FlowGramMicroflowNodeData): InlineEditorDraft {
  return {
    returnExpression: expressionRaw(data.returnValue as never),
  };
}

export function applyEndEventDraft(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  draft: InlineEditorDraft,
): MicroflowAuthoringSchema {
  const raw = String(draft.returnExpression ?? "").trim();
  return updateEndEventConfig(schema, objectId, {
    returnValue: raw ? createExpression(raw, schema.returnType) : undefined,
  } as Partial<MicroflowEndEvent>);
}
