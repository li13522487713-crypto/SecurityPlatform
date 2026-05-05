import type { MicroflowCallMicroflowAction } from "../schema/types";
import type { MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import { expressionText } from "./inline-formatters";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";

export function deriveCallMicroflowNodeInline(input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
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
  const action = ((input.node.data ?? {}) as { action?: MicroflowCallMicroflowAction & { calledMicroflowId?: string; argumentMappings?: Array<{ parameterName: string; valueExpression?: { raw?: string } }>; outputVariableName?: string } }).action;
  const parameterMappings = action?.parameterMappings
    ?? action?.argumentMappings?.map(item => ({
      parameterName: item.parameterName,
      argumentExpression: { raw: item.valueExpression?.raw ?? "" },
    }))
    ?? [];
  const mappingPathPrefix = action?.parameterMappings ? "data.action.parameterMappings" : "data.action.argumentMappings";
  const returnValue = action?.returnValue ?? { outputVariableName: action?.outputVariableName };
  const targetName = action?.targetMicroflowDisplayName ?? action?.targetMicroflowName ?? action?.targetMicroflowId ?? action?.calledMicroflowId ?? "未选择";
  return {
    ...base,
    summaryLines: [
      { id: "target", value: `调用微流 ${targetName}`, kind: "text", editable: true, fieldPath: "data.action.targetMicroflowName" },
      { id: "args", value: `input: ${parameterMappings.map(item => item.parameterName).join(", ") || "-"}`, kind: "input" },
      { id: "ret", value: `out: ${(returnValue as { outputVariableName?: string; resultVariableName?: string }).outputVariableName ?? (returnValue as { outputVariableName?: string; resultVariableName?: string }).resultVariableName ?? "-"}`, kind: "output", editable: true, fieldPath: "data.action.returnValue.outputVariableName" },
    ],
    sections: [
      {
        id: "target",
        title: "目标微流",
        kind: "advanced",
        fields: [
          { id: "targetDisplayName", label: "显示名", value: String(action?.targetMicroflowDisplayName ?? ""), fieldPath: "data.action.targetMicroflowDisplayName", editType: "text" },
          { id: "targetName", label: "名称", value: String(action?.targetMicroflowName ?? ""), fieldPath: "data.action.targetMicroflowName", editType: "text" },
          { id: "targetId", label: "ID", value: String(action?.targetMicroflowId ?? action?.calledMicroflowId ?? ""), fieldPath: "data.action.targetMicroflowId", editType: "text" },
        ],
      },
      {
        id: "inputs",
        title: "参数映射",
        kind: "inputs",
        fields: parameterMappings.map((mapping, index) => ({
          id: mapping.parameterName,
          label: mapping.parameterName,
          value: expressionText(mapping.argumentExpression),
          fieldPath: action?.parameterMappings
            ? `${mappingPathPrefix}.${index}.argumentExpression.raw`
            : `${mappingPathPrefix}.${index}.valueExpression.raw`,
          editType: "mapping",
          options: expressionOptions,
        })),
      },
      {
        id: "outputs",
        title: "返回值",
        kind: "outputs",
        fields: [{
          id: "return-var",
          label: "result",
          value: (returnValue as { outputVariableName?: string; resultVariableName?: string }).outputVariableName ?? (returnValue as { outputVariableName?: string; resultVariableName?: string }).resultVariableName ?? "",
          fieldPath: "data.action.returnValue.outputVariableName",
          editType: "variable",
          options: variableNameOptions,
        }],
      },
      ...base.sections,
    ],
  };
}
