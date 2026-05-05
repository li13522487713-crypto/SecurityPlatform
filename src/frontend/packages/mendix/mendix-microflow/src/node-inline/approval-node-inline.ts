import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

export function deriveApprovalNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const base = createDefaultInlineConfig(input);
  const expressionOptions = buildNodeInlineVariableOptions({
    schema: input.schema,
    node: input.node,
    runtimeFrame: input.runtimeFrame,
    mode: "expression",
  });
  const variableNameOptions = buildNodeInlineVariableOptions({
    schema: input.schema,
    node: input.node,
    runtimeFrame: input.runtimeFrame,
    mode: "name",
  });
  const data = (input.node.data ?? {}) as Record<string, unknown>;
  const title = typeof data.title === "string" ? data.title : "人工审批";
  return {
    ...base,
    summaryLines: [
      { id: "title", value: title, kind: "approval" },
      { id: "approver", value: `approver: ${String(data.approver ?? "$manager")}`, kind: "approval", editable: true, fieldPath: "data.approver" },
      { id: "branches", value: "approved / rejected / timeout", kind: "branch" },
    ],
    sections: [
      {
        id: "approval",
        title: "审批配置",
        kind: "approval",
        fields: [
          {
            id: "approver",
            label: "审批人",
            value: String(data.approver ?? "$manager"),
            fieldPath: "data.approver",
            editType: "variable",
            options: expressionOptions,
          },
          { id: "label", label: "标题", value: title, fieldPath: "data.title", editType: "text" },
          {
            id: "result",
            label: "结果变量",
            value: String(data.resultVariable ?? "approvalResult"),
            fieldPath: "data.resultVariable",
            editType: "variable",
            options: variableNameOptions,
          },
        ],
      },
      ...base.sections,
    ],
  };
}
