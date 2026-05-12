import type { MicroflowCaseValue, MicroflowWorkflowEdgeJSON } from "../schema/types";
import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { expressionText } from "./inline-formatters";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

function branchLabel(caseValues: MicroflowCaseValue[] | undefined, objectKind?: string): string {
  const first = caseValues?.[0];
  if (!first) {
    return "else";
  }
  if (first.kind === "boolean") {
    return String(first.value);
  }
  if (first.kind === "fallback") {
    return objectKind === "inheritanceSplit" ? "(empty)" : "else";
  }
  if (first.kind === "empty" || first.kind === "noCase") {
    return "(empty)";
  }
  if (first.kind === "enumeration") {
    return first.value;
  }
  return first.kind;
}

function parseSimpleExpression(raw: string): { field: string; operator: string; value: string } | undefined {
  const matched = raw.trim().match(/^\$?([A-Za-z_]\w*)\s*(==|=|!=|>=|<=|>|<|contains)\s*(.+)$/);
  if (!matched) {
    return undefined;
  }
  const [, field, operator, value] = matched;
  return { field, operator: operator === "==" ? "=" : operator, value: value.trim() };
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
  const objectKind = String(nodeData.objectKind ?? input.node.type ?? "");
  const splitCondition = nodeData.splitCondition as { kind?: string; expression?: unknown } | undefined;
  const rawCondition = splitCondition?.kind === "expression" ? expressionText(splitCondition.expression) : "";
  const logic = String(nodeData.logic ?? "AND");
  const rawExpression = String(nodeData.rawExpression ?? "");
  const outgoing = input.schema.workflow.edges
    .filter(edge => edge.sourceNodeID === input.node.id)
    .map((edge, index) => ({ edge, index }));
  const parsed = parseSimpleExpression(rawCondition);
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    {
      id: "condition",
      value: parsed ? `if ${parsed.field} ${parsed.operator} ${parsed.value}` : (rawCondition ? `if ${rawCondition}` : "if 条件"),
      kind: "condition",
      editable: true,
      fieldPath: "data.splitCondition.expression.raw",
    },
    {
      id: "branch",
      value: outgoing
        .slice(0, 2)
        .map(item => `${branchLabel((item.edge.data as { caseValues?: MicroflowCaseValue[] } | undefined)?.caseValues, objectKind)} → ${item.edge.targetNodeID}`)
        .join(" · ") || "分支未配置",
      kind: "branch",
    },
    ...(outgoing.length > 2 ? [{ id: "branch-more", value: `+${outgoing.length - 2} more`, kind: "branch" as const }] : []),
    ...(base.runtime?.selectedBranchLabel
      ? [{ id: "runtime", label: "selected", value: base.runtime.selectedBranchLabel, kind: "runtime" as const }]
      : []),
  ];
  const shouldShowCodeMode = !parsed && Boolean(rawExpression.trim());
  return {
    ...base,
    summaryLines: summaryLines.slice(0, 3),
    sections: [
      {
        id: "conditions",
        title: "条件",
        kind: "conditions",
        maxVisibleRows: 2,
        fields: [
          {
            id: "condition-expression",
            label: "if",
            value: rawCondition,
            fieldPath: "data.splitCondition.expression.raw",
            editType: "condition",
            placeholder: "$riskScore >= 80",
            options: variableOptions,
          },
          { id: "condition-logic", label: "逻辑", value: logic, fieldPath: "data.logic", editType: "select", options: [{ label: "AND", value: "AND" }, { label: "OR", value: "OR" }] },
          ...(shouldShowCodeMode
            ? [{
              id: "raw-expression",
              label: "代码模式",
              value: rawExpression,
              fieldPath: "data.rawExpression",
              editType: "expression" as const,
              options: variableOptions,
            }]
            : []),
        ],
      },
      {
        id: "branches",
        title: "分支",
        kind: "branches",
        maxVisibleRows: 3,
        fields: outgoing.map(item => {
          const edge = item.edge as MicroflowWorkflowEdgeJSON;
          const data = (edge.data ?? {}) as Record<string, unknown>;
          const customLabel = typeof data.label === "string" ? data.label : "";
          const fallback = branchLabel((data.caseValues as MicroflowCaseValue[] | undefined), objectKind);
          return {
            id: String(data.flowId ?? edge.id ?? item.index),
            label: fallback,
            value: customLabel || fallback,
            fieldPath: `edge:${String(data.flowId ?? edge.id)}.data.label`,
            editType: "branch" as const,
          };
        }),
      },
      ...base.sections.filter(section => section.kind === "errors"),
    ],
  };
}
