import type { MicroflowActionKind } from "../schema/types";

export type MicroflowRuntimeSupportLevel =
  | "supported"
  | "modeledOnly"
  | "unsupported"
  | "requiresConnector"
  | "nanoflowOnly"
  | "deprecated";

export type MicroflowUnsupportedActionReason =
  | "unsupported"
  | "modeledOnly"
  | "requiresConnector"
  | "deprecated"
  | "nanoflowOnly"
  | "notImplemented";

export const MICROFLOW_P0_ACTION_KINDS: ReadonlySet<MicroflowActionKind> = new Set<MicroflowActionKind>([
  "retrieve",
  "createObject",
  "changeMembers",
  "commit",
  "delete",
  "rollback",
  "cast",
  "createList",
  "changeList",
  "aggregateList",
  "listOperation",
  "filterList",
  "sortList",
  "counter",
  "incrementCounter",
  "gauge",
  "createVariable",
  "changeVariable",
  "callMicroflow",
  "restCall",
  "logMessage",
  "throwException",
]);

const MICROFLOW_RUNTIME_COMMAND_ACTION_KINDS: ReadonlySet<MicroflowActionKind> = new Set<MicroflowActionKind>([
  "showPage",
  "showHomePage",
  "showMessage",
  "closePage",
  "validationFeedback",
  "downloadFile",
  "callJavaScriptAction",
  "callNanoflow",
  "synchronize",
]);

const MICROFLOW_CONNECTOR_REQUIRED_ACTION_KINDS: ReadonlySet<MicroflowActionKind> = new Set<MicroflowActionKind>([
  "callJavaAction",
  "webServiceCall",
  "importXml",
  "exportXml",
  "callExternalAction",
  "restOperationCall",
  "generateDocument",
  "mlModelCall",
  "applyJumpToOption",
  "callWorkflow",
  "changeWorkflowState",
  "completeUserTask",
  "generateJumpToOptions",
  "retrieveWorkflowActivityRecords",
  "retrieveWorkflowContext",
  "retrieveWorkflows",
  "showUserTaskPage",
  "showWorkflowAdminPage",
  "lockWorkflow",
  "unlockWorkflow",
  "notifyWorkflow",
  "deleteExternalObject",
  "sendExternalObject",
]);

export function isP0ActionKind(kind: MicroflowActionKind): boolean {
  return MICROFLOW_P0_ACTION_KINDS.has(kind);
}

export function resolveActionRuntimeSupportLevel(
  actionKind: MicroflowActionKind,
): {
  supportLevel: MicroflowRuntimeSupportLevel;
  reason?: MicroflowUnsupportedActionReason;
  message: string;
} {
  if (MICROFLOW_P0_ACTION_KINDS.has(actionKind)) {
    return { supportLevel: "supported", message: "P0 supported action." };
  }
  if (MICROFLOW_RUNTIME_COMMAND_ACTION_KINDS.has(actionKind)) {
    return { supportLevel: "supported", message: "Runtime emits runtimeCommands for client handling." };
  }
  if (MICROFLOW_CONNECTOR_REQUIRED_ACTION_KINDS.has(actionKind)) {
    const deprecated = actionKind === "generateDocument";
    return {
      supportLevel: deprecated ? "deprecated" : "requiresConnector",
      reason: deprecated ? "deprecated" : "requiresConnector",
      message: deprecated
        ? "Deprecated action requires a configured connector."
        : "Action requires a configured connector capability.",
    };
  }
  return {
    supportLevel: "requiresConnector",
    reason: "requiresConnector",
    message: "Action requires an explicit runtime descriptor or connector capability.",
  };
}
