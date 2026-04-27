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
  const names = new Map<string, string>();
  for (const parameter of schema.parameters) {
    const trimmed = parameter.name.trim();
    const normalized = trimmed.toLocaleLowerCase();
    if (!trimmed) {
      issues.push(issue("MF_PARAMETER_NAME_MISSING", "Parameter name is required.", { fieldPath: `parameters.${parameter.id}.name`, parameterId: parameter.id }));
      continue;
    }
    if (names.has(normalized)) {
      issues.push(issue("MF_PARAMETER_DUPLICATED", `Parameter "${parameter.name}" duplicates "${names.get(normalized)}".`, { fieldPath: `parameters.${parameter.id}.name`, parameterId: parameter.id }));
    }
    names.set(normalized, parameter.name);
  }
  return issues;
}
