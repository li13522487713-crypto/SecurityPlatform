import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { expressionText } from "./inline-formatters";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

function collectActionLeafFields(
  value: unknown,
  path: string,
  acc: Array<{ path: string; value: string; editType: "text" | "json" | "expression" }>,
): void {
  if (value === null || value === undefined) {
    return;
  }
  if (typeof value === "string" || typeof value === "number" || typeof value === "boolean") {
    const text = String(value);
    const looksExpression = text.includes("$") || text.includes("${");
    acc.push({ path, value: text, editType: looksExpression ? "expression" : "text" });
    return;
  }
  if (Array.isArray(value)) {
    if (value.length === 0) {
      return;
    }
    const primitiveOnly = value.every(item => item === null || item === undefined || ["string", "number", "boolean"].includes(typeof item));
    if (primitiveOnly) {
      acc.push({ path, value: value.map(item => String(item ?? "")).join("\n"), editType: "json" });
      return;
    }
    acc.push({ path, value: JSON.stringify(value, null, 2), editType: "json" });
    return;
  }
  if (typeof value === "object") {
    const entries = Object.entries(value as Record<string, unknown>);
    if (entries.length === 1 && entries[0]?.[0] === "raw" && typeof entries[0][1] === "string") {
      acc.push({ path: `${path}.raw`, value: entries[0][1], editType: "expression" });
      return;
    }
    for (const [key, child] of entries) {
      collectActionLeafFields(child, `${path}.${key}`, acc);
    }
  }
}

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
  const knownPaths = new Set([
    "data.action.inputExpression.raw",
    "data.action.inputMappings",
    "data.action.outputVariableName",
    "data.action.errorVariableName",
    "data.action.sideEffectTarget",
  ]);
  const discovered: Array<{ path: string; value: string; editType: "text" | "json" | "expression" }> = [];
  collectActionLeafFields(action, "data.action", discovered);
  const extraFields = discovered
    .filter(item => !knownPaths.has(item.path))
    .slice(0, 32)
    .map((item, index) => ({
      id: `extra-${index}`,
      label: item.path.replace(/^data\.action\./, ""),
      value: item.value,
      fieldPath: item.path,
      editType: item.editType,
      options: item.editType === "expression" ? expressionOptions : undefined,
    }));

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
      ...(extraFields.length > 0 ? [{
        id: "advanced-all-fields",
        title: "完整字段",
        kind: "advanced" as const,
        collapsed: true,
        fields: extraFields,
      }] : []),
      ...base.sections,
    ],
  };
}
