import type { MicroflowValidationIssue } from "../../schema";

export function getIssuesForObject(
  validationIssues: MicroflowValidationIssue[],
  objectId: string,
  actionId?: string,
): MicroflowValidationIssue[] {
  return validationIssues.filter(issue =>
    issue.objectId === objectId ||
    issue.nodeId === objectId ||
    (Boolean(actionId) && issue.actionId === actionId)
  );
}

export function getIssuesForFlow(
  validationIssues: MicroflowValidationIssue[],
  flowId: string,
): MicroflowValidationIssue[] {
  return validationIssues.filter(issue => issue.flowId === flowId || issue.edgeId === flowId);
}

export function getIssuesForAction(
  validationIssues: MicroflowValidationIssue[],
  actionId: string,
): MicroflowValidationIssue[] {
  return validationIssues.filter(issue => issue.actionId === actionId);
}

export function getIssuesForField(
  validationIssues: MicroflowValidationIssue[],
  fieldPath: string,
): MicroflowValidationIssue[] {
  return validationIssues.filter(issue => issue.fieldPath === fieldPath || issue.fieldPath?.endsWith(`.${fieldPath}`));
}

export function countIssuesBySeverity(validationIssues: MicroflowValidationIssue[]): { errors: number; warnings: number; infos: number } {
  return {
    errors: validationIssues.filter(issue => issue.severity === "error").length,
    warnings: validationIssues.filter(issue => issue.severity === "warning").length,
    infos: validationIssues.filter(issue => issue.severity === "info").length,
  };
}
