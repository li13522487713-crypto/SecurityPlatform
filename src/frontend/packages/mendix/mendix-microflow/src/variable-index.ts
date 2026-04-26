import type { MicroflowExpression, MicroflowSchema, MicroflowSequenceFlow, MicroflowVariableIndex, MicroflowVariableSymbol } from "./schema/types";
import { flattenObjectCollection } from "./adapters";

export interface ExpressionScopeContext {
  objectId: string;
  loopObjectId?: string;
  activeErrorFlowIds: string[];
}

export function expressionVariables(expression: MicroflowExpression | undefined): string[] {
  if (!expression) {
    return [];
  }
  const raw = expression.raw ?? expression.text ?? "";
  return expression.references?.variables
    ?? expression.referencedVariables
    ?? Array.from(raw.matchAll(/\$[A-Za-z_][\w]*/g)).map(match => match[0]);
}

export function flattenVariableIndex(index: MicroflowVariableIndex): MicroflowVariableSymbol[] {
  return Object.values(index.parameters)
    .concat(Object.values(index.localVariables))
    .concat(Object.values(index.objectOutputs))
    .concat(Object.values(index.listOutputs))
    .concat(Object.values(index.loopVariables))
    .concat(Object.values(index.errorVariables))
    .concat(Object.values(index.systemVariables));
}

export function collectErrorFlowIdsForObject(schema: MicroflowSchema, objectId: string): string[] {
  const flowByOrigin = schema.flows.filter((flow): flow is MicroflowSequenceFlow => flow.kind === "sequence" && flow.isErrorHandler);
  const result = new Set<string>();
  for (const flow of flowByOrigin) {
    if (flow.destinationObjectId === objectId || flow.originObjectId === objectId) {
      result.add(flow.id);
    }
  }
  return [...result];
}

export function resolveExpressionScope(schema: MicroflowSchema, objectId: string): ExpressionScopeContext {
  let loopObjectId: string | undefined;
  const objects = flattenObjectCollection(schema.objectCollection);
  for (const object of objects) {
    if (object.kind === "loopedActivity" && flattenObjectCollection(object.objectCollection).some(inner => inner.id === objectId)) {
      loopObjectId = object.id;
      break;
    }
  }
  return {
    objectId,
    loopObjectId,
    activeErrorFlowIds: collectErrorFlowIdsForObject(schema, objectId)
  };
}

export function isVariableInScope(symbol: MicroflowVariableSymbol | undefined, variableName: string, scope: ExpressionScopeContext): boolean {
  if (!symbol) {
    return false;
  }
  if (variableName === "$currentIndex") {
    return Boolean(scope.loopObjectId && symbol.scope.loopObjectId === scope.loopObjectId);
  }
  if (variableName.startsWith("$latest")) {
    return Boolean(symbol.scope.errorHandlerFlowId && scope.activeErrorFlowIds.includes(symbol.scope.errorHandlerFlowId));
  }
  if (symbol.scope.loopObjectId) {
    return symbol.scope.loopObjectId === scope.loopObjectId;
  }
  return true;
}
