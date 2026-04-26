import type { MicroflowValidationIssue } from "../../schema";

export type FlowGramValidationState = "valid" | "warning" | "error";

export function validationStateFromIssues(issues: MicroflowValidationIssue[]): FlowGramValidationState {
  if (issues.some(issue => issue.severity === "error")) {
    return "error";
  }
  return issues.length > 0 ? "warning" : "valid";
}

export function issuesForFlowGramEntity(issues: MicroflowValidationIssue[], id: string): MicroflowValidationIssue[] {
  return issues.filter(issue => issue.objectId === id || issue.nodeId === id || issue.flowId === id || issue.edgeId === id);
}

