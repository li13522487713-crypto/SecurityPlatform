import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

export function deriveErrorNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const base = createDefaultInlineConfig(input);
  const data = (input.node.data ?? {}) as Record<string, unknown>;
  const action = (data.action ?? {}) as Record<string, unknown>;
  const policy = String(data.policy ?? action.policy ?? "rollback");
  const errorVariable = String(data.customHandlerVariable ?? action.customHandlerVariable ?? action.errorVariableName ?? "");
  const continueOnError = Boolean(data.continueOnError ?? action.continueOnError ?? false);
  return {
    ...base,
    summaryLines: [
      { id: "title", value: "错误处理", kind: "error" },
      { id: "policy", value: `policy: ${policy}`, kind: "error", editable: true, fieldPath: "data.policy" },
      { id: "out", value: `error var: ${errorVariable || "(none)"}`, kind: "error", editable: true, fieldPath: "data.customHandlerVariable" },
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
            editType: "text",
          },
          {
            id: "continueOnError",
            label: "继续执行",
            value: continueOnError ? "true" : "false",
            fieldPath: "data.continueOnError",
            editType: "select",
            options: [
              { label: "false", value: "false" },
              { label: "true", value: "true" },
            ],
          },
        ],
      },
      ...base.sections,
    ],
  };
}
