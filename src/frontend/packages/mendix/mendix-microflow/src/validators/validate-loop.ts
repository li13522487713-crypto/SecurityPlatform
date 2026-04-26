import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { collectFlowsRecursive } from "../schema/utils/object-utils";
import { flattenObjects, issue } from "./shared";

const variableNamePattern = /^[A-Za-z_][A-Za-z0-9_]*$/;

export function validateLoop(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const objects = flattenObjects(schema.objectCollection);
  for (const { object, loopObjectId, collectionId } of objects) {
    if (object.kind === "loopedActivity") {
      if (!object.objectCollection) {
        issues.push(issue("MF_LOOP_COLLECTION_REQUIRED", "LoopedActivity requires an objectCollection.", { objectId: object.id, fieldPath: "objectCollection", collectionId }));
        continue;
      }
      if (object.loopSource.kind === "iterableList" && !object.loopSource.listVariableName.trim()) {
        issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "IterableList loopSource requires listVariableName.", { objectId: object.id, fieldPath: "loopSource.listVariableName", collectionId }));
      }
      if (object.loopSource.kind === "iterableList" && !object.loopSource.iteratorVariableName.trim()) {
        issues.push(issue("MF_LOOP_ITERATOR_REQUIRED", "IterableList loopSource requires iteratorVariableName.", { objectId: object.id, fieldPath: "loopSource.iteratorVariableName", collectionId }));
      }
      if (object.loopSource.kind === "iterableList" && object.loopSource.iteratorVariableName.trim() && !variableNamePattern.test(object.loopSource.iteratorVariableName)) {
        issues.push(issue("MF_LOOP_ITERATOR_INVALID", "Loop iteratorVariableName must start with a letter or underscore and contain only letters, numbers and underscores.", { objectId: object.id, fieldPath: "loopSource.iteratorVariableName", collectionId }));
      }
      if (object.loopSource.kind === "whileCondition" && !(object.loopSource.expression.raw ?? object.loopSource.expression.text ?? "").trim()) {
        issues.push(issue("MF_EXPRESSION_INVALID", "WhileLoopCondition expression is required.", { objectId: object.id, fieldPath: "loopSource.expression", collectionId }));
      }
      if (object.loopSource.kind === "whileCondition" && object.loopSource.expression.inferredType && object.loopSource.expression.inferredType.kind !== "boolean") {
        issues.push(issue("MF_LOOP_WHILE_BOOLEAN_REQUIRED", "WhileLoopCondition expression must return boolean.", { objectId: object.id, fieldPath: "loopSource.expression", collectionId }));
      }
    }
    if ((object.kind === "breakEvent" || object.kind === "continueEvent") && !loopObjectId) {
      issues.push(issue(object.kind === "breakEvent" ? "MF_BREAK_OUTSIDE_LOOP" : "MF_CONTINUE_OUTSIDE_LOOP", "Break/Continue must be inside LoopedActivity.objectCollection.", { objectId: object.id, collectionId }));
    }
    if ((object.kind === "breakEvent" || object.kind === "continueEvent")) {
      const outgoing = collectFlowsRecursive(schema).filter(flow => flow.originObjectId === object.id);
      if (outgoing.length > 0) {
        issues.push(issue("MF_LOOP_CONTROL_OUTGOING", "Break/Continue events cannot have outgoing flows.", { objectId: object.id, collectionId }));
      }
    }
    if (loopObjectId && (object.kind === "startEvent" || object.kind === "endEvent")) {
      issues.push(issue(object.kind === "startEvent" ? "MF_LOOP_START_FORBIDDEN" : "MF_LOOP_END_FORBIDDEN", "StartEvent and EndEvent cannot be placed inside a Loop body.", { objectId: object.id, collectionId }));
    }
    if (loopObjectId && object.kind === "parameterObject") {
      issues.push(issue("MF_LOOP_PARAMETER_FORBIDDEN", "ParameterObject cannot be placed inside a Loop body.", { objectId: object.id, collectionId }));
    }
  }
  return issues;
}
