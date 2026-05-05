import type { MicroflowChangeVariableAction, MicroflowCreateVariableAction } from "../schema/types";
import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { expressionText } from "./inline-formatters";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

export function deriveVariableNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
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
  const action = data.action as MicroflowCreateVariableAction | MicroflowChangeVariableAction | undefined;
  if (action?.kind === "createVariable") {
    const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
      { id: "out", value: `out: ${action.variableName ?? "var"}: ${action.dataType?.kind ?? "unknown"}`, kind: "output" },
      { id: "init", value: `init: ${expressionText(action.initialValue) || "null"}`, kind: "assignment", editable: true, fieldPath: "data.action.initialValue.raw" },
      ...(base.runtime?.outputPreview ? [{ id: "run", value: base.runtime.outputPreview, kind: "runtime" as const }] : []),
    ];
    return {
      ...base,
      summaryLines,
      sections: [
        {
          id: "variables-core",
          title: "变量",
          kind: "variables",
          maxVisibleRows: 3,
          fields: [
            {
              id: "name",
              label: "out",
              value: action.variableName ?? "",
              fieldPath: "data.action.variableName",
              editType: "text",
              required: true,
              options: variableNameOptions,
            },
            {
              id: "type",
              label: "type",
              value: action.dataType?.kind ?? "",
              fieldPath: "data.action.dataType.kind",
              editType: "select",
              options: [
                "string",
                "boolean",
                "integer",
                "decimal",
                "dateTime",
                "object",
                "list",
              ].map(kind => ({ label: kind, value: kind })),
            },
            {
              id: "initial",
              label: "init",
              value: expressionText(action.initialValue),
              fieldPath: "data.action.initialValue.raw",
              editType: "expression",
              options: expressionOptions,
            },
          ],
        },
        ...base.sections.filter(section => section.kind === "errors"),
      ],
    };
  }
  if (action?.kind !== "changeVariable") {
    return base;
  }
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    { id: "target", value: `set ${action.targetVariableName ?? "var"} = ${expressionText(action.newValueExpression)}`, kind: "assignment", editable: true, fieldPath: "data.action.newValueExpression.raw" },
    ...(base.runtime?.outputPreview ? [{ id: "runtime", value: base.runtime.outputPreview, kind: "runtime" as const }] : []),
  ];
  return {
    ...base,
    summaryLines: summaryLines.slice(0, 3),
    sections: [
      {
        id: "variables-core",
        title: "赋值",
        kind: "variables",
        maxVisibleRows: 3,
        fields: [
          {
            id: "target",
            label: "set",
            value: action.targetVariableName ?? "",
            fieldPath: "data.action.targetVariableName",
            editType: "variable",
            required: true,
            options: variableNameOptions,
          },
          {
            id: "expr",
            label: "value",
            value: expressionText(action.newValueExpression),
            fieldPath: "data.action.newValueExpression.raw",
            editType: "assignment",
            required: true,
            options: expressionOptions,
          },
        ],
      },
      ...base.sections.filter(section => section.kind === "errors"),
    ],
  };
}
