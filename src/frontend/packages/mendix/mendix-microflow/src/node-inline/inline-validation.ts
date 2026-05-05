import type { MicroflowValidationIssue } from "../schema/types";

export function nodeIssues(issues: MicroflowValidationIssue[] | undefined, nodeId: string): MicroflowValidationIssue[] {
  if (!issues?.length) {
    return [];
  }
  return issues.filter(item => item.objectId === nodeId || item.nodeId === nodeId);
}

export function hasError(issues: MicroflowValidationIssue[] | undefined): boolean {
  return Boolean(issues?.some(item => item.severity === "error"));
}

export function hasWarning(issues: MicroflowValidationIssue[] | undefined): boolean {
  return Boolean(issues?.some(item => item.severity === "warning"));
}
