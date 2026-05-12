import type { MicroflowValidationIssue } from "../../schema";

const ISSUE_MESSAGE_MAP: Record<string, string> = {
  MF_VARIABLE_NAME_REQUIRED: "Variable name is required.",
  MF_VARIABLE_NAME_SYSTEM_RESERVED: "Variable names cannot start with '$'.",
  MF_VARIABLE_NAME_INVALID: "Use letters, numbers, and underscores, and do not start with a number.",
  MF_VARIABLE_DUPLICATED: "Variable name already exists in current scope.",
  MF_VARIABLE_PARAMETER_CONFLICT: "Variable name conflicts with a parameter name.",
  MF_VARIABLE_OUTPUT_TYPE_UNKNOWN: "Output variable type is unknown. Select an entity first.",
  MF_VARIABLE_OUTPUT_MODELED_ONLY: "Output variable is modeled-only and might not be available at runtime.",
  MF_VARIABLE_METADATA_ENTITY_NOT_FOUND: "Entity metadata is missing; cannot infer variable type.",
  MF_VARIABLE_METADATA_ASSOCIATION_NOT_FOUND: "Association metadata is missing.",
  MF_VARIABLE_MICROFLOW_RETURN_VOID: "Target microflow returns void. Output variable is ignored.",
  MF_VARIABLE_MICROFLOW_RETURN_UNKNOWN: "Target microflow return type is unknown.",
  MF_VARIABLE_LOOP_ITERATOR_REQUIRED: "Iterator variable name is required.",
  MF_VARIABLE_LOOP_ENTITY_UNKNOWN: "Loop collection entity metadata is missing.",
  MF_VARIABLE_REST_RESPONSE_UNKNOWN: "REST response type is unknown.",
  MF_VARIABLE_TYPE_MISMATCH: "Variable type mismatch.",
};

function issueKey(issue: MicroflowValidationIssue): string {
  return `${issue.code}::${issue.fieldPath ?? ""}`;
}

export function presentIssueMessage(issue: MicroflowValidationIssue): string {
  const mapped = ISSUE_MESSAGE_MAP[issue.code];
  if (mapped) {
    return mapped;
  }
  if (issue.message && !/^MF_[A-Z0-9_]+$/.test(issue.message)) {
    return issue.message;
  }
  if (/^MF_[A-Z0-9_]+$/.test(issue.code)) {
    return "Validation issue on this field.";
  }
  return issue.message || issue.code;
}

export function dedupeIssues(issues: MicroflowValidationIssue[]): MicroflowValidationIssue[] {
  const seen = new Set<string>();
  const result: MicroflowValidationIssue[] = [];
  for (const issue of issues) {
    const key = issueKey(issue);
    if (seen.has(key)) {
      continue;
    }
    seen.add(key);
    result.push(issue);
  }
  return result;
}
