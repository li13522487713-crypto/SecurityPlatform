import type { MicroflowExpression, MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { flattenObjects, issue } from "./shared";

function expressionVariables(expression: MicroflowExpression | undefined): string[] {
  if (!expression) {
    return [];
  }
  return expression.references?.variables ?? expression.referencedVariables ?? [...(expression.text ?? expression.raw ?? "").matchAll(/\$?[A-Za-z_][\w]*/g)].map(match => match[0]);
}

export function validateExpressions(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const available = new Set(Object.values(schema.variableIndex).flatMap(group => Object.values(group).map(symbol => symbol.name)));
  for (const { object, loopObjectId } of flattenObjects(schema.objectCollection)) {
    const expressions: Array<{ expression?: MicroflowExpression; fieldPath: string }> = [];
    if (object.kind === "endEvent") {
      expressions.push({ expression: object.returnValue, fieldPath: "returnValue" });
    }
    if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression") {
      expressions.push({ expression: object.splitCondition.expression, fieldPath: "splitCondition.expression" });
    }
    if (object.kind === "loopedActivity" && object.loopSource.kind === "whileCondition") {
      expressions.push({ expression: object.loopSource.expression, fieldPath: "loopSource.expression" });
    }
    if (object.kind === "actionActivity" && object.action.kind === "restCall") {
      expressions.push({ expression: object.action.request.urlExpression, fieldPath: "action.request.urlExpression" });
    }
    for (const item of expressions) {
      for (const variable of expressionVariables(item.expression)) {
        if (variable === "$currentIndex" && !loopObjectId && object.kind !== "loopedActivity") {
          issues.push(issue("MF_EXPRESSION_INVALID", "$currentIndex is only valid inside Loop.", { objectId: object.id, fieldPath: item.fieldPath }));
        }
        if (variable.startsWith("$latest") && !Object.prototype.hasOwnProperty.call(schema.variableIndex.errorVariables, variable)) {
          issues.push(issue("MF_EXPRESSION_INVALID", `${variable} is only valid inside error handler context.`, { objectId: object.id, fieldPath: item.fieldPath }));
        }
        if (variable.startsWith("$") && !available.has(variable) && !["$currentUser", "$currentIndex"].includes(variable)) {
          issues.push(issue("MF_EXPRESSION_INVALID", `Variable "${variable}" is not in scope.`, { objectId: object.id, fieldPath: item.fieldPath }, "warning"));
        }
      }
    }
  }
  return issues;
}
