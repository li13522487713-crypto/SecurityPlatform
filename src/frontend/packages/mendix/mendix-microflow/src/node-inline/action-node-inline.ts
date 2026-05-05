import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { expressionText } from "./inline-formatters";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

export function deriveActionNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const base = createDefaultInlineConfig(input);
  const data = (input.node.data ?? {}) as Record<string, unknown>;
  const actionKind = String(data.actionKind ?? (data.action as { kind?: string } | undefined)?.kind ?? "action");
  const action = (data.action ?? {}) as Record<string, unknown>;

  const inputExpr = expressionText(action.inputExpression) || expressionText(action.valueExpression) || "";
  const outputVar = String(action.outputVariableName ?? action.resultVariableName ?? "");
  const sideEffect = String(action.sideEffectTarget ?? "");
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

  return {
    ...base,
    summaryLines: [
      { id: "title", value: String(data.title ?? actionKind), kind: "text" },
      { id: "input", value: `in: ${inputExpr || "-"}`, kind: "input", editable: Boolean(inputExpr), fieldPath: "data.action.inputExpression.raw" },
      { id: "output", value: `out: ${outputVar || "-"}`, kind: "output", editable: true, fieldPath: "data.action.outputVariableName" },
    ],
    sections: [
      {
        id: "inputs",
        title: "输入",
        kind: "inputs",
        fields: [
          {
            id: "inputExpression",
            label: "输入表达式",
            value: inputExpr,
            fieldPath: "data.action.inputExpression.raw",
            editType: "expression",
            placeholder: "$value",
            options: expressionOptions,
          },
        ],
      },
      {
        id: "outputs",
        title: "输出",
        kind: "outputs",
        fields: [
          {
            id: "outputVariable",
            label: "输出变量",
            value: outputVar,
            fieldPath: "data.action.outputVariableName",
            editType: "variable",
            placeholder: "result",
            options: variableNameOptions,
          },
        ],
      },
      {
        id: "advanced-action",
        title: "副作用",
        kind: "advanced",
        collapsed: true,
        fields: [
          {
            id: "sideEffect",
            label: "副作用目标",
            value: sideEffect,
            fieldPath: "data.action.sideEffectTarget",
            editType: "text",
            placeholder: "target",
            options: variableNameOptions,
          },
        ],
      },
      ...base.sections,
    ],
  };
}
