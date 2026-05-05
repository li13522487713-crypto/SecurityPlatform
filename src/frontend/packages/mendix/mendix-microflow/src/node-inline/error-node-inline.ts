import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

export function deriveErrorNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
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
  const policy = String(data.policy ?? "continue");
  const catchType = String(data.catchType ?? "HTTP_ERROR");
  const errorVariable = String(data.customHandlerVariable ?? "$latestError");
  const fallbackResult = String(data.fallbackResultVariable ?? "");
  const fallbackExpression = String(data.fallbackExpression ?? "");
  const rethrow = String(data.rethrow ?? "false");
  return {
    ...base,
    summaryLines: [
      { id: "title", value: "错误处理", kind: "error" },
      { id: "catch", value: `catch: ${catchType}`, kind: "error", editable: true, fieldPath: "data.catchType" },
      { id: "out", value: `out: ${errorVariable}${fallbackResult ? `, ${fallbackResult}` : ""}`, kind: "error", editable: true, fieldPath: "data.customHandlerVariable" },
    ],
    sections: [
      {
        id: "errors",
        title: "错误处理",
        kind: "errors",
        fields: [
          {
            id: "catchType",
            label: "捕获类型",
            value: catchType,
            fieldPath: "data.catchType",
            editType: "text",
          },
          {
            id: "policy",
            label: "策略",
            value: policy,
            fieldPath: "data.policy",
            editType: "select",
            options: [
              { label: "continue", value: "continue" },
              { label: "rollback", value: "rollback" },
              { label: "custom", value: "custom" },
            ],
          },
          {
            id: "errorVariable",
            label: "错误变量",
            value: errorVariable,
            fieldPath: "data.customHandlerVariable",
            editType: "variable",
            options: variableNameOptions,
          },
          {
            id: "fallbackResult",
            label: "Fallback 结果变量",
            value: fallbackResult,
            fieldPath: "data.fallbackResultVariable",
            editType: "variable",
            options: variableNameOptions,
          },
          {
            id: "fallbackExpression",
            label: "Fallback 表达式",
            value: fallbackExpression,
            fieldPath: "data.fallbackExpression",
            editType: "expression",
            options: expressionOptions,
          },
          {
            id: "rethrow",
            label: "继续抛出",
            value: rethrow,
            fieldPath: "data.rethrow",
            editType: "select",
            options: [
              { label: "false", value: "false" },
              { label: "true", value: "true" },
            ],
          },
          { id: "handledBranch", label: "handled 标签", value: String(branchLabels.handled ?? "handled"), fieldPath: "data.branchLabels.handled", editType: "branch" },
          { id: "fallbackBranch", label: "fallback 标签", value: String(branchLabels.fallback ?? "fallback"), fieldPath: "data.branchLabels.fallback", editType: "branch" },
          { id: "rethrowBranch", label: "rethrow 标签", value: String(branchLabels.rethrow ?? "rethrow"), fieldPath: "data.branchLabels.rethrow", editType: "branch" },
        ],
      },
      ...base.sections,
    ],
  };
}
