import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
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
  const parameterLines = input.schema.parameters.slice(0, 3).map(param => ({
    id: `p-${param.id}`,
    value: `${param.name}: ${param.dataType.kind}`,
    kind: "input" as const,
  }));
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    { id: "start", value: "输入参数", kind: "text" },
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
        maxVisibleRows: 6,
        fields: input.schema.parameters.flatMap((param, index) => ([
          { id: `${param.id}-name`, label: "name", value: param.name, fieldPath: `parameters.${index}.name`, editType: "text" as const, required: true },
          { id: `${param.id}-type`, label: "type", value: param.dataType.kind, fieldPath: `parameters.${index}.dataType.kind`, editType: "select" as const, options: ["string", "boolean", "integer", "decimal", "dateTime", "object", "list"].map(kind => ({ label: kind, value: kind })) },
          { id: `${param.id}-required`, label: "required", value: param.required ? "true" : "false", fieldPath: `parameters.${index}.required`, editType: "select" as const, options: [{ label: "true", value: "true" }, { label: "false", value: "false" }] },
        ])),
      },
      ...base.sections.filter(section => section.kind === "errors"),
    ],
  };
}
