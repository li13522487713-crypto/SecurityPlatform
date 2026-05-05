import type { MicroflowRestCallAction } from "../schema/types";
import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { expressionText } from "./inline-formatters";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

export function deriveRestNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
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
  const action = ((input.node.data ?? {}) as { action?: MicroflowRestCallAction }).action;
  const method = action?.request.method ?? "GET";
  const url = expressionText(action?.request.urlExpression);
  const outputVar = action?.response.handling.kind === "ignore"
    ? ""
    : action?.response.handling.outputVariableName ?? "";
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    { id: "method", value: `${method} ${url || "/"}`, kind: "http", editable: true, fieldPath: "data.action.request.urlExpression.raw" },
    { id: "io", value: `in: ${action?.request.queryParameters.map(item => item.key).join(", ") || "-"} · out: ${outputVar || "-"}`, kind: "http" },
    ...(base.runtime?.outputPreview ? [{ id: "status", value: base.runtime.outputPreview, kind: "runtime" as const }] : []),
  ];
  return {
    ...base,
    summaryLines: summaryLines.slice(0, 3),
    sections: [
      {
        id: "http",
        title: "请求",
        kind: "http",
        fields: [
          {
            id: "method",
            label: "Method",
            value: method,
            fieldPath: "data.action.request.method",
            editType: "select",
            options: ["GET", "POST", "PUT", "PATCH", "DELETE"].map(item => ({ label: item, value: item })),
          },
          {
            id: "url",
            label: "URL",
            value: url,
            fieldPath: "data.action.request.urlExpression.raw",
            editType: "http",
            required: true,
            options: expressionOptions,
          },
          {
            id: "body",
            label: "Body",
            value: action?.request.body.kind === "json" || action?.request.body.kind === "text"
              ? expressionText(action.request.body.expression)
              : "",
            fieldPath: "data.action.request.body.expression.raw",
            editType: "json",
            placeholder: "{ }",
            options: expressionOptions,
          },
          {
            id: "out",
            label: "输出变量",
            value: outputVar,
            fieldPath: "data.action.response.handling.outputVariableName",
            editType: "variable",
            options: variableNameOptions,
          },
        ],
      },
      ...base.sections,
    ],
  };
}
