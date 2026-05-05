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
  const headersVar = action?.response.headersVariableName ?? "";
  const timeoutMs = requestData.timeoutMs ?? requestData.timeoutInMs ?? "";
  const errorHandlingData = (actionData.errorHandling ?? {}) as Record<string, unknown>;
  const errorVar = String(errorHandlingData.errorVariableName ?? data.errorVariableName ?? "");
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    { id: "method", value: `${method} ${url || "/"}`, kind: "http", editable: true, fieldPath: "data.action.request.urlExpression.raw" },
    { id: "io", value: `in: ${(action?.request.queryParameters ?? []).map(item => item.key).join(", ") || "-"} · out: ${outputVar || "-"}`, kind: "http" },
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
            id: "query",
            label: "Query",
            value: (action?.request.queryParameters ?? []).map(item => `${item.key}=${expressionText(item.valueExpression)}`).join("\n"),
            fieldPath: "data.action.request.queryParameters",
            editType: "mapping",
            placeholder: "incidentId=$incidentId",
            options: expressionOptions,
          },
          {
            id: "headers",
            label: "Headers",
            value: (action?.request.headers ?? []).map(item => `${item.key}=${expressionText(item.valueExpression)}`).join("\n"),
            fieldPath: "data.action.request.headers",
            editType: "mapping",
            placeholder: "Authorization=Bearer ...",
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
          {
            id: "statusCodeVar",
            label: "状态码变量",
            value: statusCodeVar,
            fieldPath: "data.action.response.statusCodeVariableName",
            editType: "variable",
            options: variableNameOptions,
          },
          {
            id: "headersVar",
            label: "响应头变量",
            value: headersVar,
            fieldPath: "data.action.response.headersVariableName",
            editType: "variable",
            options: variableNameOptions,
          },
          {
            id: "timeout",
            label: "超时(ms)",
            value: String(timeoutMs),
            fieldPath: "data.action.request.timeoutMs",
            editType: "text",
          },
          {
            id: "errorVar",
            label: "错误变量",
            value: errorVar,
            fieldPath: "data.action.errorHandling.errorVariableName",
            editType: "variable",
            options: variableNameOptions,
          },
        ],
      },
      ...base.sections,
    ],
  };
}
