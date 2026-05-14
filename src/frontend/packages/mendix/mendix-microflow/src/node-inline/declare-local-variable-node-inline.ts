import type { MicroflowDeclareLocalVariableAction } from "../schema/types";
import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";
import { expressionText } from "./inline-formatters";

export function deriveDeclareLocalVariableNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
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
  const action = (data.action ?? {}) as MicroflowDeclareLocalVariableAction | Record<string, unknown>;
  const declareAction = action as MicroflowDeclareLocalVariableAction;

  const varName = declareAction.variableName ?? "";
  const scope = declareAction.scope ?? "local";
  const isGlobal = scope === "global";
  const prefix = isGlobal ? "$global." : "$.";
  const displayName = varName ? `${prefix}${varName}` : "未命名变量";
  const dataType = declareAction.dataType?.kind ?? "string";
  const source = declareAction.source ?? "empty";

  let valuePreview = "";
  if (source === "literal") {
    valuePreview = String(declareAction.value ?? "");
  } else if (source === "expression") {
    valuePreview = expressionText(declareAction.expression);
  } else if (source === "reference") {
    valuePreview = `ref: ${declareAction.reference ?? ""}`;
  } else {
    valuePreview = "empty";
  }

  const isConfigured = Boolean(varName);

  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    { id: "out", value: `out: ${displayName}: ${dataType}`, kind: "output" },
    { id: "value", value: `init: ${valuePreview || "empty"}`, kind: "assignment", editable: true, fieldPath: "data.action.value" },
    ...(base.runtime?.outputPreview ? [{ id: "run", value: base.runtime.outputPreview, kind: "runtime" as const }] : []),
  ];

  return {
    ...base,
    summaryLines: summaryLines.slice(0, 3),
    sections: [
      {
        id: "localvar-core",
        title: "变量",
        kind: "variables",
        maxVisibleRows: 4,
        fields: [
          {
            id: "name",
            label: "out",
            value: varName,
            fieldPath: "data.action.variableName",
            editType: "text",
            required: true,
            options: variableNameOptions,
          },
          {
            id: "type",
            label: "type",
            value: dataType,
            fieldPath: "data.action.dataType.kind",
            editType: "select",
            options: ["string", "integer", "boolean", "decimal", "dateTime", "object", "list", "json"].map(kind => ({ label: kind, value: kind })),
          },
          {
            id: "scope",
            label: "scope",
            value: scope,
            fieldPath: "data.action.scope",
            editType: "select",
            options: [{ label: "local ($.) ", value: "local" }, { label: "global ($global.)", value: "global" }],
          },
          {
            id: "value",
            label: "init",
            value: valuePreview,
            fieldPath: source === "expression" ? "data.action.expression.raw" : "data.action.value",
            editType: source === "expression" ? "expression" : "text",
            options: expressionOptions,
          },
        ],
      },
      ...base.sections.filter(section => section.kind === "errors"),
    ],
  };
}
