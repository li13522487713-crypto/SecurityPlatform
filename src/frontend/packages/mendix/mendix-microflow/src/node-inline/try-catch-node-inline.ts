import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

export function deriveTryCatchNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const base = createDefaultInlineConfig(input);
  const data = (input.node.data ?? {}) as Record<string, unknown>;
  const tryBranchKey = String(data.tryBranchKey ?? "try");
  const catchBranchKey = String(data.catchBranchKey ?? "catch");
  const finallyBranchKey = String(data.finallyBranchKey ?? "");
  const errorVariableName = String(data.errorVariableName ?? "latestError");

  return {
    ...base,
    summaryLines: [
      { id: "title", value: "异常捕获", kind: "error" },
      { id: "try", value: `try: ${tryBranchKey}`, kind: "branch", editable: true, fieldPath: "data.tryBranchKey" },
      { id: "catch", value: `catch: ${catchBranchKey}`, kind: "branch", editable: true, fieldPath: "data.catchBranchKey" },
      { id: "errorVariable", value: `error var: ${errorVariableName}`, kind: "error", editable: true, fieldPath: "data.errorVariableName" },
    ],
    sections: [
      {
        id: "try-catch",
        title: "异常捕获",
        kind: "errors",
        fields: [
          {
            id: "tryBranchKey",
            label: "Try Branch",
            value: tryBranchKey,
            fieldPath: "data.tryBranchKey",
            editType: "text",
          },
          {
            id: "catchBranchKey",
            label: "Catch Branch",
            value: catchBranchKey,
            fieldPath: "data.catchBranchKey",
            editType: "text",
          },
          {
            id: "finallyBranchKey",
            label: "Finally Branch",
            value: finallyBranchKey,
            fieldPath: "data.finallyBranchKey",
            editType: "text",
          },
          {
            id: "errorVariableName",
            label: "Error Variable",
            value: errorVariableName,
            fieldPath: "data.errorVariableName",
            editType: "variable",
          },
        ],
      },
      ...base.sections,
    ],
  };
}
