import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { issue } from "./shared";

export function validateVariables(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const names = new Set<string>();
  for (const group of Object.values(schema.variables)) {
    for (const symbol of Object.values(group)) {
      if (names.has(symbol.name)) {
        issues.push(issue("MF_VARIABLE_DUPLICATED", `Variable "${symbol.name}" is duplicated.`, { fieldPath: "variables" }));
      }
      names.add(symbol.name);
    }
  }
  return issues;
}
