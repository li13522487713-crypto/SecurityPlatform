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
  const data = (input.node.data ?? {}) as Record<string, unknown>;
  const actionData = (data.action ?? {}) as Record<string, unknown>;
  const requestData = (actionData.request ?? {}) as Record<string, unknown>;
  const method = action?.request.method ?? "GET";
  const url = expressionText(action?.request.urlExpression);
  const outputVar = action?.response.handling.kind === "ignore"
    ? ""
    : action?.response.handling.outputVariableName ?? "";
  const statusCodeVar = action?.response.statusCodeVariableName ?? "";
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    { id: "methodUrl", value: `${method} ${url || "/"}`, kind: "http", editable: true, fieldPath: "data.action.request.urlExpression.raw" },
    { id: "in", value: `in: ${(action?.request.queryParameters ?? []).map(item => item.key).join(", ") || "-"}`, kind: "input" },
    { id: "out", value: `out: ${[outputVar, statusCodeVar].filter(Boolean).join(", ") || "-"}`, kind: "output" },
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
        maxVisibleRows: 6,
        fields: [
          {
            id: "method",
            label: "method",
            value: method,
            fieldPath: "data.action.request.method",
            editType: "select",
            options: ["GET", "POST", "PUT", "PATCH", "DELETE"].map(item => ({ label: item, value: item })),
          },
          {
            id: "url",
            label: "url",
            value: url,
            fieldPath: "data.action.request.urlExpression.raw",
            editType: "http",
            required: true,
            options: expressionOptions,
          },
          { id: "body", label: "body", value: action?.request.body.kind ? `JSON · ${action.request.body.kind}` : "body JSON · 0 fields", fieldPath: "data.action.request.body.expression.raw", editType: "json", placeholder: "{ }", options: expressionOptions },
          {
            id: "query",
            label: "in",
            value: (action?.request.queryParameters ?? []).map(item => `${item.key}=${expressionText(item.valueExpression)}`).join("\n"),
            fieldPath: "data.action.request.queryParameters",
            editType: "mapping",
            placeholder: "incidentId=$incidentId",
            options: expressionOptions,
          },
          {
            id: "out",
            label: "out",
            value: outputVar,
            fieldPath: "data.action.response.handling.outputVariableName",
            editType: "variable",
            options: variableNameOptions,
          },
          {
            id: "statusCodeVar",
            label: "status",
            value: statusCodeVar,
            fieldPath: "data.action.response.statusCodeVariableName",
            editType: "variable",
            options: variableNameOptions,
          },
        ],
      },
      ...base.sections.filter(section => section.kind === "errors"),
    ],
  };
}
