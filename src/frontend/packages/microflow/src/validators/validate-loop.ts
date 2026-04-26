import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { flattenObjects, issue } from "./shared";

export function validateLoop(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  for (const { object, loopObjectId } of flattenObjects(schema.objectCollection)) {
    if (object.kind === "loopedActivity") {
      if (object.loopSource.kind === "iterableList" && !object.loopSource.listVariableName.trim()) {
        issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "IterableList loopSource requires listVariableName.", { objectId: object.id, fieldPath: "loopSource.listVariableName" }));
      }
      if (object.loopSource.kind === "whileCondition" && !object.loopSource.expression.text?.trim()) {
        issues.push(issue("MF_EXPRESSION_INVALID", "WhileLoopCondition expression is required.", { objectId: object.id, fieldPath: "loopSource.expression" }));
      }
    }
    if ((object.kind === "breakEvent" || object.kind === "continueEvent") && !loopObjectId) {
      issues.push(issue(object.kind === "breakEvent" ? "MF_BREAK_OUTSIDE_LOOP" : "MF_CONTINUE_OUTSIDE_LOOP", "Break/Continue must be inside LoopedActivity.objectCollection.", { objectId: object.id }));
    }
  }
  return issues;
}
