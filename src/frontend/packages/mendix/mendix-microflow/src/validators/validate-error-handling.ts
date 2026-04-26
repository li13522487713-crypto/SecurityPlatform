import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { collectFlowsRecursive } from "../schema/utils/object-utils";
import { objectMap, issue } from "./shared";

export function validateErrorHandling(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const objects = objectMap(schema);
  const errorFlows = collectFlowsRecursive(schema).filter(item => item.kind === "sequence" && item.isErrorHandler);
  for (const flow of errorFlows) {
    const source = objects.get(flow.originObjectId);
    if (source?.kind === "actionActivity") {
      if (source.action.errorHandlingType === "rollback") {
        issues.push(issue("MF_ERROR_HANDLER_ROLLBACK", "rollback must not have error handler flow.", { objectId: source.id, flowId: flow.id, actionId: source.action.id }));
      }
      if (source.action.errorHandlingType === "continue" && source.action.kind !== "callMicroflow") {
        issues.push(issue("MF_ERROR_HANDLER_CONTINUE", "continue is only valid for CallMicroflow or Loop.", { objectId: source.id, flowId: flow.id, actionId: source.action.id }));
      }
    }
    if (source?.kind === "loopedActivity" && source.errorHandlingType === "rollback") {
      issues.push(issue("MF_ERROR_HANDLER_ROLLBACK", "Loop rollback must not have error handler flow.", { objectId: source.id, flowId: flow.id }));
    }
  }
  for (const object of objects.values()) {
    if (object.kind === "actionActivity") {
      const hasErrorFlow = errorFlows.some(flow => flow.originObjectId === object.id);
      if (["customWithRollback", "customWithoutRollback"].includes(object.action.errorHandlingType) && !hasErrorFlow) {
        issues.push(issue("MF_ERROR_HANDLER_MISSING", "Custom error handling requires an error handler SequenceFlow.", { objectId: object.id, actionId: object.action.id }));
      }
      if (object.action.errorHandlingType === "continue" && object.action.kind !== "callMicroflow") {
        issues.push(issue("MF_ERROR_HANDLER_CONTINUE", "continue is only valid for CallMicroflow or Loop.", { objectId: object.id, actionId: object.action.id }));
      }
    }
    if (object.kind === "loopedActivity") {
      const hasErrorFlow = errorFlows.some(flow => flow.originObjectId === object.id);
      if (["customWithRollback", "customWithoutRollback"].includes(object.errorHandlingType) && !hasErrorFlow) {
        issues.push(issue("MF_ERROR_HANDLER_MISSING", "Custom loop error handling requires an error handler SequenceFlow.", { objectId: object.id }));
      }
      if (object.errorHandlingType === "continue" && !hasErrorFlow) {
        issues.push(issue("MF_ERROR_HANDLER_CONTINUE", "Loop continue mode should define an error handler SequenceFlow.", { objectId: object.id }));
      }
    }
  }
  return issues;
}
