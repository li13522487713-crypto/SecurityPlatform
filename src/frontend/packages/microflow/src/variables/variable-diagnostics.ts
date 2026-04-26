import type { MicroflowValidationIssue } from "../schema/types";

export function variableIssue(
  code: string,
  message: string,
  target: Partial<Pick<MicroflowValidationIssue, "objectId" | "flowId" | "actionId" | "fieldPath">> = {},
  severity: MicroflowValidationIssue["severity"] = "error"
): MicroflowValidationIssue {
  return {
    id: `${code}:${target.objectId ?? target.flowId ?? target.actionId ?? target.fieldPath ?? "variables"}`,
    code,
    message,
    severity,
    ...target
  };
}
