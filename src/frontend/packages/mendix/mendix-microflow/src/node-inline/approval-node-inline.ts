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
  return {
    ...base,
    summaryLines: [
      { id: "title", value: title, kind: "approval" },
      { id: "approver", value: `approver: ${String(data.approver ?? "$manager")}`, kind: "approval", editable: true, fieldPath: "data.approver" },
      { id: "branches", value: `${String(branchLabels.approved ?? "approved")} / ${String(branchLabels.rejected ?? "rejected")} / ${String(branchLabels.timeout ?? "timeout")}`, kind: "branch" },
      ...(base.runtime?.outputPreview ? [{ id: "run", value: base.runtime.outputPreview, kind: "runtime" as const }] : []),
    ],
    sections: [
      {
        id: "approval",
        title: "审批配置",
        kind: "approval",
        maxVisibleRows: 6,
        fields: [
          {
            id: "approver",
            label: "approver",
            value: String(data.approver ?? "$manager"),
            fieldPath: "data.approver",
            editType: "variable",
            options: expressionOptions,
          },
          {
            id: "label",
            label: "title",
            value: title,
            fieldPath: "data.title",
            editType: "text",
            placeholder: "审批标题",
          },
          { id: "result", label: "result", value: String(data.resultVariable ?? "approvalResult"), fieldPath: "data.resultVariable", editType: "variable", options: variableNameOptions },
          { id: "approvedBranch", label: "approved", value: String(branchLabels.approved ?? "approved"), fieldPath: "data.branchLabels.approved", editType: "branch" },
          { id: "rejectedBranch", label: "rejected", value: String(branchLabels.rejected ?? "rejected"), fieldPath: "data.branchLabels.rejected", editType: "branch" },
          { id: "timeoutBranch", label: "timeout", value: String(branchLabels.timeout ?? "timeout"), fieldPath: "data.branchLabels.timeout", editType: "branch" },
        ],
      },
      ...base.sections.filter(section => section.kind === "errors"),
    ],
  };
}
