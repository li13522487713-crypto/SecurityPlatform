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
  const branchLabels = (data.branchLabels ?? {}) as Record<string, unknown>;
  const title = typeof data.title === "string" ? data.title : "人工审批";
  const description = String(data.description ?? "");
  const dueTime = String(data.dueTime ?? data.dueInMinutes ?? "");
  const formFields = Array.isArray(data.formFields) ? data.formFields as Array<Record<string, unknown>> : [];
  const approvalCommentVar = String(data.approvalCommentVariable ?? "");
  const approvedAtVar = String(data.approvedAtVariable ?? "");
  return {
    ...base,
    summaryLines: [
      { id: "title", value: title, kind: "approval" },
      { id: "approver", value: `approver: ${String(data.approver ?? "$manager")}`, kind: "approval", editable: true, fieldPath: "data.approver" },
      { id: "branches", value: `${String(branchLabels.approved ?? "approved")} / ${String(branchLabels.rejected ?? "rejected")} / ${String(branchLabels.timeout ?? "timeout")}`, kind: "branch" },
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
            id: "commentVariable",
            label: "审批备注变量",
            value: approvalCommentVar,
            fieldPath: "data.approvalCommentVariable",
            editType: "variable",
            options: variableNameOptions,
          },
          {
            id: "approvedAtVariable",
            label: "审批时间变量",
            value: approvedAtVar,
            fieldPath: "data.approvedAtVariable",
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
            fieldPath: data.dueTime !== undefined ? "data.dueTime" : "data.dueInMinutes",
            editType: "text",
          },
          {
            id: "formFields",
            label: "表单字段",
            value: formFields.length ? JSON.stringify(formFields, null, 2) : "",
            fieldPath: "data.formFields",
            editType: "json",
            placeholder: "[{\"name\":\"reason\",\"type\":\"text\"}]",
          },
          { id: "approvedBranch", label: "approved 标签", value: String(branchLabels.approved ?? "approved"), fieldPath: "data.branchLabels.approved", editType: "branch" },
          { id: "rejectedBranch", label: "rejected 标签", value: String(branchLabels.rejected ?? "rejected"), fieldPath: "data.branchLabels.rejected", editType: "branch" },
          { id: "timeoutBranch", label: "timeout 标签", value: String(branchLabels.timeout ?? "timeout"), fieldPath: "data.branchLabels.timeout", editType: "branch" },
        ],
      },
      ...base.sections,
    ],
  };
}
