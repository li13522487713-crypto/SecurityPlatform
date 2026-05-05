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
  const mappingText = Array.isArray(action.inputMappings)
    ? (action.inputMappings as Array<{ name?: string; valueExpression?: { raw?: string } }>).map(item => `${item.name ?? ""}=${item.valueExpression?.raw ?? ""}`).join("\n")
    : "";
  const outputVar = String(action.outputVariableName ?? action.resultVariableName ?? "");
  const errorVar = String(action.errorVariableName ?? "");
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
          {
            id: "inputMapping",
            label: "输入映射",
            value: mappingText,
            fieldPath: "data.action.inputMappings",
            editType: "mapping",
            placeholder: "fieldA=$valueA",
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
          {
            id: "errorVariable",
            label: "错误变量",
            value: errorVar,
            fieldPath: "data.action.errorVariableName",
            editType: "variable",
            options: variableNameOptions,
          },
        ],
      },
      ...base.sections.filter(section => section.kind === "errors"),
    ],
  };
}
