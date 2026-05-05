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
  const description = String(data.description ?? "");
  const dueTime = String(data.dueTime ?? data.dueInMinutes ?? "");
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
          {
            id: "label",
            label: "标题",
            value: title,
            fieldPath: "data.title",
            editType: "text",
            placeholder: "审批标题",
          },
          {
            id: "result",
            label: "结果变量",
            value: String(data.resultVariable ?? "approvalResult"),
            fieldPath: "data.resultVariable",
            editType: "variable",
            options: variableNameOptions,
          },
          {
            id: "description",
            label: "说明",
            value: description,
            fieldPath: "data.description",
            editType: "text",
          },
          {
            id: "dueTime",
            label: "到期时间",
            value: dueTime,
            fieldPath: "data.dueTime",
            editType: "text",
          },
          { id: "approvedBranch", label: "approved 标签", value: "approved", fieldPath: "data.branchLabels.approved", editType: "branch" },
          { id: "rejectedBranch", label: "rejected 标签", value: "rejected", fieldPath: "data.branchLabels.rejected", editType: "branch" },
          { id: "timeoutBranch", label: "timeout 标签", value: "timeout", fieldPath: "data.branchLabels.timeout", editType: "branch" },
        ],
      },
      ...base.sections,
    ],
  };
}
