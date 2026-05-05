import type { MicroflowTraceFrame } from "../debug/trace-types";
import type { MicroflowNodeViewMode, MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import type { MicroflowDesignSchema, MicroflowValidationIssue, MicroflowWorkflowNodeJSON } from "../schema/types";
import { deriveApprovalNodeInline } from "./approval-node-inline";
import { deriveActionNodeInline } from "./action-node-inline";
import { deriveCallMicroflowNodeInline } from "./call-microflow-node-inline";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";
import { deriveDecisionNodeInline } from "./decision-node-inline";
import { deriveEndNodeInline } from "./end-node-inline";
import { deriveErrorNodeInline } from "./error-node-inline";
import { deriveLoopNodeInline } from "./loop-node-inline";
import { deriveRestNodeInline } from "./rest-node-inline";
import { deriveStartNodeInline } from "./start-node-inline";
import { deriveVariableNodeInline } from "./variable-node-inline";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";

function withDefaultVariableOptions(config: MicroflowNodeInlineConfig, deriveInput: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const expressionOptions = buildNodeInlineVariableOptions({
    schema: deriveInput.schema,
    node: deriveInput.node,
    runtimeFrame: deriveInput.runtimeFrame,
    mode: "expression",
  });
  const variableNameOptions = buildNodeInlineVariableOptions({
    schema: deriveInput.schema,
    node: deriveInput.node,
    runtimeFrame: deriveInput.runtimeFrame,
    mode: "name",
  });
  return {
    ...config,
    sections: config.sections.map(section => ({
      ...section,
      fields: section.fields.map(field => {
        if (field.options && field.options.length > 0) {
          return field;
        }
        if (field.editType === "variable" || field.editType === "select" || field.editType === "text") {
          return { ...field, options: variableNameOptions };
        }
        if (
          field.editType === "expression" ||
          field.editType === "condition" ||
          field.editType === "http" ||
          field.editType === "assignment" ||
          field.editType === "mapping" ||
          field.editType === "json"
        ) {
          return { ...field, options: expressionOptions };
        }
        return field;
      }),
    })),
  };
}

export function deriveNodeInlineConfig(input: {
  node: MicroflowWorkflowNodeJSON;
  schema: MicroflowDesignSchema;
  runtimeFrame?: MicroflowTraceFrame;
  issues?: MicroflowValidationIssue[];
  viewMode?: MicroflowNodeViewMode;
}): MicroflowNodeInlineConfig {
  const deriveInput: DeriveNodeInlineInput = {
    node: input.node,
    schema: input.schema,
    runtimeFrame: input.runtimeFrame,
    issues: input.issues,
    viewMode: input.viewMode,
  };
  const data = (input.node.data ?? {}) as Record<string, unknown>;
  const objectKind = String(data.objectKind ?? input.node.type);
  const actionKind = String(data.actionKind ?? (data.action as { kind?: string } | undefined)?.kind ?? "");

  if (objectKind === "startEvent") {
    return withDefaultVariableOptions(deriveStartNodeInline(deriveInput), deriveInput);
  }
  if (objectKind === "endEvent") {
    return withDefaultVariableOptions(deriveEndNodeInline(deriveInput), deriveInput);
  }
  if (objectKind === "exclusiveSplit" || objectKind === "inheritanceSplit") {
    return withDefaultVariableOptions(deriveDecisionNodeInline(deriveInput), deriveInput);
  }
  if (objectKind === "loopedActivity" || actionKind === "forEach") {
    return withDefaultVariableOptions(deriveLoopNodeInline(deriveInput), deriveInput);
  }
  if (objectKind === "errorHandler" || actionKind === "errorHandler") {
    return withDefaultVariableOptions(deriveErrorNodeInline(deriveInput), deriveInput);
  }
  if (actionKind === "restCall" || actionKind === "restOperationCall") {
    return withDefaultVariableOptions(deriveRestNodeInline(deriveInput), deriveInput);
  }
  if (actionKind === "callMicroflow") {
    return withDefaultVariableOptions(deriveCallMicroflowNodeInline(deriveInput), deriveInput);
  }
  if (actionKind === "createVariable" || actionKind === "changeVariable") {
    return withDefaultVariableOptions(deriveVariableNodeInline(deriveInput), deriveInput);
  }
  if (actionKind === "completeUserTask" || actionKind === "changeWorkflowState" || objectKind === "tryCatch") {
    return withDefaultVariableOptions(deriveApprovalNodeInline(deriveInput), deriveInput);
  }
  if (objectKind === "actionActivity" || actionKind.length > 0) {
    return withDefaultVariableOptions(deriveActionNodeInline(deriveInput), deriveInput);
  }
  return withDefaultVariableOptions(createDefaultInlineConfig(deriveInput), deriveInput);
}
