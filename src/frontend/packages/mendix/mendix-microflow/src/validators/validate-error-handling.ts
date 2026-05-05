import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { collectFlowsRecursive } from "../schema/utils/object-utils";
import { objectMap, issue } from "./shared";
import type { MicroflowValidatorContext } from "./validator-types";

export function getErrorHandlerFlowForObject(schema: MicroflowSchema, objectId: string) {
  return collectFlowsRecursive(schema).find(flow => flow.kind === "sequence" && flow.isErrorHandler && flow.originObjectId === objectId);
}

export function hasErrorHandlerFlow(schema: MicroflowSchema, objectId: string): boolean {
  return Boolean(getErrorHandlerFlowForObject(schema, objectId));
}

function supportsErrorHandlerSource(objectKind: string | undefined): boolean {
  return Boolean(objectKind && ["actionActivity", "loopedActivity", "exclusiveSplit", "inheritanceSplit"].includes(objectKind));
}

function supportsContinueErrorHandling(kind: string): boolean {
  return kind === "callMicroflow" || kind === "restCall" || kind === "loopedActivity";
}

export function validateErrorHandling(schema: MicroflowSchema, _context: MicroflowValidatorContext): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const objects = objectMap(schema);
  const errorFlows = collectFlowsRecursive(schema).filter(item => item.kind === "sequence" && item.isErrorHandler);
  for (const flow of errorFlows) {
    const source = objects.get(flow.originObjectId);
    const target = objects.get(flow.destinationObjectId);
    if (!supportsErrorHandlerSource(source?.kind)) {
      issues.push(issue("MF_ERROR_HANDLER_SOURCE_UNSUPPORTED", "Error handler flow source must support error handling.", { objectId: source?.id, flowId: flow.id }));
    }
    if (!target || target.kind === "annotation" || target.kind === "parameterObject" || target.kind === "startEvent") {
      issues.push(issue("MF_ERROR_HANDLER_TARGET_INVALID", "Error handler flow target must be an executable object.", { objectId: source?.id, flowId: flow.id }));
    }
    if (source?.kind === "actionActivity") {
      if (source.action.errorHandlingType === "rollback") {
        issues.push(issue("MF_ERROR_HANDLER_ROLLBACK_HAS_FLOW", "rollback must not have error handler flow.", { objectId: source.id, flowId: flow.id, actionId: source.action.id }));
      }
      if (source.action.errorHandlingType === "continue" && !supportsContinueErrorHandling(source.action.kind)) {
        issues.push(issue("MF_ERROR_HANDLER_CONTINUE_NOT_ALLOWED", "continue is only valid for CallMicroflow, RestCall or Loop.", { objectId: source.id, flowId: flow.id, actionId: source.action.id }));
      }
    }
    if (source?.kind === "loopedActivity" && source.errorHandlingType === "rollback") {
      issues.push(issue("MF_ERROR_HANDLER_ROLLBACK_HAS_FLOW", "Loop rollback must not have error handler flow.", { objectId: source.id, flowId: flow.id }));
    }
  }
  for (const object of objects.values()) {
    const outgoingErrorFlows = errorFlows.filter(flow => flow.originObjectId === object.id);
    if (outgoingErrorFlows.length > 1) {
      issues.push(issue("MF_ERROR_HANDLER_DUPLICATED", "Each object can define at most one error handler flow.", { objectId: object.id, relatedFlowIds: outgoingErrorFlows.map(flow => flow.id) }));
    }
    if (object.kind === "actionActivity") {
      const hasErrorFlow = outgoingErrorFlows.length > 0;
      if (["customWithRollback", "customWithoutRollback"].includes(object.action.errorHandlingType) && !hasErrorFlow) {
        issues.push(issue("MF_ERROR_HANDLER_WITH_ROLLBACK_MISSING_FLOW", "Custom error handling requires an error handler SequenceFlow.", { objectId: object.id, actionId: object.action.id }));
      }
      if (object.action.errorHandlingType === "continue" && !supportsContinueErrorHandling(object.action.kind)) {
        issues.push(issue("MF_ERROR_HANDLER_CONTINUE_NOT_ALLOWED", "continue is only valid for CallMicroflow, RestCall or Loop.", { objectId: object.id, actionId: object.action.id }));
      }
    }
    if (object.kind === "loopedActivity") {
      const hasErrorFlow = outgoingErrorFlows.length > 0;
      if (["customWithRollback", "customWithoutRollback"].includes(object.errorHandlingType) && !hasErrorFlow) {
        issues.push(issue("MF_ERROR_HANDLER_WITH_ROLLBACK_MISSING_FLOW", "Custom loop error handling requires an error handler SequenceFlow.", { objectId: object.id }));
      }
    }
  }
  return issues;
}
