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
  const resultsVar = String(data.resultsVariableName ?? data.resultCollectionVariableName ?? "");
  const outgoing = input.schema.workflow.edges.filter(edge => edge.sourceNodeID === input.node.id);
  const summaryLines: MicroflowNodeInlineConfig["summaryLines"] = [
    { id: "loop", value: `for ${iterator} in ${listExpr || "$list"}`, kind: "loop", editable: true, fieldPath: loopSource?.kind === "iterableExpression" ? "data.loopSource.expression.raw" : "data.loopSource.listVariableName" },
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
            fieldPath: loopSource?.kind === "iterableExpression" ? "data.loopSource.expression.raw" : "data.loopSource.listVariableName",
            editType: "variable",
            options: expressionOptions,
          },
          {
            id: "iterator",
            label: "迭代变量",
            value: iterator,
            fieldPath: "data.loopSource.iteratorVariableName",
            editType: "text",
            options: variableNameOptions,
          },
          {
            id: "index",
            label: "索引变量",
            value: indexName,
            fieldPath: "data.currentIndexVariableName",
            editType: "text",
            options: variableNameOptions,
          },
          {
            id: "results",
            label: "结果收集变量",
            value: resultsVar,
            fieldPath: data.resultsVariableName !== undefined ? "data.resultsVariableName" : "data.resultCollectionVariableName",
            editType: "variable",
            options: variableNameOptions,
          },
          {
            id: "loopCondition",
            label: "循环条件",
            value: String(data.loopCondition ?? ""),
            fieldPath: "data.loopCondition",
            editType: "condition",
            options: expressionOptions,
          },
          {
            id: "branchBody",
            label: "body 标签",
            value: String(branchLabels.body ?? "body"),
            fieldPath: "data.branchLabels.body",
            editType: "branch",
          },
          {
            id: "branchDone",
            label: "done 标签",
            value: String(branchLabels.done ?? "done"),
            fieldPath: "data.branchLabels.done",
            editType: "branch",
          },
          {
            id: "branchBreak",
            label: "break 标签",
            value: String(branchLabels.break ?? "break"),
            fieldPath: "data.branchLabels.break",
            editType: "branch",
          },
          {
            id: "branchContinue",
            label: "continue 标签",
            value: String(branchLabels.continue ?? "continue"),
            fieldPath: "data.branchLabels.continue",
            editType: "branch",
          },
        ],
      },
      ...base.sections,
    ],
  };
}
