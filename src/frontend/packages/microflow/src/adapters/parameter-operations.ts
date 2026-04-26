import type { MicroflowExpression, MicroflowParameter, MicroflowSchema } from "../schema/types";
import { addParameter as addParameterInternal, deleteParameter as deleteParameterInternal, renameParameter as renameParameterInternal } from "./authoring-operations";

function replaceExpressionVariable(expression: MicroflowExpression | undefined, from: string, to: string): MicroflowExpression | undefined {
  if (!expression) {
    return expression;
  }
  const raw = (expression.raw ?? expression.text ?? "").replace(new RegExp(`\\$${from}\\b`, "g"), `$${to}`);
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

function renameExpressions(schema: MicroflowSchema, from: string, to: string): MicroflowSchema {
  return {
    ...schema,
    objectCollection: {
      ...schema.objectCollection,
      objects: schema.objectCollection.objects.map(object => {
        if (object.kind === "actionActivity" && object.action.kind === "restCall") {
          return {
            ...object,
            action: {
              ...object.action,
              request: {
                ...object.action.request,
                urlExpression: replaceExpressionVariable(object.action.request.urlExpression, from, to) ?? object.action.request.urlExpression,
                queryParameters: object.action.request.queryParameters.map(item => ({ ...item, valueExpression: replaceExpressionVariable(item.valueExpression, from, to) ?? item.valueExpression })),
                headers: object.action.request.headers.map(item => ({ ...item, valueExpression: replaceExpressionVariable(item.valueExpression, from, to) ?? item.valueExpression }))
              }
            }
          };
        }
        if (object.kind === "endEvent") {
          return { ...object, returnValue: replaceExpressionVariable(object.returnValue, from, to) };
        }
        return object;
      })
    }
  };
}

export function addParameter(schema: MicroflowSchema, parameter: MicroflowParameter, position: { x: number; y: number }): MicroflowSchema {
  return addParameterInternal(schema, parameter, position);
}

export function renameParameter(schema: MicroflowSchema, parameterId: string, nextName: string): MicroflowSchema {
  const parameter = schema.parameters.find(item => item.id === parameterId);
  const renamed = renameParameterInternal(schema, parameterId, nextName);
  return parameter ? renameExpressions(renamed, parameter.name, nextName) : renamed;
}

export function deleteParameter(schema: MicroflowSchema, parameterId: string): MicroflowSchema {
  return deleteParameterInternal(schema, parameterId);
}
