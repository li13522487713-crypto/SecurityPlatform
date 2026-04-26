import type { MicroflowExpression, MicroflowSchema, MicroflowValidationIssue, MicroflowVariableIndex } from "../schema/types";
import { flattenObjects, issue } from "./shared";
import { expressionVariables, flattenVariableIndex, isVariableInScope, resolveExpressionScope } from "../variable-index";
import { validateExpression } from "../expressions";

export function validateExpressions(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const variables: MicroflowVariableIndex = schema.variables ?? {
    parameters: {},
    localVariables: {},
    objectOutputs: {},
    listOutputs: {},
    loopVariables: {},
    errorVariables: {},
    systemVariables: {}
  };
  const allSymbols = flattenVariableIndex(variables);
  const symbolByName = new Map(allSymbols.map(symbol => [symbol.name, symbol]));
  for (const { object, loopObjectId } of flattenObjects(schema.objectCollection)) {
    const scope = resolveExpressionScope(schema, object.id);
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
      issues.push(...validateExpression(schema, item.expression, { objectId: object.id, fieldPath: item.fieldPath }));
      for (const variable of expressionVariables(item.expression)) {
        if (variable === "$currentIndex" && !loopObjectId && object.kind !== "loopedActivity") {
          issues.push(issue("MF_EXPRESSION_INVALID", "$currentIndex is only valid inside Loop.", { objectId: object.id, fieldPath: item.fieldPath }));
        }
        if (variable.startsWith("$latest") && !Object.prototype.hasOwnProperty.call(variables.errorVariables, variable)) {
          issues.push(issue("MF_EXPRESSION_INVALID", `${variable} is only valid inside error handler context.`, { objectId: object.id, fieldPath: item.fieldPath }));
        }
        const symbol = symbolByName.get(variable);
        if (variable.startsWith("$") && !symbol && !["$currentUser", "$currentIndex"].includes(variable)) {
          issues.push(issue("MF_EXPRESSION_INVALID", `Variable "${variable}" is not in scope.`, { objectId: object.id, fieldPath: item.fieldPath }, "warning"));
          continue;
        }
        if (symbol && !isVariableInScope(symbol, variable, scope)) {
          issues.push(issue("MF_EXPRESSION_INVALID", `Variable "${variable}" is outside the current control-flow scope.`, { objectId: object.id, fieldPath: item.fieldPath }));
        }
      }
    }
  }
  return issues;
}
