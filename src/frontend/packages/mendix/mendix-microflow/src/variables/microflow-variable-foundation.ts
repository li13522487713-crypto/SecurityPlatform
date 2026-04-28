import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import type {
  MicroflowAction,
  MicroflowActionActivity,
  MicroflowAuthoringSchema,
  MicroflowDataType,
  MicroflowExpression,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowVariableIndex,
  MicroflowVariableSymbol,
} from "../schema/types";
import { duplicateObject } from "../adapters/authoring-operations";
import { buildVariableIndex } from "./variable-index";

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

function removeCreateVariableObject(
  collection: MicroflowObjectCollection,
  variableIdOrSourceObjectId: string,
): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects
      .filter(object => !(object.kind === "actionActivity" && object.action.kind === "createVariable" && (object.id === variableIdOrSourceObjectId || object.action.id === variableIdOrSourceObjectId)))
      .map(object => object.kind === "loopedActivity"
        ? { ...object, objectCollection: removeCreateVariableObject(object.objectCollection, variableIdOrSourceObjectId) }
        : object),
  };
}

function refreshVariableIndex(schema: MicroflowAuthoringSchema): MicroflowAuthoringSchema {
  return {
    ...schema,
    variables: buildMicroflowVariableIndex(schema),
  };
}

function normalizeName(name: string): string {
  return name.trim().toLocaleLowerCase();
}

function updateCreateVariableAction(
  schema: MicroflowAuthoringSchema,
  variableId: string,
  updater: (action: Extract<MicroflowAction, { kind: "createVariable" }>, object: MicroflowActionActivity) => Extract<MicroflowAction, { kind: "createVariable" }>,
): MicroflowAuthoringSchema {
  return refreshVariableIndex({
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => {
      if (object.kind !== "actionActivity" || object.action.kind !== "createVariable" || object.action.id !== variableId) {
        return object;
      }
      return { ...object, action: updater(object.action, object) };
    }),
  });
}

export function buildMicroflowVariableIndex(schema: MicroflowAuthoringSchema): MicroflowVariableIndex {
  return buildVariableIndex(schema, EMPTY_MICROFLOW_METADATA_CATALOG);
}

export function getMicroflowVariables(schema: MicroflowAuthoringSchema): MicroflowVariableSymbol[] {
  return buildMicroflowVariableIndex(schema).all ?? [];
}

export function buildMicroflowExpressionContext(schema: MicroflowAuthoringSchema): MicroflowVariableSymbol[] {
  return getMicroflowVariables(schema);
}

export function getMicroflowVariableByName(schema: MicroflowAuthoringSchema, name: string): MicroflowVariableSymbol | undefined {
  const normalized = normalizeName(name);
  return getMicroflowVariables(schema).find(symbol => normalizeName(symbol.name) === normalized);
}

export function getMicroflowVariableById(schema: MicroflowAuthoringSchema, id: string): MicroflowVariableSymbol | undefined {
  return getMicroflowVariables(schema).find(symbol =>
    symbol.id === id ||
    (symbol.source.kind === "createVariable" && (symbol.source.actionId === id || symbol.source.objectId === id)) ||
    (symbol.source.kind === "parameter" && symbol.source.parameterId === id)
  );
}

export function upsertMicroflowVariable(
  schema: MicroflowAuthoringSchema,
  variable: { id: string; name: string; dataType: MicroflowDataType; initialValue?: MicroflowExpression; description?: string; readonly?: boolean },
): MicroflowAuthoringSchema {
  return updateCreateVariableAction(schema, variable.id, action => ({
    ...action,
    variableName: variable.name,
    dataType: variable.dataType,
    initialValue: variable.initialValue ?? action.initialValue,
    documentation: variable.description ?? action.documentation,
    readonly: variable.readonly ?? action.readonly,
  }));
}

export function removeMicroflowVariable(schema: MicroflowAuthoringSchema, variableId: string): MicroflowAuthoringSchema {
  return removeMicroflowVariableDefinition(schema, variableId);
}

export function renameMicroflowVariable(
  schema: MicroflowAuthoringSchema,
  variableId: string,
  nextName: string,
): MicroflowAuthoringSchema {
  return updateCreateVariableAction(schema, variableId, action => ({ ...action, variableName: nextName }));
}

export function updateMicroflowVariableType(
  schema: MicroflowAuthoringSchema,
  variableId: string,
  nextType: MicroflowDataType,
): MicroflowAuthoringSchema {
  return updateCreateVariableAction(schema, variableId, action => ({ ...action, dataType: nextType }));
}

export function syncCreateVariableActionToDefinition(
  schema: MicroflowAuthoringSchema,
  objectId: string,
): MicroflowAuthoringSchema {
  const object = findObject(schema.objectCollection, objectId);
  if (!object || object.kind !== "actionActivity" || object.action.kind !== "createVariable") {
    return schema;
  }
  return refreshVariableIndex(schema);
}

export function syncVariableDefinitionToCreateVariableAction(
  schema: MicroflowAuthoringSchema,
  variableId: string,
): MicroflowAuthoringSchema {
  return updateCreateVariableAction(schema, variableId, action => action);
}

export function updateChangeVariableTarget(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  variableName: string,
): MicroflowAuthoringSchema {
  return refreshVariableIndex({
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.id === objectId && object.kind === "actionActivity" && object.action.kind === "changeVariable"
      ? { ...object, action: { ...object.action, targetVariableName: variableName } }
      : object),
  });
}

export function updateChangeVariableExpression(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  expression: MicroflowExpression,
): MicroflowAuthoringSchema {
  return refreshVariableIndex({
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.id === objectId && object.kind === "actionActivity" && object.action.kind === "changeVariable"
      ? { ...object, action: { ...object.action, newValueExpression: expression } }
      : object),
  });
}

export function updateCreateVariableConfig(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  patch: Partial<Pick<Extract<MicroflowAction, { kind: "createVariable" }>, "variableName" | "dataType" | "initialValue" | "documentation" | "readonly">>,
): MicroflowAuthoringSchema {
  return refreshVariableIndex({
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.id === objectId && object.kind === "actionActivity" && object.action.kind === "createVariable"
      ? { ...object, action: { ...object.action, ...patch } }
      : object),
  });
}

export function updateChangeVariableConfig(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  patch: Partial<Pick<Extract<MicroflowAction, { kind: "changeVariable" }>, "targetVariableName" | "newValueExpression" | "documentation">>,
): MicroflowAuthoringSchema {
  return refreshVariableIndex({
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.id === objectId && object.kind === "actionActivity" && object.action.kind === "changeVariable"
      ? { ...object, action: { ...object.action, ...patch } }
      : object),
  });
}

export const upsertMicroflowVariableDefinition = upsertMicroflowVariable;

export function removeMicroflowVariableDefinition(schema: MicroflowAuthoringSchema, variableIdOrSourceObjectId: string): MicroflowAuthoringSchema {
  return refreshVariableIndex({
    ...schema,
    objectCollection: removeCreateVariableObject(schema.objectCollection, variableIdOrSourceObjectId),
  });
}

export const renameMicroflowVariableDefinition = renameMicroflowVariable;

export function duplicateCreateVariableObject(schema: MicroflowAuthoringSchema, objectId: string): MicroflowAuthoringSchema {
  return refreshVariableIndex(duplicateObject(schema, objectId) as MicroflowAuthoringSchema);
}

export function getVariableNameConflicts(schema: MicroflowAuthoringSchema, variableName: string, excludeVariableId?: string): string[] {
  const normalized = normalizeName(variableName);
  if (!normalized) {
    return ["Variable name is required."];
  }
  const conflicts: string[] = [];
  for (const parameter of schema.parameters) {
    if (normalizeName(parameter.name) === normalized) {
      conflicts.push(`Conflicts with parameter "${parameter.name}".`);
    }
  }
  for (const symbol of getMicroflowVariables(schema)) {
    const sourceId = symbol.source.kind === "createVariable" ? symbol.source.actionId : symbol.id;
    const loopSourceId = symbol.source.kind === "loopIterator" ? symbol.source.loopObjectId : undefined;
    if (sourceId !== excludeVariableId && normalizeName(symbol.name) === normalized && !symbol.name.startsWith("$")) {
      if (loopSourceId && (excludeVariableId === loopSourceId || excludeVariableId === `loopIterator:${loopSourceId}`)) {
        continue;
      }
      conflicts.push(`Conflicts with variable "${symbol.name}".`);
    }
  }
  return conflicts;
}

export function getVariableReferences(schema: MicroflowAuthoringSchema, variableNameOrId: string): Array<{ objectId: string; actionId?: string; fieldPath: string }> {
  const references: Array<{ objectId: string; actionId?: string; fieldPath: string }> = [];
  const variable = getMicroflowVariables(schema).find(symbol =>
    symbol.name === variableNameOrId ||
    (symbol.source.kind === "createVariable" && symbol.source.actionId === variableNameOrId)
  );
  const variableName = variable?.name ?? variableNameOrId;
  const visit = (collection: MicroflowObjectCollection) => {
    for (const object of collection.objects) {
      if (object.kind === "loopedActivity") {
        visit(object.objectCollection);
      }
      for (const [fieldPath, expression] of expressionsForObject(object)) {
        if (expression.raw.includes(variableName) || expression.raw.includes(`$${variableName}`)) {
          references.push({ objectId: object.id, fieldPath });
        }
      }
      if (object.kind !== "actionActivity") {
        continue;
      }
      if (object.action.kind === "changeVariable" && object.action.targetVariableName === variableName) {
        references.push({ objectId: object.id, actionId: object.action.id, fieldPath: "action.targetVariableName" });
      }
      for (const [fieldPath, expression] of expressionsForAction(object.action)) {
        if (expression.raw.includes(variableName) || expression.raw.includes(`$${variableName}`)) {
          references.push({ objectId: object.id, actionId: object.action.id, fieldPath });
        }
      }
    }
  };
  visit(schema.objectCollection);
  return references;
}

export const findVariableTextReferences = getVariableReferences;

export function getStaleVariableReferences(schema: MicroflowAuthoringSchema): Array<{ objectId: string; actionId?: string; fieldPath: string; variableName: string }> {
  const availableNames = new Set(getMicroflowVariables(schema).map(symbol => symbol.name));
  const staleReferences: Array<{ objectId: string; actionId?: string; fieldPath: string; variableName: string }> = [];
  const visit = (collection: MicroflowObjectCollection) => {
    for (const object of collection.objects) {
      if (object.kind === "loopedActivity") {
        visit(object.objectCollection);
      }
      if (object.kind === "actionActivity" && object.action.kind === "changeVariable" && object.action.targetVariableName && !availableNames.has(object.action.targetVariableName)) {
        staleReferences.push({ objectId: object.id, actionId: object.action.id, fieldPath: "action.targetVariableName", variableName: object.action.targetVariableName });
      }
    }
  };
  visit(schema.objectCollection);
  return staleReferences;
}

function expressionsForObject(object: MicroflowObject): Array<[string, MicroflowExpression]> {
  if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression") {
    return [["splitCondition.expression", object.splitCondition.expression]];
  }
  if (object.kind === "endEvent" && object.returnValue) {
    return [["returnValue", object.returnValue]];
  }
  if (object.kind === "errorEvent" && object.error.messageExpression) {
    return [["error.messageExpression", object.error.messageExpression]];
  }
  if (object.kind === "loopedActivity" && object.loopSource.kind === "whileCondition") {
    return [["loopSource.expression", object.loopSource.expression]];
  }
  return [];
}

function expressionsForAction(action: MicroflowAction): Array<[string, MicroflowExpression]> {
  if (action.kind === "createVariable" && action.initialValue) {
    return [["action.initialValue", action.initialValue]];
  }
  if (action.kind === "changeVariable") {
    return [["action.newValueExpression", action.newValueExpression]];
  }
  return [];
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
