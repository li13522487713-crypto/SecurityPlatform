import type { MicroflowCaseValue, MicroflowWorkflowEdgeJSON } from "../schema/types";
import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { expressionText } from "./inline-formatters";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

function branchLabel(caseValues: MicroflowCaseValue[] | undefined): string {
  const first = caseValues?.[0];
  if (!first) {
    return "else";
  }
  if (first.kind === "boolean") {
    return String(first.value);
  }
  if (first.kind === "fallback" || first.kind === "noCase") {
    return "else";
  }
  if (first.kind === "enumeration") {
    return first.value;
  }
  return first.kind;
}

export function deriveDecisionNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const base = createDefaultInlineConfig(input);
  const variableOptions = buildNodeInlineVariableOptions({
    schema: input.schema,
    node: input.node,
    runtimeFrame: input.runtimeFrame,
    mode: "expression",
  });
  const nodeData = (input.node.data ?? {}) as Record<string, unknown>;
  const splitCondition = nodeData.splitCondition as { kind?: string; expression?: unknown } | undefined;
  const rawCondition = splitCondition?.kind === "expression" ? expressionText(splitCondition.expression) : "";
  const outgoing = input.schema.workflow.edges
    .filter(edge => edge.sourceNodeID === input.node.id)
    .map((edge, index) => ({ edge, index }));
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    {
      id: "condition",
      value: rawCondition || "if 条件",
      kind: "condition",
      editable: true,
      fieldPath: "data.splitCondition.expression.raw",
    },
    {
      id: "branch",
      value: outgoing
        .slice(0, 2)
        .map(item => `${branchLabel((item.edge.data as { caseValues?: MicroflowCaseValue[] } | undefined)?.caseValues)} → ${item.edge.targetNodeID}`)
        .join(" · ") || "分支未配置",
      kind: "branch",
    },
    ...(base.runtime?.selectedBranchLabel
      ? [{ id: "runtime", label: "selected", value: base.runtime.selectedBranchLabel, kind: "runtime" as const }]
      : []),
  ];
  return {
    ...base,
    summaryLines: summaryLines.slice(0, 3),
    sections: [
      {
        id: "conditions",
        title: "条件",
        kind: "conditions",
        fields: [{
          id: "condition-expression",
          label: "if",
          value: rawCondition,
          fieldPath: "data.splitCondition.expression.raw",
          editType: "condition",
          placeholder: "$riskScore >= 80",
          options: variableOptions,
        }],
      },
      {
        id: "branches",
        title: "分支",
        kind: "branches",
        fields: outgoing.map(item => {
          const edge = item.edge as MicroflowWorkflowEdgeJSON;
          const data = (edge.data ?? {}) as Record<string, unknown>;
          const customLabel = typeof data.label === "string" ? data.label : "";
          const fallback = branchLabel((data.caseValues as MicroflowCaseValue[] | undefined));
          return {
            id: String(data.flowId ?? edge.id ?? item.index),
            label: fallback,
            value: customLabel || fallback,
            fieldPath: `edge:${String(data.flowId ?? edge.id)}.data.label`,
            editType: "branch" as const,
          };
        }),
      },
      ...base.sections,
    ],
  };
}
