import type {
  MicroflowAuthoringSchema,
  MicroflowDataType,
  MicroflowExpression,
  MicroflowGlobalVariable,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowParameter,
  MicroflowTypeRef,
} from "../types";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../../metadata/metadata-catalog";
import { parseExpression } from "../../expressions/expression-parser";
import { buildVariableIndex } from "../../variables/variable-index";
import { resolveVariableReferenceFromIndex } from "../../variables/variable-scope-query";
import { isReservedSystemVariableName } from "./reserved-variable-names";

const SIMPLE_RETURN_VARIABLE_REGEX = /^\$([A-Za-z_][A-Za-z0-9_]*)$/;

function mapObjectCollection(
  collection: MicroflowObjectCollection,
  updater: (object: MicroflowObject) => MicroflowObject,
): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects.map(object => {
      const next = updater(object);
      return next.kind === "loopedActivity"
        ? { ...next, objectCollection: mapObjectCollection(next.objectCollection, updater) }
        : next;
    }),
  };
}

function normalizeParameterName(name: string): string {
  return name.trim().toLocaleLowerCase();
}

function expressionForVariable(variableName: string, inferredType?: MicroflowDataType): MicroflowExpression {
  const raw = variableName.startsWith("$") ? variableName : `$${variableName}`;
  return {
    raw,
    inferredType,
    references: {
      variables: [raw],
      entities: [],
      attributes: [],
      associations: [],
      enumerations: [],
      functions: [],
    },
    diagnostics: [],
  };
}

function collectEndEvents(collection: MicroflowObjectCollection): Extract<MicroflowObject, { kind: "endEvent" }>[] {
  return collection.objects.flatMap(object => {
    if (object.kind === "endEvent") {
      return [object];
    }
    if (object.kind === "loopedActivity") {
      return collectEndEvents(object.objectCollection);
    }
    return [];
  });
}

function simpleReturnVariableName(expression: MicroflowExpression | undefined): string | undefined {
  const raw = expression?.raw?.trim();
  if (!raw) {
    return undefined;
  }
  const match = SIMPLE_RETURN_VARIABLE_REGEX.exec(raw);
  return match?.[1];
}

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
      variables,
    },
    referencedVariables: variables,
  };
}

function rewriteRawScopedVariableReferences(input: {
  schema: MicroflowAuthoringSchema;
  objectId: string;
  actionId?: string;
  fieldPath: string;
  raw: string;
  from: string;
  to: string;
  matchesSymbol: (symbol: ReturnType<typeof resolveVariableReferenceFromIndex>) => boolean;
}): string {
  const { schema, objectId, actionId, fieldPath, raw, from, to, matchesSymbol } = input;
  if (!raw.trim() || from === to) {
    return raw;
  }
  const variableIndex = buildVariableIndex(schema, EMPTY_MICROFLOW_METADATA_CATALOG);
  const parsed = parseExpression(raw);
  const replacements = parsed.references
    .filter((reference): reference is Extract<typeof reference, { kind: "variable" }> => reference.kind === "variable")
    .filter(reference => reference.variableName === from)
    .filter(reference => {
      const symbol = resolveVariableReferenceFromIndex(
        schema,
        variableIndex,
        { objectId, actionId, fieldPath },
        `$${reference.variableName}`,
      );
      return matchesSymbol(symbol);
    })
    .sort((left, right) => right.range.start - left.range.start);
  if (!replacements.length) {
    return raw;
  }
  let nextRaw = raw;
  for (const reference of replacements) {
    nextRaw = `${nextRaw.slice(0, reference.range.start)}$${to}${nextRaw.slice(reference.range.end)}`;
  }
  return nextRaw;
}

function replaceScopedExpressionVariable(
  schema: MicroflowAuthoringSchema,
  expression: MicroflowExpression | undefined,
  context: {
    objectId: string;
    actionId?: string;
    fieldPath: string;
  },
  from: string,
  to: string,
  matchesSymbol: (symbol: ReturnType<typeof resolveVariableReferenceFromIndex>) => boolean,
): MicroflowExpression | undefined {
  if (!expression) {
    return expression;
  }
  const current = expression.raw ?? expression.text ?? "";
  const raw = rewriteRawScopedVariableReferences({
    schema,
    objectId: context.objectId,
    actionId: context.actionId,
    fieldPath: context.fieldPath,
    raw: current,
    from,
    to,
    matchesSymbol,
  });
  if (raw === current) {
    return expression;
  }
  const variables = (expression.references?.variables ?? expression.referencedVariables ?? []).map(variable => variable === `$${from}` ? `$${to}` : variable);
  return {
    ...expression,
    raw,
    text: raw,
    references: {
      ...(expression.references ?? { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }),
      variables,
    },
    referencedVariables: variables,
  };
}

function rewriteScopedLoopVariableText(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  fieldPath: string,
  raw: string | undefined,
  from: string,
  to: string,
  loopObjectId: string,
): string | undefined {
  if (typeof raw !== "string") {
    return raw;
  }
  return rewriteRawScopedVariableReferences({
    schema,
    objectId,
    fieldPath,
    raw,
    from,
    to,
    matchesSymbol: symbol => Boolean(symbol && symbol.source.kind === "loopIterator" && symbol.source.loopObjectId === loopObjectId),
  });
}

function replaceScopedVariableReferenceName(
  schema: MicroflowAuthoringSchema,
  context: {
    objectId: string;
    actionId?: string;
    fieldPath: string;
  },
  value: string | undefined,
  from: string,
  to: string,
  matchesSymbol: (symbol: ReturnType<typeof resolveVariableReferenceFromIndex>) => boolean,
): string | undefined {
  if (typeof value !== "string") {
    return value;
  }
  const trimmed = value.trim();
  if (!trimmed) {
    return value;
  }
  const normalized = trimmed.startsWith("$.")
    ? trimmed.slice(2)
    : trimmed.startsWith("$")
      ? trimmed.slice(1)
      : trimmed;
  if (normalized !== from) {
    return value;
  }
  const symbol = resolveVariableReferenceFromIndex(
    schema,
    buildVariableIndex(schema, EMPTY_MICROFLOW_METADATA_CATALOG),
    { objectId: context.objectId, actionId: context.actionId, fieldPath: context.fieldPath },
    trimmed,
  );
  if (!matchesSymbol(symbol)) {
    return value;
  }
  const leadingWhitespace = value.match(/^\s*/)?.[0] ?? "";
  const trailingWhitespace = value.match(/\s*$/)?.[0] ?? "";
  const replacement = trimmed.startsWith("$.")
    ? `$.${to}`
    : trimmed.startsWith("$")
      ? `$${to}`
      : to;
  return `${leadingWhitespace}${replacement}${trailingWhitespace}`;
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
        messageExpression: replaceExpressionVariable(object.error.messageExpression, from, to),
      },
    };
  }
  if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression") {
    return {
      ...object,
      splitCondition: {
        ...object.splitCondition,
        expression: replaceExpressionVariable(object.splitCondition.expression, from, to) ?? object.splitCondition.expression,
      },
    };
  }
  if (object.kind === "loopedActivity") {
    return {
      ...object,
      loopSource: object.loopSource.kind === "whileCondition"
        ? { ...object.loopSource, expression: replaceExpressionVariable(object.loopSource.expression, from, to) ?? object.loopSource.expression }
        : object.loopSource,
      objectCollection: renameCollectionExpressions(object.objectCollection, from, to),
    };
  }
  if (object.kind === "annotation") {
    return { ...object, text: replaceVariableToken(object.text, from, to) };
  }
  if (object.kind !== "actionActivity") {
    return object;
  }
  const action = object.action;
  if (action.kind === "retrieve" && action.retrieveSource.kind === "database") {
    return {
      ...object,
      action: {
        ...action,
        retrieveSource: {
          ...action.retrieveSource,
          xPathConstraint: replaceExpressionVariable(action.retrieveSource.xPathConstraint ?? undefined, from, to),
          range: action.retrieveSource.range.kind === "custom"
            ? {
                ...action.retrieveSource.range,
                limitExpression: replaceExpressionVariable(action.retrieveSource.range.limitExpression, from, to) ?? action.retrieveSource.range.limitExpression,
                offsetExpression: replaceExpressionVariable(action.retrieveSource.range.offsetExpression, from, to),
              }
            : action.retrieveSource.range,
        },
      },
    };
  }
  if (action.kind === "createObject" || action.kind === "changeMembers") {
    return {
      ...object,
      action: {
        ...action,
        memberChanges: action.memberChanges.map(change => ({
          ...change,
          valueExpression: replaceExpressionVariable(change.valueExpression, from, to) ?? change.valueExpression,
        })),
      },
    };
  }
  if (action.kind === "callMicroflow") {
    return {
      ...object,
      action: {
        ...action,
        parameterMappings: action.parameterMappings.map(mapping => ({
          ...mapping,
          argumentExpression: replaceExpressionVariable(mapping.argumentExpression, from, to) ?? mapping.argumentExpression,
        })),
      },
    };
  }
  if (action.kind === "restCall") {
    const body = action.request.body.kind === "json" || action.request.body.kind === "text"
      ? { ...action.request.body, expression: replaceExpressionVariable(action.request.body.expression, from, to) ?? action.request.body.expression }
      : action.request.body.kind === "form"
        ? {
            ...action.request.body,
            fields: action.request.body.fields.map(field => ({
              ...field,
              valueExpression: replaceExpressionVariable(field.valueExpression, from, to) ?? field.valueExpression,
            })),
          }
        : action.request.body;
    return {
      ...object,
      action: {
        ...action,
        request: {
          ...action.request,
          urlExpression: replaceExpressionVariable(action.request.urlExpression, from, to) ?? action.request.urlExpression,
          headers: action.request.headers.map(item => ({
            ...item,
            valueExpression: replaceExpressionVariable(item.valueExpression, from, to) ?? item.valueExpression,
          })),
          queryParameters: action.request.queryParameters.map(item => ({
            ...item,
            valueExpression: replaceExpressionVariable(item.valueExpression, from, to) ?? item.valueExpression,
          })),
          body,
        },
      },
    };
  }
  if (action.kind === "logMessage") {
    return {
      ...object,
      action: {
        ...action,
        template: {
          ...action.template,
          text: replaceVariableToken(action.template.text, from, to),
          arguments: action.template.arguments.map(argument => replaceExpressionVariable(argument, from, to) ?? argument),
        },
      },
    };
  }
  return object;
}

function renameCollectionExpressions(collection: MicroflowObjectCollection, from: string, to: string): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects.map(object => renameObjectExpressions(object, from, to)),
  };
}

function renameParameterReferencesInObject(
  schema: MicroflowAuthoringSchema,
  parameterId: string,
  from: string,
  to: string,
  object: MicroflowObject,
): MicroflowObject {
  const matchesParameter = (symbol: ReturnType<typeof resolveVariableReferenceFromIndex>) =>
    Boolean(symbol && symbol.source.kind === "parameter" && symbol.source.parameterId === parameterId);
  const replaceParameterVariableName = (
    value: string | undefined,
    fieldPath: string,
    actionId?: string,
  ) => replaceScopedVariableReferenceName(schema, { objectId: object.id, actionId, fieldPath }, value, from, to, matchesParameter);
  if (object.kind === "endEvent") {
    return {
      ...object,
      returnValue: replaceScopedExpressionVariable(schema, object.returnValue, { objectId: object.id, fieldPath: "returnValue" }, from, to, matchesParameter),
    };
  }
  if (object.kind === "errorEvent") {
    return {
      ...object,
      error: {
        ...object.error,
        messageExpression: replaceScopedExpressionVariable(schema, object.error.messageExpression, { objectId: object.id, fieldPath: "error.messageExpression" }, from, to, matchesParameter),
      },
    };
  }
  if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression") {
    return {
      ...object,
      splitCondition: {
        ...object.splitCondition,
        expression: replaceScopedExpressionVariable(schema, object.splitCondition.expression, { objectId: object.id, fieldPath: "splitCondition.expression" }, from, to, matchesParameter) ?? object.splitCondition.expression,
      },
    };
  }
  if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "rule") {
    return {
      ...object,
      splitCondition: {
        ...object.splitCondition,
        parameterMappings: object.splitCondition.parameterMappings.map((mapping, index) => ({
          ...mapping,
          argumentExpression: replaceScopedExpressionVariable(schema, mapping.argumentExpression, { objectId: object.id, fieldPath: `splitCondition.parameterMappings.${index}.argumentExpression` }, from, to, matchesParameter) ?? mapping.argumentExpression,
          sourceVariableName: replaceParameterVariableName(mapping.sourceVariableName, `splitCondition.parameterMappings.${index}.sourceVariableName`) ?? mapping.sourceVariableName,
        })),
      },
    };
  }
  if (object.kind === "inheritanceSplit") {
    return {
      ...object,
      inputObjectVariableName: replaceParameterVariableName(object.inputObjectVariableName, "inputObjectVariableName") ?? object.inputObjectVariableName,
    };
  }
  if (object.kind === "loopedActivity") {
    return {
      ...object,
      loopSource: object.loopSource.kind === "whileCondition"
        ? {
            ...object.loopSource,
            expression: replaceScopedExpressionVariable(schema, object.loopSource.expression, { objectId: object.id, fieldPath: "loopSource.expression" }, from, to, matchesParameter) ?? object.loopSource.expression,
          }
        : {
            ...object.loopSource,
            listVariableName: replaceParameterVariableName(object.loopSource.listVariableName, "loopSource.listVariableName") ?? object.loopSource.listVariableName,
          },
      objectCollection: renameParameterReferencesInCollection(schema, parameterId, from, to, object.objectCollection),
    };
  }
  if (object.kind === "annotation") {
    return { ...object, text: replaceVariableToken(object.text, from, to) };
  }
  if (object.kind !== "actionActivity") {
    return object;
  }
  const action = object.action;
  if (action.kind === "retrieve" && action.retrieveSource.kind === "database") {
    return {
      ...object,
      action: {
        ...action,
        retrieveSource: {
          ...action.retrieveSource,
          xPathConstraint: replaceScopedExpressionVariable(schema, action.retrieveSource.xPathConstraint ?? undefined, { objectId: object.id, actionId: action.id, fieldPath: "action.retrieveSource.xPathConstraint" }, from, to, matchesParameter),
          range: action.retrieveSource.range.kind === "custom"
            ? {
                ...action.retrieveSource.range,
                limitExpression: replaceScopedExpressionVariable(schema, action.retrieveSource.range.limitExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.retrieveSource.range.limitExpression" }, from, to, matchesParameter) ?? action.retrieveSource.range.limitExpression,
                offsetExpression: replaceScopedExpressionVariable(schema, action.retrieveSource.range.offsetExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.retrieveSource.range.offsetExpression" }, from, to, matchesParameter),
              }
            : action.retrieveSource.range,
        },
      },
    };
  }
  if (action.kind === "retrieve" && action.retrieveSource.kind === "association") {
    return {
      ...object,
      action: {
        ...action,
        retrieveSource: {
          ...action.retrieveSource,
          startVariableName: replaceParameterVariableName(action.retrieveSource.startVariableName, "action.retrieveSource.startVariableName", action.id) ?? action.retrieveSource.startVariableName,
        },
      },
    };
  }
  if (action.kind === "cast") {
    return {
      ...object,
      action: {
        ...action,
        sourceObjectVariableName: replaceParameterVariableName((action as Record<string, string | undefined>).sourceObjectVariableName, "action.sourceObjectVariableName", action.id),
      },
    };
  }
  if (action.kind === "createObject" || action.kind === "changeMembers") {
    return {
      ...object,
      action: {
        ...action,
        ...(action.kind === "changeMembers"
          ? { changeVariableName: replaceParameterVariableName(action.changeVariableName, "action.changeVariableName", action.id) ?? action.changeVariableName }
          : {}),
        memberChanges: action.memberChanges.map((change, index) => ({
          ...change,
          valueExpression: replaceScopedExpressionVariable(schema, change.valueExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.memberChanges.${index}.valueExpression` }, from, to, matchesParameter) ?? change.valueExpression,
        })),
      },
    };
  }
  if (action.kind === "commit" || action.kind === "delete" || action.kind === "rollback") {
    return {
      ...object,
      action: {
        ...action,
        objectOrListVariableName: replaceParameterVariableName(action.objectOrListVariableName, "action.objectOrListVariableName", action.id) ?? action.objectOrListVariableName,
      },
    };
  }
  if (action.kind === "callMicroflow") {
    return {
      ...object,
      action: {
        ...action,
        parameterMappings: action.parameterMappings.map((mapping, index) => ({
          ...mapping,
          argumentExpression: replaceScopedExpressionVariable(schema, mapping.argumentExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.parameterMappings.${index}.argumentExpression` }, from, to, matchesParameter) ?? mapping.argumentExpression,
          sourceVariableName: replaceParameterVariableName(mapping.sourceVariableName, `action.parameterMappings.${index}.sourceVariableName`, action.id) ?? mapping.sourceVariableName,
        })),
      },
    };
  }
  if (action.kind === "restCall") {
    const body = action.request.body.kind === "json" || action.request.body.kind === "text"
      ? {
          ...action.request.body,
          expression: replaceScopedExpressionVariable(schema, action.request.body.expression, { objectId: object.id, actionId: action.id, fieldPath: "action.request.body.expression" }, from, to, matchesParameter) ?? action.request.body.expression,
        }
      : action.request.body.kind === "form"
        ? {
            ...action.request.body,
            fields: action.request.body.fields.map((field, index) => ({
              ...field,
              valueExpression: replaceScopedExpressionVariable(schema, field.valueExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.request.body.fields.${index}.valueExpression` }, from, to, matchesParameter) ?? field.valueExpression,
            })),
          }
        : action.request.body;
    return {
      ...object,
      action: {
        ...action,
        request: {
          ...action.request,
          urlExpression: replaceScopedExpressionVariable(schema, action.request.urlExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.request.urlExpression" }, from, to, matchesParameter) ?? action.request.urlExpression,
          headers: action.request.headers.map((item, index) => ({
            ...item,
            valueExpression: replaceScopedExpressionVariable(schema, item.valueExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.request.headers.${index}.valueExpression` }, from, to, matchesParameter) ?? item.valueExpression,
          })),
          queryParameters: action.request.queryParameters.map((item, index) => ({
            ...item,
            valueExpression: replaceScopedExpressionVariable(schema, item.valueExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.request.queryParameters.${index}.valueExpression` }, from, to, matchesParameter) ?? item.valueExpression,
          })),
          body,
        },
      },
    };
  }
  if (action.kind === "logMessage") {
    return {
      ...object,
      action: {
        ...action,
        template: {
          ...action.template,
          text: replaceVariableToken(action.template.text, from, to),
          arguments: action.template.arguments.map((argument, index) =>
            replaceScopedExpressionVariable(schema, argument, { objectId: object.id, actionId: action.id, fieldPath: `action.template.arguments.${index}` }, from, to, matchesParameter) ?? argument),
        },
      },
    };
  }
  if (action.kind === "createVariable" && action.initialValue) {
    return {
      ...object,
      action: {
        ...action,
        initialValue: replaceScopedExpressionVariable(schema, action.initialValue, { objectId: object.id, actionId: action.id, fieldPath: "action.initialValue" }, from, to, matchesParameter) ?? action.initialValue,
      },
    };
  }
  if (action.kind === "changeVariable") {
    return {
      ...object,
      action: {
        ...action,
        targetVariableName: replaceParameterVariableName(action.targetVariableName, "action.targetVariableName", action.id) ?? action.targetVariableName,
        newValueExpression: replaceScopedExpressionVariable(schema, action.newValueExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.newValueExpression" }, from, to, matchesParameter) ?? action.newValueExpression,
      },
    };
  }
  if (action.kind === "changeList") {
    return {
      ...object,
      action: {
        ...action,
        targetListVariableName: replaceParameterVariableName(action.targetListVariableName, "action.targetListVariableName", action.id) ?? action.targetListVariableName,
        sourceListVariableName: replaceParameterVariableName(action.sourceListVariableName, "action.sourceListVariableName", action.id),
        itemExpression: replaceScopedExpressionVariable(schema, action.itemExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.itemExpression" }, from, to, matchesParameter),
        itemsExpression: replaceScopedExpressionVariable(schema, action.itemsExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.itemsExpression" }, from, to, matchesParameter),
        conditionExpression: replaceScopedExpressionVariable(schema, action.conditionExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.conditionExpression" }, from, to, matchesParameter),
        indexExpression: replaceScopedExpressionVariable(schema, action.indexExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.indexExpression" }, from, to, matchesParameter),
      },
    };
  }
  if (action.kind === "aggregateList") {
    return {
      ...object,
      action: {
        ...action,
        listVariableName: replaceParameterVariableName(action.listVariableName, "action.listVariableName", action.id) ?? action.listVariableName,
        sourceListVariableName: replaceParameterVariableName(action.sourceListVariableName, "action.sourceListVariableName", action.id),
        aggregateExpression: replaceScopedExpressionVariable(schema, action.aggregateExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.aggregateExpression" }, from, to, matchesParameter) ?? action.aggregateExpression,
      },
    };
  }
  if (action.kind === "listOperation") {
    return {
      ...object,
      action: {
        ...action,
        leftListVariableName: replaceParameterVariableName(action.leftListVariableName, "action.leftListVariableName", action.id) ?? action.leftListVariableName,
        sourceListVariableName: replaceParameterVariableName(action.sourceListVariableName, "action.sourceListVariableName", action.id),
        rightListVariableName: replaceParameterVariableName(action.rightListVariableName, "action.rightListVariableName", action.id),
        secondListVariable: replaceParameterVariableName(action.secondListVariable, "action.secondListVariable", action.id),
        secondListVariableName: replaceParameterVariableName(action.secondListVariableName, "action.secondListVariableName", action.id),
        targetListVariableName: replaceParameterVariableName(action.targetListVariableName, "action.targetListVariableName", action.id),
        expression: replaceScopedExpressionVariable(schema, action.expression, { objectId: object.id, actionId: action.id, fieldPath: "action.expression" }, from, to, matchesParameter),
        filterExpression: replaceScopedExpressionVariable(schema, action.filterExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.filterExpression" }, from, to, matchesParameter),
        sortExpression: replaceScopedExpressionVariable(schema, action.sortExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.sortExpression" }, from, to, matchesParameter),
        sortKeys: action.sortKeys?.map((item, index) => ({
          ...item,
          expression: replaceScopedExpressionVariable(schema, item.expression, { objectId: object.id, actionId: action.id, fieldPath: `action.sortKeys.${index}.expression` }, from, to, matchesParameter),
        })),
      },
    };
  }
  const genericAction = action as Record<string, unknown>;
  const genericFieldPaths = [
    "targetObjectVariableName",
    "fileDocumentVariableName",
    "sourceVariableName",
    "workflowInstanceVariableName",
    "userTaskVariableName",
    "externalObjectVariableName",
  ] as const;
  const genericPatch = Object.fromEntries(
    genericFieldPaths
      .map(fieldPath => [fieldPath, replaceParameterVariableName(
        typeof genericAction[fieldPath] === "string" ? genericAction[fieldPath] as string : undefined,
        `action.${fieldPath}`,
        action.id,
      )])
      .filter(([, value]) => value !== undefined),
  );
  if (Object.keys(genericPatch).length > 0) {
    return {
      ...object,
      action: {
        ...action,
        ...genericPatch,
      },
    };
  }
  return object;
}

function renameParameterReferencesInCollection(
  schema: MicroflowAuthoringSchema,
  parameterId: string,
  from: string,
  to: string,
  collection: MicroflowObjectCollection,
): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects.map(object => renameParameterReferencesInObject(schema, parameterId, from, to, object)),
  };
}

function renameMatchedVariableReferencesInObject(
  schema: MicroflowAuthoringSchema,
  from: string,
  to: string,
  object: MicroflowObject,
  matchesVariable: (symbol: ReturnType<typeof resolveVariableReferenceFromIndex>) => boolean,
): MicroflowObject {
  const replaceMatchedVariableName = (
    value: string | undefined,
    fieldPath: string,
    actionId?: string,
  ) => replaceScopedVariableReferenceName(schema, { objectId: object.id, actionId, fieldPath }, value, from, to, matchesVariable);
  if (object.kind === "endEvent") {
    return {
      ...object,
      returnValue: replaceScopedExpressionVariable(schema, object.returnValue, { objectId: object.id, fieldPath: "returnValue" }, from, to, matchesVariable),
    };
  }
  if (object.kind === "errorEvent") {
    return {
      ...object,
      error: {
        ...object.error,
        messageExpression: replaceScopedExpressionVariable(schema, object.error.messageExpression, { objectId: object.id, fieldPath: "error.messageExpression" }, from, to, matchesVariable),
      },
    };
  }
  if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression") {
    return {
      ...object,
      splitCondition: {
        ...object.splitCondition,
        expression: replaceScopedExpressionVariable(schema, object.splitCondition.expression, { objectId: object.id, fieldPath: "splitCondition.expression" }, from, to, matchesVariable) ?? object.splitCondition.expression,
      },
    };
  }
  if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "rule") {
    return {
      ...object,
      splitCondition: {
        ...object.splitCondition,
        parameterMappings: object.splitCondition.parameterMappings.map((mapping, index) => ({
          ...mapping,
          argumentExpression: replaceScopedExpressionVariable(schema, mapping.argumentExpression, { objectId: object.id, fieldPath: `splitCondition.parameterMappings.${index}.argumentExpression` }, from, to, matchesVariable) ?? mapping.argumentExpression,
          sourceVariableName: replaceMatchedVariableName(mapping.sourceVariableName, `splitCondition.parameterMappings.${index}.sourceVariableName`) ?? mapping.sourceVariableName,
        })),
      },
    };
  }
  if (object.kind === "inheritanceSplit") {
    return {
      ...object,
      inputObjectVariableName: replaceMatchedVariableName(object.inputObjectVariableName, "inputObjectVariableName") ?? object.inputObjectVariableName,
    };
  }
  if (object.kind === "loopedActivity") {
    return {
      ...object,
      loopSource: object.loopSource.kind === "whileCondition"
        ? {
            ...object.loopSource,
            expression: replaceScopedExpressionVariable(schema, object.loopSource.expression, { objectId: object.id, fieldPath: "loopSource.expression" }, from, to, matchesVariable) ?? object.loopSource.expression,
          }
        : {
            ...object.loopSource,
            listVariableName: replaceMatchedVariableName(object.loopSource.listVariableName, "loopSource.listVariableName") ?? object.loopSource.listVariableName,
          },
      objectCollection: renameMatchedVariableReferencesInCollection(schema, from, to, object.objectCollection, matchesVariable),
    };
  }
  if (object.kind === "annotation") {
    return { ...object, text: replaceVariableToken(object.text, from, to) };
  }
  if (object.kind !== "actionActivity") {
    return object;
  }
  const action = object.action;
  if (action.kind === "retrieve" && action.retrieveSource.kind === "database") {
    return {
      ...object,
      action: {
          ...action,
          retrieveSource: {
            ...action.retrieveSource,
            xPathConstraint: replaceScopedExpressionVariable(schema, action.retrieveSource.xPathConstraint ?? undefined, { objectId: object.id, actionId: action.id, fieldPath: "action.retrieveSource.xPathConstraint" }, from, to, matchesVariable),
            range: action.retrieveSource.range.kind === "custom"
              ? {
                  ...action.retrieveSource.range,
                  limitExpression: replaceScopedExpressionVariable(schema, action.retrieveSource.range.limitExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.retrieveSource.range.limitExpression" }, from, to, matchesVariable) ?? action.retrieveSource.range.limitExpression,
                  offsetExpression: replaceScopedExpressionVariable(schema, action.retrieveSource.range.offsetExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.retrieveSource.range.offsetExpression" }, from, to, matchesVariable),
                }
              : action.retrieveSource.range,
        },
      },
    };
  }
  if (action.kind === "retrieve" && action.retrieveSource.kind === "association") {
    return {
      ...object,
      action: {
        ...action,
        retrieveSource: {
          ...action.retrieveSource,
          startVariableName: replaceMatchedVariableName(action.retrieveSource.startVariableName, "action.retrieveSource.startVariableName", action.id) ?? action.retrieveSource.startVariableName,
        },
      },
    };
  }
  if (action.kind === "cast") {
    return {
      ...object,
      action: {
        ...action,
        sourceObjectVariableName: replaceMatchedVariableName((action as Record<string, string | undefined>).sourceObjectVariableName, "action.sourceObjectVariableName", action.id),
      },
    };
  }
  if (action.kind === "createObject" || action.kind === "changeMembers") {
    return {
      ...object,
      action: {
        ...action,
        ...(action.kind === "changeMembers"
          ? { changeVariableName: replaceMatchedVariableName(action.changeVariableName, "action.changeVariableName", action.id) ?? action.changeVariableName }
          : {}),
        memberChanges: action.memberChanges.map((change, index) => ({
          ...change,
          valueExpression: replaceScopedExpressionVariable(schema, change.valueExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.memberChanges.${index}.valueExpression` }, from, to, matchesVariable) ?? change.valueExpression,
        })),
      },
    };
  }
  if (action.kind === "commit" || action.kind === "delete" || action.kind === "rollback") {
    return {
      ...object,
      action: {
        ...action,
        objectOrListVariableName: replaceMatchedVariableName(action.objectOrListVariableName, "action.objectOrListVariableName", action.id) ?? action.objectOrListVariableName,
      },
    };
  }
  if (action.kind === "callMicroflow") {
    return {
      ...object,
      action: {
        ...action,
        parameterMappings: action.parameterMappings.map((mapping, index) => ({
          ...mapping,
          argumentExpression: replaceScopedExpressionVariable(schema, mapping.argumentExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.parameterMappings.${index}.argumentExpression` }, from, to, matchesVariable) ?? mapping.argumentExpression,
          sourceVariableName: replaceMatchedVariableName(mapping.sourceVariableName, `action.parameterMappings.${index}.sourceVariableName`, action.id) ?? mapping.sourceVariableName,
        })),
      },
    };
  }
  if (action.kind === "restCall") {
    const body = action.request.body.kind === "json" || action.request.body.kind === "text"
      ? {
          ...action.request.body,
          expression: replaceScopedExpressionVariable(schema, action.request.body.expression, { objectId: object.id, actionId: action.id, fieldPath: "action.request.body.expression" }, from, to, matchesVariable) ?? action.request.body.expression,
        }
      : action.request.body.kind === "form"
        ? {
            ...action.request.body,
            fields: action.request.body.fields.map((field, index) => ({
              ...field,
              valueExpression: replaceScopedExpressionVariable(schema, field.valueExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.request.body.fields.${index}.valueExpression` }, from, to, matchesVariable) ?? field.valueExpression,
            })),
          }
        : action.request.body;
    return {
      ...object,
      action: {
        ...action,
        request: {
          ...action.request,
          urlExpression: replaceScopedExpressionVariable(schema, action.request.urlExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.request.urlExpression" }, from, to, matchesVariable) ?? action.request.urlExpression,
          headers: action.request.headers.map((item, index) => ({
            ...item,
            valueExpression: replaceScopedExpressionVariable(schema, item.valueExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.request.headers.${index}.valueExpression` }, from, to, matchesVariable) ?? item.valueExpression,
          })),
          queryParameters: action.request.queryParameters.map((item, index) => ({
            ...item,
            valueExpression: replaceScopedExpressionVariable(schema, item.valueExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.request.queryParameters.${index}.valueExpression` }, from, to, matchesVariable) ?? item.valueExpression,
          })),
          body,
        },
      },
    };
  }
  if (action.kind === "logMessage") {
    return {
      ...object,
      action: {
        ...action,
        template: {
          ...action.template,
          arguments: action.template.arguments.map((argument, index) =>
            replaceScopedExpressionVariable(schema, argument, { objectId: object.id, actionId: action.id, fieldPath: `action.template.arguments.${index}` }, from, to, matchesVariable) ?? argument),
        },
      },
    };
  }
  if (action.kind === "createVariable" && action.initialValue) {
    return {
      ...object,
      action: {
        ...action,
        initialValue: replaceScopedExpressionVariable(schema, action.initialValue, { objectId: object.id, actionId: action.id, fieldPath: "action.initialValue" }, from, to, matchesVariable) ?? action.initialValue,
      },
    };
  }
  if (action.kind === "changeVariable") {
    return {
      ...object,
      action: {
        ...action,
        targetVariableName: replaceMatchedVariableName(action.targetVariableName, "action.targetVariableName", action.id) ?? action.targetVariableName,
        newValueExpression: replaceScopedExpressionVariable(schema, action.newValueExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.newValueExpression" }, from, to, matchesVariable) ?? action.newValueExpression,
      },
    };
  }
  if (action.kind === "changeList") {
    return {
      ...object,
      action: {
        ...action,
        targetListVariableName: replaceMatchedVariableName(action.targetListVariableName, "action.targetListVariableName", action.id) ?? action.targetListVariableName,
        sourceListVariableName: replaceMatchedVariableName(action.sourceListVariableName, "action.sourceListVariableName", action.id),
        itemExpression: replaceScopedExpressionVariable(schema, action.itemExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.itemExpression" }, from, to, matchesVariable),
        itemsExpression: replaceScopedExpressionVariable(schema, action.itemsExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.itemsExpression" }, from, to, matchesVariable),
        conditionExpression: replaceScopedExpressionVariable(schema, action.conditionExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.conditionExpression" }, from, to, matchesVariable),
        indexExpression: replaceScopedExpressionVariable(schema, action.indexExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.indexExpression" }, from, to, matchesVariable),
      },
    };
  }
  if (action.kind === "aggregateList" && action.aggregateExpression) {
    return {
      ...object,
      action: {
        ...action,
        listVariableName: replaceMatchedVariableName(action.listVariableName, "action.listVariableName", action.id) ?? action.listVariableName,
        sourceListVariableName: replaceMatchedVariableName(action.sourceListVariableName, "action.sourceListVariableName", action.id),
        aggregateExpression: replaceScopedExpressionVariable(schema, action.aggregateExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.aggregateExpression" }, from, to, matchesVariable) ?? action.aggregateExpression,
      },
    };
  }
  if (action.kind === "listOperation") {
    return {
      ...object,
      action: {
        ...action,
        leftListVariableName: replaceMatchedVariableName(action.leftListVariableName, "action.leftListVariableName", action.id) ?? action.leftListVariableName,
        sourceListVariableName: replaceMatchedVariableName(action.sourceListVariableName, "action.sourceListVariableName", action.id),
        rightListVariableName: replaceMatchedVariableName(action.rightListVariableName, "action.rightListVariableName", action.id),
        secondListVariable: replaceMatchedVariableName(action.secondListVariable, "action.secondListVariable", action.id),
        secondListVariableName: replaceMatchedVariableName(action.secondListVariableName, "action.secondListVariableName", action.id),
        targetListVariableName: replaceMatchedVariableName(action.targetListVariableName, "action.targetListVariableName", action.id),
        expression: replaceScopedExpressionVariable(schema, action.expression, { objectId: object.id, actionId: action.id, fieldPath: "action.expression" }, from, to, matchesVariable),
        filterExpression: replaceScopedExpressionVariable(schema, action.filterExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.filterExpression" }, from, to, matchesVariable),
        sortExpression: replaceScopedExpressionVariable(schema, action.sortExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.sortExpression" }, from, to, matchesVariable),
        sortKeys: action.sortKeys?.map((item, index) => ({
          ...item,
          expression: replaceScopedExpressionVariable(schema, item.expression, { objectId: object.id, actionId: action.id, fieldPath: `action.sortKeys.${index}.expression` }, from, to, matchesVariable),
        })),
      },
    };
  }
  const genericAction = action as Record<string, unknown>;
  const genericFieldPaths = [
    "targetObjectVariableName",
    "fileDocumentVariableName",
    "sourceVariableName",
    "workflowInstanceVariableName",
    "userTaskVariableName",
    "externalObjectVariableName",
  ] as const;
  const genericPatch = Object.fromEntries(
    genericFieldPaths
      .map(fieldPath => [fieldPath, replaceMatchedVariableName(
        typeof genericAction[fieldPath] === "string" ? genericAction[fieldPath] as string : undefined,
        `action.${fieldPath}`,
        action.id,
      )])
      .filter(([, value]) => value !== undefined),
  );
  if (Object.keys(genericPatch).length > 0) {
    return {
      ...object,
      action: {
        ...action,
        ...genericPatch,
      },
    };
  }
  return object;
}

function renameMatchedVariableReferencesInCollection(
  schema: MicroflowAuthoringSchema,
  from: string,
  to: string,
  collection: MicroflowObjectCollection,
  matchesVariable: (symbol: ReturnType<typeof resolveVariableReferenceFromIndex>) => boolean,
): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects.map(object => renameMatchedVariableReferencesInObject(schema, from, to, object, matchesVariable)),
  };
}

function renameCreateVariableReferencesInCollection(
  schema: MicroflowAuthoringSchema,
  variableSourceObjectId: string,
  variableActionId: string,
  from: string,
  to: string,
  collection: MicroflowObjectCollection,
): MicroflowObjectCollection {
  return renameMatchedVariableReferencesInCollection(
    schema,
    from,
    to,
    collection,
    symbol => Boolean(
      symbol
      && symbol.source.kind === "createVariable"
      && symbol.source.objectId === variableSourceObjectId
      && symbol.source.actionId === variableActionId,
    ),
  );
}

type ActionOutputRenameSlot = "primary" | "statusCode" | "headers";

function renameActionOutputReferencesInCollection(
  schema: MicroflowAuthoringSchema,
  from: string,
  to: string,
  collection: MicroflowObjectCollection,
  matchesSymbol: (symbol: ReturnType<typeof resolveVariableReferenceFromIndex>) => boolean,
): MicroflowObjectCollection {
  return renameMatchedVariableReferencesInCollection(schema, from, to, collection, matchesSymbol);
}

function renameLoopIteratorExpressionsInObject(
  schema: MicroflowAuthoringSchema,
  loopObjectId: string,
  from: string,
  to: string,
  object: MicroflowObject,
): MicroflowObject {
  const matchesLoopIterator = (symbol: ReturnType<typeof resolveVariableReferenceFromIndex>) =>
    Boolean(symbol && symbol.source.kind === "loopIterator" && symbol.source.loopObjectId === loopObjectId);
  const replaceLoopVariableName = (
    value: string | undefined,
    fieldPath: string,
    actionId?: string,
  ) => replaceScopedVariableReferenceName(schema, { objectId: object.id, actionId, fieldPath }, value, from, to, matchesLoopIterator);
  if (object.kind === "endEvent") {
    return {
      ...object,
      returnValue: replaceScopedExpressionVariable(schema, object.returnValue, { objectId: object.id, fieldPath: "returnValue" }, from, to, matchesLoopIterator),
    };
  }
  if (object.kind === "errorEvent") {
    return {
      ...object,
      error: {
        ...object.error,
        messageExpression: replaceScopedExpressionVariable(schema, object.error.messageExpression, { objectId: object.id, fieldPath: "error.messageExpression" }, from, to, matchesLoopIterator),
      },
    };
  }
  if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression") {
    return {
      ...object,
      splitCondition: {
        ...object.splitCondition,
        expression: replaceScopedExpressionVariable(schema, object.splitCondition.expression, { objectId: object.id, fieldPath: "splitCondition.expression" }, from, to, matchesLoopIterator) ?? object.splitCondition.expression,
      },
    };
  }
  if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "rule") {
    return {
      ...object,
      splitCondition: {
        ...object.splitCondition,
        parameterMappings: object.splitCondition.parameterMappings.map((mapping, index) => ({
          ...mapping,
          argumentExpression: replaceScopedExpressionVariable(schema, mapping.argumentExpression, { objectId: object.id, fieldPath: `splitCondition.parameterMappings.${index}.argumentExpression` }, from, to, matchesLoopIterator) ?? mapping.argumentExpression,
        })),
      },
    };
  }
  if (object.kind === "inheritanceSplit") {
    return {
      ...object,
      inputObjectVariableName: replaceLoopVariableName(object.inputObjectVariableName, "inputObjectVariableName") ?? object.inputObjectVariableName,
    };
  }
  if (object.kind === "loopedActivity") {
    return {
      ...object,
      loopSource: object.loopSource.kind === "whileCondition"
        ? {
            ...object.loopSource,
            expression: replaceScopedExpressionVariable(schema, object.loopSource.expression, { objectId: object.id, fieldPath: "loopSource.expression" }, from, to, matchesLoopIterator) ?? object.loopSource.expression,
          }
        : {
            ...object.loopSource,
            listVariableName: rewriteScopedLoopVariableText(schema, object.id, "loopSource.listVariableName", object.loopSource.listVariableName, from, to, loopObjectId) ?? object.loopSource.listVariableName,
          },
      objectCollection: renameLoopIteratorExpressionsInCollection(schema, loopObjectId, from, to, object.objectCollection),
    };
  }
  if (object.kind !== "actionActivity") {
    return object;
  }
  const action = object.action;
  if (action.kind === "retrieve" && action.retrieveSource.kind === "database") {
    return {
      ...object,
      action: {
        ...action,
        retrieveSource: {
          ...action.retrieveSource,
          xPathConstraint: replaceScopedExpressionVariable(schema, action.retrieveSource.xPathConstraint ?? undefined, { objectId: object.id, actionId: action.id, fieldPath: "action.retrieveSource.xPathConstraint" }, from, to, matchesLoopIterator),
          range: action.retrieveSource.range.kind === "custom"
            ? {
                ...action.retrieveSource.range,
                limitExpression: replaceScopedExpressionVariable(schema, action.retrieveSource.range.limitExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.retrieveSource.range.limitExpression" }, from, to, matchesLoopIterator) ?? action.retrieveSource.range.limitExpression,
                offsetExpression: replaceScopedExpressionVariable(schema, action.retrieveSource.range.offsetExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.retrieveSource.range.offsetExpression" }, from, to, matchesLoopIterator),
              }
            : action.retrieveSource.range,
        },
      },
    };
  }
  if (action.kind === "retrieve" && action.retrieveSource.kind === "association") {
    return {
      ...object,
      action: {
        ...action,
        retrieveSource: {
          ...action.retrieveSource,
          startVariableName: replaceLoopVariableName(action.retrieveSource.startVariableName, "action.retrieveSource.startVariableName", action.id) ?? action.retrieveSource.startVariableName,
        },
      },
    };
  }
  if (action.kind === "cast") {
    return {
      ...object,
      action: {
        ...action,
        sourceObjectVariableName: replaceLoopVariableName((action as Record<string, string | undefined>).sourceObjectVariableName, "action.sourceObjectVariableName", action.id),
      },
    };
  }
  if (action.kind === "createObject" || action.kind === "changeMembers") {
    const nextAction = {
      ...object,
      action: {
        ...action,
        ...(action.kind === "changeMembers"
          ? { changeVariableName: replaceLoopVariableName(action.changeVariableName, "action.changeVariableName", action.id) ?? action.changeVariableName }
          : {}),
        memberChanges: action.memberChanges.map((change, index) => ({
          ...change,
          valueExpression: replaceScopedExpressionVariable(schema, change.valueExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.memberChanges.${index}.valueExpression` }, from, to, matchesLoopIterator) ?? change.valueExpression,
        })),
      },
    };
    return nextAction;
  }
  if (action.kind === "commit" || action.kind === "delete" || action.kind === "rollback") {
    return {
      ...object,
      action: {
        ...action,
        objectOrListVariableName: replaceLoopVariableName(action.objectOrListVariableName, "action.objectOrListVariableName", action.id) ?? action.objectOrListVariableName,
      },
    };
  }
  if (action.kind === "callMicroflow") {
    return {
      ...object,
      action: {
        ...action,
        parameterMappings: action.parameterMappings.map((mapping, index) => ({
          ...mapping,
          argumentExpression: replaceScopedExpressionVariable(schema, mapping.argumentExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.parameterMappings.${index}.argumentExpression` }, from, to, matchesLoopIterator) ?? mapping.argumentExpression,
          sourceVariableName: replaceLoopVariableName(mapping.sourceVariableName, `action.parameterMappings.${index}.sourceVariableName`, action.id) ?? mapping.sourceVariableName,
        })),
      },
    };
  }
  if (action.kind === "restCall") {
    const body = action.request.body.kind === "json" || action.request.body.kind === "text"
      ? {
          ...action.request.body,
          expression: replaceScopedExpressionVariable(schema, action.request.body.expression, { objectId: object.id, actionId: action.id, fieldPath: "action.request.body.expression" }, from, to, matchesLoopIterator) ?? action.request.body.expression,
        }
      : action.request.body.kind === "form"
        ? {
            ...action.request.body,
            fields: action.request.body.fields.map((field, index) => ({
              ...field,
              valueExpression: replaceScopedExpressionVariable(schema, field.valueExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.request.body.fields.${index}.valueExpression` }, from, to, matchesLoopIterator) ?? field.valueExpression,
            })),
          }
        : action.request.body;
    return {
      ...object,
      action: {
        ...action,
        request: {
          ...action.request,
          urlExpression: replaceScopedExpressionVariable(schema, action.request.urlExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.request.urlExpression" }, from, to, matchesLoopIterator) ?? action.request.urlExpression,
          headers: action.request.headers.map((item, index) => ({
            ...item,
            valueExpression: replaceScopedExpressionVariable(schema, item.valueExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.request.headers.${index}.valueExpression` }, from, to, matchesLoopIterator) ?? item.valueExpression,
          })),
          queryParameters: action.request.queryParameters.map((item, index) => ({
            ...item,
            valueExpression: replaceScopedExpressionVariable(schema, item.valueExpression, { objectId: object.id, actionId: action.id, fieldPath: `action.request.queryParameters.${index}.valueExpression` }, from, to, matchesLoopIterator) ?? item.valueExpression,
          })),
          body,
        },
      },
    };
  }
  if (action.kind === "logMessage") {
    return {
      ...object,
      action: {
        ...action,
        template: {
          ...action.template,
          arguments: action.template.arguments.map((argument, index) =>
            replaceScopedExpressionVariable(schema, argument, { objectId: object.id, actionId: action.id, fieldPath: `action.template.arguments.${index}` }, from, to, matchesLoopIterator) ?? argument),
        },
      },
    };
  }
  if (action.kind === "createVariable" && action.initialValue) {
    return {
      ...object,
      action: {
        ...action,
        initialValue: replaceScopedExpressionVariable(schema, action.initialValue, { objectId: object.id, actionId: action.id, fieldPath: "action.initialValue" }, from, to, matchesLoopIterator) ?? action.initialValue,
      },
    };
  }
  if (action.kind === "changeVariable") {
    return {
      ...object,
      action: {
        ...action,
        targetVariableName: replaceLoopVariableName(action.targetVariableName, "action.targetVariableName", action.id) ?? action.targetVariableName,
        newValueExpression: replaceScopedExpressionVariable(schema, action.newValueExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.newValueExpression" }, from, to, matchesLoopIterator) ?? action.newValueExpression,
      },
    };
  }
  if (action.kind === "changeList") {
    return {
      ...object,
      action: {
        ...action,
        targetListVariableName: replaceLoopVariableName(action.targetListVariableName, "action.targetListVariableName", action.id) ?? action.targetListVariableName,
        sourceListVariableName: replaceLoopVariableName(action.sourceListVariableName, "action.sourceListVariableName", action.id),
        itemExpression: replaceScopedExpressionVariable(schema, action.itemExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.itemExpression" }, from, to, matchesLoopIterator),
        itemsExpression: replaceScopedExpressionVariable(schema, action.itemsExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.itemsExpression" }, from, to, matchesLoopIterator),
        conditionExpression: replaceScopedExpressionVariable(schema, action.conditionExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.conditionExpression" }, from, to, matchesLoopIterator),
        indexExpression: replaceScopedExpressionVariable(schema, action.indexExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.indexExpression" }, from, to, matchesLoopIterator),
      },
    };
  }
  if (action.kind === "aggregateList" && action.aggregateExpression) {
    return {
      ...object,
      action: {
        ...action,
        listVariableName: replaceLoopVariableName(action.listVariableName, "action.listVariableName", action.id) ?? action.listVariableName,
        sourceListVariableName: replaceLoopVariableName(action.sourceListVariableName, "action.sourceListVariableName", action.id),
        aggregateExpression: replaceScopedExpressionVariable(schema, action.aggregateExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.aggregateExpression" }, from, to, matchesLoopIterator) ?? action.aggregateExpression,
      },
    };
  }
  if (action.kind === "listOperation") {
    return {
      ...object,
      action: {
        ...action,
        leftListVariableName: replaceLoopVariableName(action.leftListVariableName, "action.leftListVariableName", action.id) ?? action.leftListVariableName,
        sourceListVariableName: replaceLoopVariableName(action.sourceListVariableName, "action.sourceListVariableName", action.id),
        rightListVariableName: replaceLoopVariableName(action.rightListVariableName, "action.rightListVariableName", action.id),
        secondListVariable: replaceLoopVariableName(action.secondListVariable, "action.secondListVariable", action.id),
        secondListVariableName: replaceLoopVariableName(action.secondListVariableName, "action.secondListVariableName", action.id),
        targetListVariableName: replaceLoopVariableName(action.targetListVariableName, "action.targetListVariableName", action.id),
        expression: replaceScopedExpressionVariable(schema, action.expression, { objectId: object.id, actionId: action.id, fieldPath: "action.expression" }, from, to, matchesLoopIterator),
        filterExpression: replaceScopedExpressionVariable(schema, action.filterExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.filterExpression" }, from, to, matchesLoopIterator),
        sortExpression: replaceScopedExpressionVariable(schema, action.sortExpression, { objectId: object.id, actionId: action.id, fieldPath: "action.sortExpression" }, from, to, matchesLoopIterator),
        sortKeys: action.sortKeys?.map((item, index) => ({
          ...item,
          expression: replaceScopedExpressionVariable(schema, item.expression, { objectId: object.id, actionId: action.id, fieldPath: `action.sortKeys.${index}.expression` }, from, to, matchesLoopIterator),
        })),
      },
    };
  }
  const genericAction = action as Record<string, unknown>;
  const genericFieldPaths = [
    "targetObjectVariableName",
    "fileDocumentVariableName",
    "sourceVariableName",
    "workflowInstanceVariableName",
    "userTaskVariableName",
    "externalObjectVariableName",
  ] as const;
  const genericPatch = Object.fromEntries(
    genericFieldPaths
      .map(fieldPath => [fieldPath, replaceLoopVariableName(
        typeof genericAction[fieldPath] === "string" ? genericAction[fieldPath] as string : undefined,
        `action.${fieldPath}`,
        action.id,
      )])
      .filter(([, value]) => value !== undefined),
  );
  if (Object.keys(genericPatch).length > 0) {
    return {
      ...object,
      action: {
        ...action,
        ...genericPatch,
      },
    };
  }
  return object;
}

function renameLoopIteratorExpressionsInCollection(
  schema: MicroflowAuthoringSchema,
  loopObjectId: string,
  from: string,
  to: string,
  collection: MicroflowObjectCollection,
): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects.map(object => renameLoopIteratorExpressionsInObject(schema, loopObjectId, from, to, object)),
  };
}

export function deriveMicroflowReturnVariableName(
  schema: Pick<MicroflowAuthoringSchema, "objectCollection" | "returnType">,
): string | undefined {
  if (schema.returnType.kind === "void") {
    return undefined;
  }
  const endEvents = collectEndEvents(schema.objectCollection);
  if (endEvents.length === 0) {
    return undefined;
  }
  let candidate: string | undefined;
  for (const endEvent of endEvents) {
    const nextName = simpleReturnVariableName(endEvent.returnValue);
    if (!nextName) {
      return undefined;
    }
    if (candidate && candidate !== nextName) {
      return undefined;
    }
    candidate = nextName;
  }
  return candidate;
}

function syncCompatReturnVariableName(schema: MicroflowAuthoringSchema): MicroflowAuthoringSchema {
  const returnVariableName = deriveMicroflowReturnVariableName(schema);
  if (schema.returnVariableName === returnVariableName) {
    return schema;
  }
  return {
    ...schema,
    returnVariableName,
  };
}

export function clearDeletedReturnParameterBindings(
  schema: MicroflowAuthoringSchema,
  removedParameterNames: string[],
): MicroflowAuthoringSchema {
  const removed = new Set(removedParameterNames.map(name => name.trim()).filter(Boolean));
  if (removed.size === 0) {
    return syncCompatReturnVariableName(schema);
  }
  const boundReturnVariableName = deriveMicroflowReturnVariableName(schema);
  if (!boundReturnVariableName || !removed.has(boundReturnVariableName)) {
    return syncCompatReturnVariableName(schema);
  }
  return updateMicroflowReturnType(schema, { kind: "void" });
}

export function microflowDataTypeToTypeRef(dataType: MicroflowDataType): MicroflowTypeRef {
  if (dataType.kind === "void") {
    return { kind: "void", name: "Void" };
  }
  if (dataType.kind === "object") {
    return { kind: "entity", name: dataType.entityQualifiedName || "Object", entity: dataType.entityQualifiedName };
  }
  if (dataType.kind === "list") {
    return { kind: "list", name: "List", itemType: microflowDataTypeToTypeRef(dataType.itemType) };
  }
  if (dataType.kind === "unknown") {
    return { kind: "unknown", name: dataType.reason ?? "Unknown" };
  }
  return { kind: "primitive", name: dataType.kind };
}

export function getMicroflowParameters(schema: MicroflowAuthoringSchema): MicroflowParameter[] {
  return schema.parameters ?? [];
}

export function upsertMicroflowParameter(
  schema: MicroflowAuthoringSchema,
  parameter: MicroflowParameter,
): MicroflowAuthoringSchema {
  const nextParameter = {
    ...parameter,
    type: parameter.type ?? microflowDataTypeToTypeRef(parameter.dataType),
  };
  const exists = schema.parameters.some(item => item.id === parameter.id);
  return {
    ...schema,
    parameters: exists
      ? schema.parameters.map(item => item.id === parameter.id ? { ...item, ...nextParameter } : item)
      : [...schema.parameters, nextParameter],
  };
}

export function removeMicroflowParameter(
  schema: MicroflowAuthoringSchema,
  parameterId: string,
): MicroflowAuthoringSchema {
  const current = schema.parameters.find(parameter => parameter.id === parameterId);
  const nextSchema = {
    ...schema,
    parameters: schema.parameters.filter(parameter => parameter.id !== parameterId),
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.kind === "parameterObject" && object.parameterId === parameterId
      ? { ...object, parameterName: undefined }
      : object),
  };
  return clearDeletedReturnParameterBindings(nextSchema, current?.name ? [current.name] : []);
}

export function renameMicroflowParameter(
  schema: MicroflowAuthoringSchema,
  parameterId: string,
  nextName: string,
  options: { rewriteExpressions?: boolean } = {},
): MicroflowAuthoringSchema {
  const current = schema.parameters.find(parameter => parameter.id === parameterId);
  const renamed = {
    ...schema,
    parameters: schema.parameters.map(parameter => parameter.id === parameterId ? { ...parameter, name: nextName } : parameter),
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.kind === "parameterObject" && object.parameterId === parameterId
      ? { ...object, caption: nextName, parameterName: nextName }
      : object),
  };
  const shouldRewriteExpressions = options.rewriteExpressions ?? true;
  const nextSchema = current && shouldRewriteExpressions && current.name !== nextName
    ? {
        ...renamed,
        objectCollection: renameParameterReferencesInCollection(schema, parameterId, current.name, nextName, renamed.objectCollection),
      }
    : renamed;
  return syncCompatReturnVariableName(nextSchema);
}

export function renameCreateVariableOutput(
  schema: MicroflowAuthoringSchema,
  variableIdOrSourceObjectId: string,
  nextName: string,
  options: { rewriteExpressions?: boolean } = {},
): MicroflowAuthoringSchema {
  const createVariableObject = findCreateVariableObject(schema.objectCollection, variableIdOrSourceObjectId);
  if (!createVariableObject) {
    return schema;
  }
  const currentName = createVariableObject.action.variableName;
  const renamed = {
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.id === createVariableObject.id && object.kind === "actionActivity" && object.action.kind === "createVariable"
      ? {
          ...object,
          action: {
            ...object.action,
            variableName: nextName,
          },
        }
      : object),
  };
  const shouldRewriteExpressions = options.rewriteExpressions ?? true;
  const nextSchema = shouldRewriteExpressions && currentName !== nextName
    ? {
        ...renamed,
        objectCollection: renameCreateVariableReferencesInCollection(
          schema,
          createVariableObject.id,
          createVariableObject.action.id,
          currentName,
          nextName,
          renamed.objectCollection,
        ),
      }
    : renamed;
  return syncCompatReturnVariableName(nextSchema);
}

function actionOutputRenameDescriptor(
  object: Extract<MicroflowObject, { kind: "actionActivity" }>,
  slot: ActionOutputRenameSlot,
): {
  currentName: string;
  renameAction: (action: typeof object.action, nextName: string) => typeof object.action;
  matchesSymbol: (symbol: ReturnType<typeof resolveVariableReferenceFromIndex>) => boolean;
} | null {
  const { action, id: objectId } = object;
  if (slot === "primary") {
    const genericAction = action as Record<string, unknown>;
    if (action.kind === "createObject" || action.kind === "retrieve") {
      return {
        currentName: action.outputVariableName,
        renameAction: (currentAction, nextName) => ({ ...currentAction, outputVariableName: nextName }),
        matchesSymbol: symbol => Boolean(
          symbol
          && symbol.source.kind === "actionOutput"
          && symbol.source.objectId === objectId
          && symbol.source.actionId === action.id,
        ),
      };
    }
    if (action.kind === "createList") {
      return {
        currentName: action.outputListVariableName || action.listVariableName || "",
        renameAction: (currentAction, nextName) => ({
          ...currentAction,
          outputListVariableName: nextName,
          listVariableName: nextName,
        }),
        matchesSymbol: symbol => Boolean(
          symbol
          && symbol.source.kind === "createList"
          && symbol.source.objectId === objectId
          && symbol.source.actionId === action.id,
        ),
      };
    }
    if (action.kind === "aggregateList") {
      return {
        currentName: action.outputVariableName || action.resultVariableName || "",
        renameAction: (currentAction, nextName) => ({
          ...currentAction,
          outputVariableName: nextName,
          resultVariableName: nextName,
        }),
        matchesSymbol: symbol => Boolean(
          symbol
          && symbol.source.kind === "aggregateList"
          && symbol.source.objectId === objectId
          && symbol.source.actionId === action.id,
        ),
      };
    }
    if (action.kind === "listOperation") {
      return {
        currentName: action.outputVariableName || action.outputListVariableName || "",
        renameAction: (currentAction, nextName) => ({
          ...currentAction,
          outputVariableName: nextName,
          outputListVariableName: nextName,
        }),
        matchesSymbol: symbol => Boolean(
          symbol
          && symbol.source.kind === "listOperation"
          && symbol.source.objectId === objectId
          && symbol.source.actionId === action.id,
        ),
      };
    }
    if (action.kind === "callMicroflow" && action.returnValue.storeResult) {
      return {
        currentName: action.returnValue.outputVariableName || action.returnValue.resultVariableName || "",
        renameAction: (currentAction, nextName) => ({
          ...currentAction,
          returnValue: {
            ...currentAction.returnValue,
            outputVariableName: nextName,
            resultVariableName: nextName,
          },
        }),
        matchesSymbol: symbol => Boolean(
          symbol
          && symbol.source.kind === "microflowReturn"
          && symbol.source.objectId === objectId,
        ),
      };
    }
    if (action.kind === "restCall" && action.response.handling.kind !== "ignore") {
      return {
        currentName: action.response.handling.outputVariableName,
        renameAction: (currentAction, nextName) => ({
          ...currentAction,
          response: {
            ...currentAction.response,
            handling: {
              ...currentAction.response.handling,
              outputVariableName: nextName,
            },
          },
        }),
        matchesSymbol: symbol => Boolean(
          symbol
          && symbol.source.kind === "restResponse"
          && symbol.source.objectId === objectId
          && symbol.source.responseKind === action.response.handling.kind,
        ),
      };
    }
    if (action.kind === "cast") {
      const currentName = typeof genericAction.outputVariableName === "string" && genericAction.outputVariableName.trim()
        ? genericAction.outputVariableName as string
        : typeof genericAction.outputVariable === "string"
          ? genericAction.outputVariable as string
          : "";
      return {
        currentName,
        renameAction: (currentAction, nextName) => ({
          ...currentAction,
          outputVariableName: nextName,
          outputVariable: nextName,
        }),
        matchesSymbol: symbol => Boolean(
          symbol
          && symbol.source.kind === "modeledOnly"
          && symbol.source.objectId === objectId
          && symbol.source.actionId === action.id
          && symbol.source.actionKind === "cast",
        ),
      };
    }
    if (action.kind === "callWorkflow") {
      return {
        currentName: typeof genericAction.outputWorkflowVariableName === "string" ? genericAction.outputWorkflowVariableName as string : "",
        renameAction: (currentAction, nextName) => ({
          ...currentAction,
          outputWorkflowVariableName: nextName,
        }),
        matchesSymbol: symbol => Boolean(
          symbol
          && symbol.source.kind === "modeledOnly"
          && symbol.source.objectId === objectId
          && symbol.source.actionId === action.id
          && symbol.source.actionKind === "callWorkflow",
        ),
      };
    }
    if (action.kind === "generateDocument") {
      return {
        currentName: typeof genericAction.outputFileDocumentVariableName === "string" ? genericAction.outputFileDocumentVariableName as string : "",
        renameAction: (currentAction, nextName) => ({
          ...currentAction,
          outputFileDocumentVariableName: nextName,
        }),
        matchesSymbol: symbol => Boolean(
          symbol
          && symbol.source.kind === "modeledOnly"
          && symbol.source.objectId === objectId
          && symbol.source.actionId === action.id
          && symbol.source.actionKind === "generateDocument",
        ),
      };
    }
    if (action.kind === "callExternalAction") {
      return {
        currentName: typeof genericAction.returnVariableName === "string" ? genericAction.returnVariableName as string : "",
        renameAction: (currentAction, nextName) => ({
          ...currentAction,
          returnVariableName: nextName,
        }),
        matchesSymbol: symbol => Boolean(
          symbol
          && symbol.source.kind === "modeledOnly"
          && symbol.source.objectId === objectId
          && symbol.source.actionId === action.id
          && symbol.source.actionKind === "callExternalAction",
        ),
      };
    }
    if (action.kind === "callJavaAction" || action.kind === "callJavaScriptAction" || action.kind === "callNanoflow") {
      const returnValue = (genericAction.returnValue && typeof genericAction.returnValue === "object") ? genericAction.returnValue as Record<string, unknown> : {};
      const currentName = typeof returnValue.outputVariableName === "string"
        ? returnValue.outputVariableName as string
        : typeof genericAction.outputVariableName === "string"
          ? genericAction.outputVariableName as string
          : "";
      return {
        currentName,
        renameAction: (currentAction, nextName) => {
          const record = currentAction as Record<string, unknown>;
          const currentReturnValue = (record.returnValue && typeof record.returnValue === "object") ? record.returnValue as Record<string, unknown> : {};
          return {
            ...currentAction,
            returnValue: {
              ...currentReturnValue,
              outputVariableName: nextName,
              resultVariableName: nextName,
            },
          };
        },
        matchesSymbol: symbol => Boolean(
          symbol
          && symbol.source.kind === "modeledOnly"
          && symbol.source.objectId === objectId
          && symbol.source.actionId === action.id
          && symbol.source.actionKind === action.kind,
        ),
      };
    }
    if (action.kind === "webServiceCall" || action.kind === "importXml" || action.kind === "exportXml" || action.kind === "restOperationCall" || action.kind === "mlModelCall" || action.kind === "retrieveWorkflowContext" || action.kind === "generateJumpToOptions" || action.kind === "retrieveWorkflowActivityRecords") {
      return {
        currentName: typeof genericAction.outputVariableName === "string" ? genericAction.outputVariableName as string : "",
        renameAction: (currentAction, nextName) => ({
          ...currentAction,
          outputVariableName: nextName,
          ...(action.kind === "generateJumpToOptions" ? { resultVariableName: nextName } : {}),
        }),
        matchesSymbol: symbol => Boolean(
          symbol
          && symbol.source.kind === "modeledOnly"
          && symbol.source.objectId === objectId
          && symbol.source.actionId === action.id
          && symbol.source.actionKind === action.kind,
        ),
      };
    }
    if (action.kind === "retrieveWorkflows") {
      const currentName = typeof genericAction.outputListVariableName === "string" && genericAction.outputListVariableName.trim()
        ? genericAction.outputListVariableName as string
        : typeof genericAction.listVariableName === "string"
          ? genericAction.listVariableName as string
          : "";
      return {
        currentName,
        renameAction: (currentAction, nextName) => ({
          ...currentAction,
          outputListVariableName: nextName,
          listVariableName: nextName,
        }),
        matchesSymbol: symbol => Boolean(
          symbol
          && symbol.source.kind === "modeledOnly"
          && symbol.source.objectId === objectId
          && symbol.source.actionId === action.id
          && symbol.source.actionKind === "retrieveWorkflows",
        ),
      };
    }
    return null;
  }
  if (action.kind !== "restCall") {
    return null;
  }
  if (slot === "statusCode") {
    return {
      currentName: action.response.statusCodeVariableName || "",
      renameAction: (currentAction, nextName) => ({
        ...currentAction,
        response: {
          ...currentAction.response,
          statusCodeVariableName: nextName,
        },
      }),
      matchesSymbol: symbol => Boolean(
        symbol
        && symbol.source.kind === "restResponse"
        && symbol.source.objectId === objectId
        && symbol.source.responseKind === "statusCode",
      ),
    };
  }
  return {
    currentName: action.response.headersVariableName || "",
    renameAction: (currentAction, nextName) => ({
      ...currentAction,
      response: {
        ...currentAction.response,
        headersVariableName: nextName,
      },
    }),
    matchesSymbol: symbol => Boolean(
      symbol
      && symbol.source.kind === "restResponse"
      && symbol.source.objectId === objectId
      && symbol.source.responseKind === "headers",
    ),
  };
}

export function renameActionOutputVariable(
  schema: MicroflowAuthoringSchema,
  objectIdOrActionId: string,
  nextName: string,
  options: { slot?: ActionOutputRenameSlot; rewriteExpressions?: boolean } = {},
): MicroflowAuthoringSchema {
  const outputObject = findActionOutputObject(schema.objectCollection, objectIdOrActionId);
  if (!outputObject) {
    return schema;
  }
  const descriptor = actionOutputRenameDescriptor(outputObject, options.slot ?? "primary");
  if (!descriptor) {
    return schema;
  }
  const currentName = descriptor.currentName;
  const renamed = {
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.id === outputObject.id && object.kind === "actionActivity"
      ? {
          ...object,
          action: descriptor.renameAction(object.action, nextName),
        }
      : object),
  };
  const shouldRewriteExpressions = options.rewriteExpressions ?? true;
  if (!shouldRewriteExpressions || !currentName || currentName === nextName) {
    return syncCompatReturnVariableName(renamed);
  }
  return syncCompatReturnVariableName({
    ...renamed,
    objectCollection: renameActionOutputReferencesInCollection(
      schema,
      currentName,
      nextName,
      renamed.objectCollection,
      descriptor.matchesSymbol,
    ),
  });
}

export function renameLoopIteratorVariable(
  schema: MicroflowAuthoringSchema,
  loopObjectId: string,
  nextName: string,
  options: { rewriteExpressions?: boolean } = {},
): MicroflowAuthoringSchema {
  const loop = findObject(schema.objectCollection, loopObjectId);
  if (!loop || loop.kind !== "loopedActivity" || loop.loopSource.kind !== "iterableList") {
    return schema;
  }
  const currentName = loop.loopSource.iteratorVariableName;
  const renamed = {
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.id === loopObjectId && object.kind === "loopedActivity" && object.loopSource.kind === "iterableList"
      ? {
          ...object,
          loopSource: {
            ...object.loopSource,
            iteratorVariableName: nextName,
          },
        }
      : object),
  };
  const shouldRewriteExpressions = options.rewriteExpressions ?? true;
  if (!shouldRewriteExpressions || currentName === nextName) {
    return renamed;
  }
  return {
    ...renamed,
    objectCollection: mapObjectCollection(renamed.objectCollection, object => object.id === loopObjectId && object.kind === "loopedActivity"
      ? {
          ...object,
          objectCollection: renameLoopIteratorExpressionsInCollection(schema, loopObjectId, currentName, nextName, object.objectCollection),
        }
      : object),
  };
}

export function updateMicroflowParameterType(
  schema: MicroflowAuthoringSchema,
  parameterId: string,
  nextType: MicroflowDataType,
): MicroflowAuthoringSchema {
  const current = schema.parameters.find(parameter => parameter.id === parameterId);
  const nextSchema = {
    ...schema,
    parameters: schema.parameters.map(parameter => parameter.id === parameterId
      ? { ...parameter, dataType: nextType, type: microflowDataTypeToTypeRef(nextType) }
      : parameter),
  };
  if (!current) {
    return nextSchema;
  }
  const boundReturnVariableName = deriveMicroflowReturnVariableName(schema);
  if (boundReturnVariableName !== current.name) {
    return nextSchema;
  }
  return syncCompatReturnVariableName({
    ...nextSchema,
    returnType: nextType,
    objectCollection: mapObjectCollection(nextSchema.objectCollection, object => object.kind === "endEvent" && simpleReturnVariableName(object.returnValue) === current.name
      ? { ...object, returnValue: object.returnValue ? { ...object.returnValue, inferredType: nextType } : object.returnValue }
      : object),
  });
}

export function syncParameterObjectToDefinition(
  schema: MicroflowAuthoringSchema,
  objectId: string,
): MicroflowAuthoringSchema {
  const object = findObject(schema.objectCollection, objectId);
  if (!object || object.kind !== "parameterObject") {
    return schema;
  }
  const current = schema.parameters.find(parameter => parameter.id === object.parameterId);
  const name = object.parameterName ?? object.caption ?? current?.name ?? "parameter";
  return upsertMicroflowParameter(schema, {
    ...(current ?? {
      id: object.parameterId,
      stableId: object.parameterId,
      dataType: { kind: "string" },
      required: true,
    }),
    name,
    documentation: current?.documentation ?? object.documentation,
  });
}

export function syncParameterDefinitionToObject(
  schema: MicroflowAuthoringSchema,
  parameterId: string,
): MicroflowAuthoringSchema {
  const parameter = schema.parameters.find(item => item.id === parameterId);
  if (!parameter) {
    return schema;
  }
  return {
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.kind === "parameterObject" && object.parameterId === parameterId
      ? { ...object, caption: parameter.name, parameterName: parameter.name, documentation: parameter.documentation ?? object.documentation }
      : object),
  };
}

export function updateMicroflowReturnType(
  schema: MicroflowAuthoringSchema,
  returnType: MicroflowDataType,
): MicroflowAuthoringSchema {
  return syncCompatReturnVariableName({
    ...schema,
    returnType,
    objectCollection: returnType.kind === "void"
      ? mapObjectCollection(schema.objectCollection, object => object.kind === "endEvent" ? { ...object, returnValue: undefined } : object)
      : schema.objectCollection,
  });
}

export function updateEndEventReturnValue(
  schema: MicroflowAuthoringSchema,
  endObjectId: string,
  expression: MicroflowExpression | undefined,
): MicroflowAuthoringSchema {
  return syncCompatReturnVariableName({
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.id === endObjectId && object.kind === "endEvent"
      ? { ...object, returnValue: expression }
      : object),
  });
}

export function setParameterAsMicroflowReturnValue(
  schema: MicroflowAuthoringSchema,
  parameterId: string,
): MicroflowAuthoringSchema {
  const parameter = schema.parameters.find(item => item.id === parameterId);
  const parameterName = parameter?.name?.trim();
  if (!parameter || !parameterName) {
    return schema;
  }
  const returnValue = expressionForVariable(parameterName, parameter.dataType);
  const nextSchema = updateMicroflowReturnType(schema, parameter.dataType);
  return syncCompatReturnVariableName({
    ...nextSchema,
    objectCollection: mapObjectCollection(nextSchema.objectCollection, object => object.kind === "endEvent"
      ? { ...object, returnValue }
      : object),
  });
}

export function getParameterNameWarning(
  schema: MicroflowAuthoringSchema,
  parameterId: string,
  name: string,
): string | undefined {
  const trimmed = name.trim();
  if (!trimmed) {
    return "Parameter name is required.";
  }
  if (isReservedSystemVariableName(trimmed)) {
    return "Parameter name conflicts with a reserved system variable.";
  }
  const normalized = normalizeParameterName(trimmed);
  const duplicate = schema.parameters.some(parameter => parameter.id !== parameterId && normalizeParameterName(parameter.name) === normalized);
  if (duplicate) {
    return "Parameter name must be unique in the current microflow.";
  }
  return undefined;
}

function findObject(collection: MicroflowObjectCollection, objectId: string): MicroflowObject | undefined {
  for (const object of collection.objects) {
    if (object.id === objectId) {
      return object;
    }
    if (object.kind === "loopedActivity") {
      const nested = findObject(object.objectCollection, objectId);
      if (nested) {
        return nested;
      }
    }
  }
  return undefined;
}

function findCreateVariableObject(
  collection: MicroflowObjectCollection,
  variableIdOrSourceObjectId: string,
): Extract<MicroflowObject, { kind: "actionActivity" }> | undefined {
  for (const object of collection.objects) {
    if (object.kind === "actionActivity" && object.action.kind === "createVariable" && (object.id === variableIdOrSourceObjectId || object.action.id === variableIdOrSourceObjectId)) {
      return object;
    }
    if (object.kind === "loopedActivity") {
      const nested = findCreateVariableObject(object.objectCollection, variableIdOrSourceObjectId);
      if (nested) {
        return nested;
      }
    }
  }
  return undefined;
}

function findActionOutputObject(
  collection: MicroflowObjectCollection,
  objectIdOrActionId: string,
): Extract<MicroflowObject, { kind: "actionActivity" }> | undefined {
  for (const object of collection.objects) {
    if (object.kind === "actionActivity" && (object.id === objectIdOrActionId || object.action.id === objectIdOrActionId)) {
      return object;
    }
    if (object.kind === "loopedActivity") {
      const nested = findActionOutputObject(object.objectCollection, objectIdOrActionId);
      if (nested) {
        return nested;
      }
    }
  }
  return undefined;
}

export function getMicroflowGlobalVariables(schema: MicroflowAuthoringSchema): MicroflowGlobalVariable[] {
  return schema.globalVariables ?? [];
}

export function upsertMicroflowGlobalVariable(
  schema: MicroflowAuthoringSchema,
  variable: MicroflowGlobalVariable,
): MicroflowAuthoringSchema {
  const exists = (schema.globalVariables ?? []).some(item => item.id === variable.id);
  return {
    ...schema,
    globalVariables: exists
      ? (schema.globalVariables ?? []).map(item => item.id === variable.id ? { ...item, ...variable } : item)
      : [...(schema.globalVariables ?? []), variable],
  };
}

export function removeMicroflowGlobalVariable(
  schema: MicroflowAuthoringSchema,
  variableId: string,
): MicroflowAuthoringSchema {
  return {
    ...schema,
    globalVariables: (schema.globalVariables ?? []).filter(item => item.id !== variableId),
  };
}

export function getGlobalVariableNameWarning(
  schema: MicroflowAuthoringSchema,
  variableId: string,
  name: string,
): string | undefined {
  const trimmed = name.trim();
  if (!trimmed) {
    return "Variable name is required.";
  }
  if (isReservedSystemVariableName(trimmed)) {
    return "Variable name conflicts with a reserved system variable.";
  }
  const normalized = trimmed.toLocaleLowerCase();
  const duplicateParam = schema.parameters.some(p => p.name.toLocaleLowerCase() === normalized);
  if (duplicateParam) {
    return "Variable name conflicts with an existing parameter.";
  }
  const duplicateGlobal = (schema.globalVariables ?? []).some(v => v.id !== variableId && v.name.toLocaleLowerCase() === normalized);
  if (duplicateGlobal) {
    return "Variable name must be unique in the current microflow.";
  }
  return undefined;
}
