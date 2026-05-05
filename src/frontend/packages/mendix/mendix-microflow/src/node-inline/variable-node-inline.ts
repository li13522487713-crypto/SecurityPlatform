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
      { id: "title", value: "创建变量", kind: "text" },
      { id: "name", value: `${action.variableName ?? "var"}: ${action.dataType?.kind ?? "unknown"}`, kind: "assignment" },
      { id: "init", value: `initial = ${expressionText(action.initialValue) || "null"}`, kind: "assignment", editable: true, fieldPath: "data.action.initialValue.raw" },
    ];
    return {
      ...base,
      summaryLines,
      sections: [
        {
          id: "variables",
          title: "变量",
          kind: "variables",
          fields: [
            { id: "name", label: "名称", value: action.variableName ?? "", fieldPath: "data.action.variableName", editType: "text", required: true },
            {
              id: "type",
              label: "类型",
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
              label: "初始值",
              value: expressionText(action.initialValue),
              fieldPath: "data.action.initialValue.raw",
              editType: "expression",
              options: expressionOptions,
            },
          ],
        },
        ...base.sections,
      ],
    };
  }
  if (action?.kind !== "changeVariable") {
    return base;
  }
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    { id: "title", value: "修改变量", kind: "text" },
    { id: "target", value: `${action.targetVariableName ?? "var"} = ${expressionText(action.newValueExpression)}`, kind: "assignment", editable: true, fieldPath: "data.action.newValueExpression.raw" },
    ...(base.runtime?.outputPreview ? [{ id: "runtime", value: base.runtime.outputPreview, kind: "runtime" as const }] : []),
  ];
  return {
    ...base,
    summaryLines: summaryLines.slice(0, 3),
    sections: [
      {
        id: "variables",
        title: "赋值",
        kind: "variables",
        fields: [
          {
            id: "target",
            label: "目标变量",
            value: action.targetVariableName ?? "",
            fieldPath: "data.action.targetVariableName",
            editType: "variable",
            required: true,
            options: variableNameOptions,
          },
          {
            id: "expr",
            label: "新值",
            value: expressionText(action.newValueExpression),
            fieldPath: "data.action.newValueExpression.raw",
            editType: "assignment",
            required: true,
            options: expressionOptions,
          },
        ],
      },
      ...base.sections,
    ],
  };
}
