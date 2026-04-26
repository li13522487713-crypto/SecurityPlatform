import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { issue } from "./shared";
import type { MicroflowValidatorContext } from "./validator-types";

export function validateRoot(schema: MicroflowSchema, _context: MicroflowValidatorContext): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  if (!schema.objectCollection) {
    issues.push(issue("MF_OBJECT_COLLECTION_MISSING", "Microflow must have objectCollection.", { fieldPath: "objectCollection" }));
  }
  if (!Array.isArray(schema.flows)) {
    issues.push(issue("MF_FLOWS_MISSING", "Microflow must have flows array.", { fieldPath: "flows" }));
  }
  const names = new Set<string>();
  for (const parameter of schema.parameters) {
    const normalized = parameter.name.trim();
    if (names.has(normalized)) {
      issues.push(issue("MF_PARAMETER_DUPLICATED", `Parameter "${parameter.name}" is duplicated.`, { fieldPath: "parameters" }));
    }
    names.add(normalized);
  }
  return issues;
}
