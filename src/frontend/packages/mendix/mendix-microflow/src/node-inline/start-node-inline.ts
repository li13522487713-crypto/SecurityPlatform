import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { nodeDataPath } from "./inline-field-paths";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

export function deriveStartNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const base = createDefaultInlineConfig(input);
  const expressionOptions = buildNodeInlineVariableOptions({
    schema: input.schema,
    node: input.node,
    runtimeFrame: input.runtimeFrame,
    mode: "expression",
  });
  const variableOptions = buildNodeInlineVariableOptions({
    schema: input.schema,
    node: input.node,
    runtimeFrame: input.runtimeFrame,
    mode: "expression",
  });
  const parameterLines = input.schema.parameters.slice(0, 3).map(param => ({
    id: `p-${param.id}`,
    value: `${param.name}: ${param.dataType.kind}`,
    kind: "input" as const,
  }));
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    { id: "start", value: "开始", kind: "text" },
    ...parameterLines,
  ];
  return {
    ...base,
    summaryLines: summaryLines.slice(0, 3),
    sections: [
      {
        id: "inputs",
        title: "输入参数",
        kind: "inputs",
        fields: input.schema.parameters.flatMap((param, index) => ([
          {
            id: `${param.id}-default`,
            label: `${param.name} 默认值`,
            value: param.defaultValue?.raw ?? "",
            displayValue: `${param.name}: ${param.dataType.kind}`,
            fieldPath: `parameters.${index}.defaultValue.raw`,
            editType: "expression" as const,
            required: param.required,
            placeholder: "输入默认值",
            options: expressionOptions,
          },
          {
            id: `${param.id}-name`,
            label: `${param.name} 名称`,
            value: param.name,
            fieldPath: `parameters.${index}.name`,
            editType: "text" as const,
            required: true,
          },
          {
            id: `${param.id}-type`,
            label: `${param.name} 类型`,
            value: param.dataType.kind,
            fieldPath: `parameters.${index}.dataType.kind`,
            editType: "select" as const,
            options: ["string", "boolean", "integer", "decimal", "dateTime", "object", "list"].map(kind => ({ label: kind, value: kind })),
          },
          {
            id: `${param.id}-required`,
            label: `${param.name} 必填`,
            value: param.required ? "true" : "false",
            fieldPath: `parameters.${index}.required`,
            editType: "select" as const,
            options: [{ label: "true", value: "true" }, { label: "false", value: "false" }],
          },
        ])),
      },
      {
        id: "system",
        title: "系统上下文",
        kind: "advanced",
        fields: [
          { id: "ctx-user", label: "currentUser", value: "$currentUser", fieldPath: nodeDataPath("system.currentUser"), editType: "variable", readonly: true, options: variableOptions },
          { id: "ctx-now", label: "now", value: "$now", fieldPath: nodeDataPath("system.now"), editType: "variable", readonly: true, options: variableOptions },
        ],
      },
      ...base.sections,
    ],
  };
}
