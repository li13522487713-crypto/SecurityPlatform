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
    return deriveStartNodeInline(deriveInput);
  }
  if (objectKind === "endEvent") {
    return deriveEndNodeInline(deriveInput);
  }
  if (objectKind === "exclusiveSplit" || objectKind === "inheritanceSplit") {
    return deriveDecisionNodeInline(deriveInput);
  }
  if (objectKind === "loopedActivity" || actionKind === "forEach") {
    return deriveLoopNodeInline(deriveInput);
  }
  if (objectKind === "errorHandler" || actionKind === "errorHandler") {
    return deriveErrorNodeInline(deriveInput);
  }
  if (actionKind === "restCall" || actionKind === "restOperationCall") {
    return deriveRestNodeInline(deriveInput);
  }
  if (actionKind === "callMicroflow") {
    return deriveCallMicroflowNodeInline(deriveInput);
  }
  if (actionKind === "createVariable" || actionKind === "changeVariable") {
    return deriveVariableNodeInline(deriveInput);
  }
  if (actionKind === "completeUserTask" || actionKind === "changeWorkflowState" || objectKind === "tryCatch") {
    return deriveApprovalNodeInline(deriveInput);
  }
  if (objectKind === "actionActivity" || actionKind.length > 0) {
    return deriveActionNodeInline(deriveInput);
  }
  return createDefaultInlineConfig(deriveInput);
}
