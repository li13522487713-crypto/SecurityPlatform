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
  const normalizedFieldPath = normalizeFieldPath(fieldPath);
  return validationIssues.filter(issue => {
    const issueFieldPath = issue.fieldPath ? normalizeFieldPath(issue.fieldPath) : undefined;
    return issueFieldPath === normalizedFieldPath || issueFieldPath?.endsWith(`.${normalizedFieldPath}`);
  });
}

function normalizeFieldPath(fieldPath: string): string {
  return fieldPath.replace(/\[(\d+)\]/gu, ".$1");
}

export function countIssuesBySeverity(validationIssues: MicroflowValidationIssue[]): { errors: number; warnings: number; infos: number } {
  return {
    errors: validationIssues.filter(issue => issue.severity === "error").length,
    warnings: validationIssues.filter(issue => issue.severity === "warning").length,
    infos: validationIssues.filter(issue => issue.severity === "info").length,
  };
}
