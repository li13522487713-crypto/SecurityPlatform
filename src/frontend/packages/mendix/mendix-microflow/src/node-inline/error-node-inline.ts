import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

export function deriveErrorNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const base = createDefaultInlineConfig(input);
  const variableNameOptions = buildNodeInlineVariableOptions({
    schema: input.schema,
    node: input.node,
    runtimeFrame: input.runtimeFrame,
    mode: "name",
  });
  const data = (input.node.data ?? {}) as Record<string, unknown>;
  const policy = String(data.policy ?? "continue");
  const errorVariable = String(data.customHandlerVariable ?? "$latestError");
  return {
    ...base,
    summaryLines: [
      { id: "title", value: "错误处理", kind: "error" },
      { id: "catch", value: `catch: ${policy}`, kind: "error", editable: true, fieldPath: "data.policy" },
      { id: "out", value: `out: ${errorVariable}`, kind: "error", editable: true, fieldPath: "data.customHandlerVariable" },
    ],
    sections: [
      {
        id: "errors",
        title: "错误处理",
        kind: "errors",
        fields: [
          {
            id: "policy",
            label: "策略",
            value: policy,
            fieldPath: "data.policy",
            editType: "select",
            options: [
              { label: "continue", value: "continue" },
              { label: "rollback", value: "rollback" },
              { label: "custom", value: "custom" },
            ],
          },
          {
            id: "errorVariable",
            label: "错误变量",
            value: errorVariable,
            fieldPath: "data.customHandlerVariable",
            editType: "variable",
            options: variableNameOptions,
          },
        ],
      },
      ...base.sections,
    ],
  };
}
