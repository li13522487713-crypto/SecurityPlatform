import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { expressionText } from "./inline-formatters";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

export function deriveLoopNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
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
  const loopSource = data.loopSource as { kind?: string; listVariableName?: string; iteratorVariableName?: string; expression?: unknown } | undefined;
  const listExpr = loopSource?.kind === "iterableList" ? loopSource.listVariableName : expressionText(loopSource?.expression);
  const iterator = loopSource?.iteratorVariableName ?? "item";
  const indexName = String(data.currentIndexVariableName ?? "$currentIndex");
  const outgoing = input.schema.workflow.edges.filter(edge => edge.sourceNodeID === input.node.id);
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    { id: "loop", value: `for ${iterator} in ${listExpr || "$list"}`, kind: "loop", editable: true, fieldPath: loopSource?.kind === "iterableExpression" ? "data.loopSource.expression.raw" : "data.loopSource.listVariableName" },
    { id: "body", value: `body: ${outgoing.length} nodes · done ->`, kind: "loop" },
    ...(base.runtime?.outputPreview ? [{ id: "runtime", value: base.runtime.outputPreview, kind: "runtime" as const }] : []),
  ];
  return {
    ...base,
    summaryLines: summaryLines.slice(0, 3),
    sections: [
      {
        id: "loop",
        title: "循环",
        kind: "loop",
        maxVisibleRows: 5,
        fields: [
          {
            id: "collection",
            label: "list",
            value: listExpr || "",
            fieldPath: loopSource?.kind === "iterableExpression" ? "data.loopSource.expression.raw" : "data.loopSource.listVariableName",
            editType: "variable",
            options: expressionOptions,
          },
          {
            id: "iterator",
            label: "item",
            value: iterator,
            fieldPath: "data.loopSource.iteratorVariableName",
            editType: "text",
            options: variableNameOptions,
          },
          {
            id: "index",
            label: "index",
            value: indexName,
            fieldPath: "data.currentIndexVariableName",
            editType: "text",
            options: variableNameOptions,
          },
          {
            id: "branchBody",
            label: "body",
            value: String(branchLabels.body ?? "body"),
            fieldPath: "data.branchLabels.body",
            editType: "branch",
          },
          {
            id: "branchDone",
            label: "done",
            value: String(branchLabels.done ?? "done"),
            fieldPath: "data.branchLabels.done",
            editType: "branch",
          },
        ],
      },
      ...base.sections.filter(section => section.kind === "errors"),
    ],
  };
}
