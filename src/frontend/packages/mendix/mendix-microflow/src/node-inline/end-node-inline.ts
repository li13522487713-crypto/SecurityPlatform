import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

export function deriveEndNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const base = createDefaultInlineConfig(input);
  const expressionOptions = buildNodeInlineVariableOptions({
    schema: input.schema,
    node: input.node,
    runtimeFrame: input.runtimeFrame,
    mode: "expression",
  });
  const nodeAction =
    (input.node.data as { action?: { returnExpression?: { raw?: string }; returnVariableName?: string } } | undefined)?.action
    ?? ((input.node as unknown as { action?: { returnExpression?: { raw?: string }; returnVariableName?: string } }).action);
  const nodeDataReturnExpression = (input.node.data as { returnExpression?: { raw?: string }; returnVariableName?: string } | undefined);
  const returnExpression = nodeAction?.returnExpression?.raw
    ?? nodeDataReturnExpression?.returnExpression?.raw
    ?? nodeAction?.returnVariableName
    ?? nodeDataReturnExpression?.returnVariableName
    ?? input.schema.returnVariableName
    ?? "";
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    { id: "ret", value: `return: ${returnExpression || "void"}`, kind: "output", editable: true, fieldPath: nodeAction?.returnExpression ? "data.action.returnExpression.raw" : "returnVariableName" },
    { id: "status", value: "status: success", kind: "text" },
    ...(base.runtime?.outputPreview ? [{ id: "out", label: "output", value: base.runtime.outputPreview, kind: "runtime" as const }] : []),
  ];
  return {
    ...base,
    summaryLines: summaryLines.slice(0, 3),
    sections: [
      {
        id: "outputs",
        title: "返回值",
        kind: "outputs",
        maxVisibleRows: 3,
        fields: [
          {
            id: "return-expression",
            label: "return",
            value: returnExpression,
            fieldPath: nodeAction?.returnExpression ? "data.action.returnExpression.raw" : "returnVariableName",
            editType: "expression",
            placeholder: "$result",
            options: expressionOptions,
          },
          {
            id: "return-type",
            label: "type",
            value: input.schema.returnType.kind,
            fieldPath: "returnType.kind",
            editType: "select",
            options: [
              { label: "void", value: "void" },
              { label: "string", value: "string" },
              { label: "boolean", value: "boolean" },
              { label: "integer", value: "integer" },
              { label: "decimal", value: "decimal" },
            ],
          },
        ],
      },
      ...base.sections.filter(section => section.kind === "errors"),
    ],
  };
}
