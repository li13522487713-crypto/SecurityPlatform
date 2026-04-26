import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { objectMap, issue } from "./shared";

export function validateErrorHandling(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const objects = objectMap(schema);
  for (const flow of schema.flows.filter(item => item.kind === "sequence" && item.isErrorHandler)) {
    const source = objects.get(flow.originObjectId);
    if (source?.kind === "actionActivity") {
      if (source.action.errorHandlingType === "rollback") {
        issues.push(issue("MF_ERROR_HANDLER_ROLLBACK", "rollback must not have error handler flow.", { objectId: source.id, flowId: flow.id, actionId: source.action.id }));
      }
      if (source.action.errorHandlingType === "continue" && source.action.kind !== "callMicroflow") {
        issues.push(issue("MF_ERROR_HANDLER_CONTINUE", "continue is only valid for CallMicroflow or Loop.", { objectId: source.id, flowId: flow.id, actionId: source.action.id }));
      }
    }
  }
  return issues;
}
