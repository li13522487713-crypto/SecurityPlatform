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
  const data = (input.node.data ?? {}) as Record<string, unknown>;
  const loopSource = data.loopSource as { kind?: string; listVariableName?: string; iteratorVariableName?: string; expression?: unknown } | undefined;
  const listExpr = loopSource?.kind === "iterableList" ? loopSource.listVariableName : expressionText(loopSource?.expression);
  const iterator = loopSource?.iteratorVariableName ?? "item";
  const outgoing = input.schema.workflow.edges.filter(edge => edge.sourceNodeID === input.node.id);
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    { id: "loop", value: `for ${iterator} in ${listExpr || "$list"}`, kind: "loop", editable: true, fieldPath: "data.loopSource.listVariableName" },
    { id: "body", value: `body: ${outgoing.length} edges`, kind: "loop" },
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
        fields: [
          {
            id: "collection",
            label: "集合",
            value: listExpr || "",
            fieldPath: "data.loopSource.listVariableName",
            editType: "variable",
            options: expressionOptions,
          },
          { id: "iterator", label: "迭代变量", value: iterator, fieldPath: "data.loopSource.iteratorVariableName", editType: "text" },
          { id: "index", label: "索引变量", value: String(data.currentIndexVariableName ?? "$currentIndex"), fieldPath: "data.currentIndexVariableName", editType: "text" },
        ],
      },
      ...base.sections,
    ],
  };
}
