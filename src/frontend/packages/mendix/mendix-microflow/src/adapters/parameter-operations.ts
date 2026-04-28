import type { MicroflowAction, MicroflowExpression, MicroflowObject, MicroflowObjectCollection, MicroflowParameter, MicroflowSchema } from "../schema/types";
import { addParameter as addParameterInternal, deleteParameter as deleteParameterInternal, refreshDerivedState, renameParameter as renameParameterInternal } from "./authoring-operations";

function replaceVariableToken(raw: string, from: string, to: string): string {
  return raw.replace(new RegExp(`\\$${from}\\b`, "g"), `$${to}`);
}

function replaceExpressionVariable(expression: MicroflowExpression | undefined, from: string, to: string): MicroflowExpression | undefined {
  if (!expression) {
    return expression;
  }
  const current = expression.raw ?? expression.text ?? "";
  const raw = replaceVariableToken(current, from, to);
  const variables = (expression.references?.variables ?? expression.referencedVariables ?? []).map(variable => variable === `$${from}` ? `$${to}` : variable);
  return {
    ...expression,
    raw,
    text: raw,
    references: {
      ...(expression.references ?? { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }),
      variables
    },
    referencedVariables: variables
  };
}

function renameActionExpressions(action: MicroflowAction, from: string, to: string): MicroflowAction {
  if (action.kind === "retrieve" && action.retrieveSource.kind === "database") {
    return {
      ...action,
      retrieveSource: {
        ...action.retrieveSource,
        xPathConstraint: replaceExpressionVariable(action.retrieveSource.xPathConstraint ?? undefined, from, to),
        range: action.retrieveSource.range.kind === "custom"
          ? {
              ...action.retrieveSource.range,
              limitExpression: replaceExpressionVariable(action.retrieveSource.range.limitExpression, from, to) ?? action.retrieveSource.range.limitExpression,
              offsetExpression: replaceExpressionVariable(action.retrieveSource.range.offsetExpression, from, to)
            }
          : action.retrieveSource.range
      }
    };
  }
  if (action.kind === "createObject" || action.kind === "changeMembers") {
    return {
      ...action,
      memberChanges: action.memberChanges.map(change => ({
        ...change,
        valueExpression: replaceExpressionVariable(change.valueExpression, from, to) ?? change.valueExpression
      }))
    };
  }
  if (action.kind === "callMicroflow") {
    return {
      ...action,
      parameterMappings: action.parameterMappings.map(mapping => ({
        ...mapping,
        argumentExpression: replaceExpressionVariable(mapping.argumentExpression, from, to) ?? mapping.argumentExpression
      }))
    };
  }
  if (action.kind === "restCall") {
    const body = action.request.body.kind === "json" || action.request.body.kind === "text"
      ? { ...action.request.body, expression: replaceExpressionVariable(action.request.body.expression, from, to) ?? action.request.body.expression }
      : action.request.body.kind === "form"
        ? {
            ...action.request.body,
            fields: action.request.body.fields.map(field => ({ ...field, valueExpression: replaceExpressionVariable(field.valueExpression, from, to) ?? field.valueExpression }))
          }
        : action.request.body;
    return {
      ...action,
      request: {
        ...action.request,
        urlExpression: replaceExpressionVariable(action.request.urlExpression, from, to) ?? action.request.urlExpression,
        headers: action.request.headers.map(item => ({ ...item, valueExpression: replaceExpressionVariable(item.valueExpression, from, to) ?? item.valueExpression })),
        queryParameters: action.request.queryParameters.map(item => ({ ...item, valueExpression: replaceExpressionVariable(item.valueExpression, from, to) ?? item.valueExpression })),
        body
      }
    };
  }
  if (action.kind === "logMessage") {
    return {
      ...action,
      template: {
        ...action.template,
        text: replaceVariableToken(action.template.text, from, to),
        arguments: action.template.arguments.map(argument => replaceExpressionVariable(argument, from, to) ?? argument)
      }
    };
  }
  return action;
}

function renameObjectExpressions(object: MicroflowObject, from: string, to: string): MicroflowObject {
  if (object.kind === "endEvent") {
    return { ...object, returnValue: replaceExpressionVariable(object.returnValue, from, to) };
  }
  if (object.kind === "errorEvent") {
    return {
      ...object,
      error: {
        ...object.error,
        messageExpression: replaceExpressionVariable(object.error.messageExpression, from, to)
      }
    };
  }
  if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression") {
    return {
      ...object,
      splitCondition: {
        ...object.splitCondition,
        expression: replaceExpressionVariable(object.splitCondition.expression, from, to) ?? object.splitCondition.expression
      }
    };
  }
  if (object.kind === "loopedActivity") {
    return {
      ...object,
      loopSource: object.loopSource.kind === "whileCondition"
        ? { ...object.loopSource, expression: replaceExpressionVariable(object.loopSource.expression, from, to) ?? object.loopSource.expression }
        : object.loopSource,
      objectCollection: renameCollectionExpressions(object.objectCollection, from, to)
    };
  }
  if (object.kind === "actionActivity") {
    return { ...object, action: renameActionExpressions(object.action, from, to) };
  }
  if (object.kind === "annotation") {
    return { ...object, text: replaceVariableToken(object.text, from, to) };
  }
  return object;
}

function renameCollectionExpressions(collection: MicroflowObjectCollection, from: string, to: string): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects.map(object => renameObjectExpressions(object, from, to))
  };
}

function renameExpressions(schema: MicroflowSchema, from: string, to: string): MicroflowSchema {
  return {
    ...schema,
    objectCollection: renameCollectionExpressions(schema.objectCollection, from, to)
  };
}

export function addParameter(schema: MicroflowSchema, parameter: MicroflowParameter, position: { x: number; y: number }): MicroflowSchema {
  return addParameterInternal(schema, parameter, position);
}

export function renameParameter(schema: MicroflowSchema, parameterId: string, nextName: string, options: { rewriteExpressions?: boolean } = {}): MicroflowSchema {
  const parameter = schema.parameters.find(item => item.id === parameterId);
  const renamed = renameParameterInternal(schema, parameterId, nextName);
  const withExpressions = parameter && options.rewriteExpressions ? renameExpressions(renamed, parameter.name, nextName) : renamed;
  return refreshDerivedState(withExpressions);
}

export function deleteParameter(schema: MicroflowSchema, parameterId: string): MicroflowSchema {
  return deleteParameterInternal(schema, parameterId);
}
