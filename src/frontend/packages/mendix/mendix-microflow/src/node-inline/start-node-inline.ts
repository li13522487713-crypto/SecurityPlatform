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
        fields: input.schema.parameters.map((param, index) => ({
          id: param.id,
          label: param.name,
          value: param.defaultValue?.raw ?? "",
          displayValue: `${param.name}: ${param.dataType.kind}`,
          fieldPath: `parameters.${index}.defaultValue.raw`,
          editType: "expression",
          required: param.required,
          placeholder: "输入默认值",
          options: expressionOptions,
        })),
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
