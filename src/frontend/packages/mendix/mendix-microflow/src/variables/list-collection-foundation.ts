import type {
  MicroflowAction,
  MicroflowActionActivity,
  MicroflowAuthoringSchema,
  MicroflowDataType,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowVariableSymbol,
} from "../schema/types";
import { buildMicroflowVariableIndex } from "./microflow-variable-foundation";

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

function refreshVariableIndex(schema: MicroflowAuthoringSchema): MicroflowAuthoringSchema {
  return {
    ...schema,
    variables: buildMicroflowVariableIndex(schema),
  };
}

function updateListAction<TKind extends MicroflowAction["kind"]>(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  actionKind: TKind,
  updater: (action: Extract<MicroflowAction, { kind: TKind }>, object: MicroflowActionActivity) => Extract<MicroflowAction, { kind: TKind }>,
): MicroflowAuthoringSchema {
  return refreshVariableIndex({
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => {
      if (object.kind !== "actionActivity" || object.action.kind !== actionKind || (object.id !== objectId && object.action.id !== objectId)) {
        return object;
      }
      return { ...object, action: updater(object.action as Extract<MicroflowAction, { kind: TKind }>, object) };
    }),
  });
}

export function buildListVariableIndex(schema: MicroflowAuthoringSchema): MicroflowVariableSymbol[] {
  return Object.values(buildMicroflowVariableIndex(schema).listOutputs);
}

export function upsertListVariable(
  schema: MicroflowAuthoringSchema,
  variable: { id: string; name: string; elementType: MicroflowDataType; description?: string; readonly?: boolean },
): MicroflowAuthoringSchema {
  return updateListAction(schema, variable.id, "createList", action => ({
    ...action,
    outputListVariableName: variable.name.trim(),
    listVariableName: variable.name.trim(),
    elementType: variable.elementType,
    itemType: variable.elementType,
    listType: variable.readonly ? "readonly" : "mutable",
    description: variable.description ?? action.description,
  }));
}

export function removeListVariable(schema: MicroflowAuthoringSchema, variableId: string): MicroflowAuthoringSchema {
  const variable = buildListVariableIndex(schema).find(symbol => symbol.source.kind === "createList" && symbol.source.actionId === variableId);
  if (!variable) {
    return refreshVariableIndex(schema);
  }
  return refreshVariableIndex({
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => {
      if (object.kind !== "actionActivity") {
        return object;
      }
      if (object.action.kind === "changeList" && object.action.targetListVariableName === variable.name) {
        return { ...object, action: { ...object.action, targetListVariableName: "" } };
      }
      if (object.action.kind === "aggregateList" && object.action.listVariableName === variable.name) {
        return { ...object, action: { ...object.action, listVariableName: "", sourceListVariableName: "" } };
      }
      if (object.action.kind === "listOperation" && object.action.leftListVariableName === variable.name) {
        return { ...object, action: { ...object.action, leftListVariableName: "", sourceListVariableName: "" } };
      }
      return object;
    }),
  });
}

export function getCompatibleListVariables(
  schema: MicroflowAuthoringSchema,
  elementType?: MicroflowDataType,
): MicroflowVariableSymbol[] {
  return buildListVariableIndex(schema).filter(symbol => {
    if (!elementType || symbol.dataType.kind !== "list") {
      return true;
    }
    return symbol.dataType.itemType.kind === elementType.kind;
  });
}

export function updateCreateListVariableName(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  name: string,
): MicroflowAuthoringSchema {
  const nextName = name.trim();
  return updateListAction(schema, objectId, "createList", action => ({
    ...action,
    outputListVariableName: nextName,
    listVariableName: nextName,
  }));
}

export function updateCreateListElementType(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  elementType: MicroflowDataType,
): MicroflowAuthoringSchema {
  return updateListAction(schema, objectId, "createList", action => ({
    ...action,
    elementType,
    itemType: elementType,
  }));
}

export function updateChangeListTarget(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  targetListVariableName: string,
): MicroflowAuthoringSchema {
  return updateListAction(schema, objectId, "changeList", action => ({
    ...action,
    targetListVariableName,
  }));
}

export function updateChangeListOperation(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  operation: Extract<MicroflowAction, { kind: "changeList" }>["operation"],
): MicroflowAuthoringSchema {
  return updateListAction(schema, objectId, "changeList", action => ({
    ...action,
    operation,
  }));
}

export function updateAggregateListSource(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  sourceListVariableName: string,
): MicroflowAuthoringSchema {
  return updateListAction(schema, objectId, "aggregateList", action => ({
    ...action,
    listVariableName: sourceListVariableName,
    sourceListVariableName,
  }));
}

export function updateAggregateFunction(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  aggregateFunction: Extract<MicroflowAction, { kind: "aggregateList" }>["aggregateFunction"],
): MicroflowAuthoringSchema {
  const resultType: MicroflowDataType = aggregateFunction === "count"
    ? { kind: "integer" }
    : aggregateFunction === "sum" || aggregateFunction === "average"
      ? { kind: "decimal" }
      : { kind: "unknown", reason: "aggregate result type" };
  return updateListAction(schema, objectId, "aggregateList", action => ({
    ...action,
    aggregateFunction,
    resultType,
  }));
}

export function updateAggregateResultVariable(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  variable: { id?: string; name: string; dataType?: MicroflowDataType },
): MicroflowAuthoringSchema {
  return updateListAction(schema, objectId, "aggregateList", action => ({
    ...action,
    outputVariableName: variable.name.trim(),
    resultVariableName: variable.name.trim(),
    resultVariableId: variable.id ?? action.id,
    resultType: variable.dataType ?? action.resultType,
  }));
}

export function updateListOperationSource(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  sourceListVariableName: string,
): MicroflowAuthoringSchema {
  return updateListAction(schema, objectId, "listOperation", action => ({
    ...action,
    leftListVariableName: sourceListVariableName,
    sourceListVariableName,
  }));
}

export function updateListOperationOutputVariable(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  variable: { id?: string; name: string; elementType?: MicroflowDataType },
): MicroflowAuthoringSchema {
  return updateListAction(schema, objectId, "listOperation", action => ({
    ...action,
    outputVariableName: variable.name.trim(),
    outputListVariableName: variable.name.trim(),
    targetListVariableId: variable.id ?? action.id,
    outputElementType: variable.elementType ?? action.outputElementType,
  }));
}

export function getListVariableWarnings(schema: MicroflowAuthoringSchema, objectId: string): string[] {
  const index = buildMicroflowVariableIndex(schema);
  const warnings: string[] = [];
  const objects = flattenObjects(schema.objectCollection);
  const object = objects.find(item => item.id === objectId);
  if (!object || object.kind !== "actionActivity") {
    return warnings;
  }
  const listNames = new Set(Object.values(index.listOutputs).map(symbol => symbol.name));
  if (object.action.kind === "changeList" && (!object.action.targetListVariableName || !listNames.has(object.action.targetListVariableName))) {
    warnings.push("Selected target list is missing or is not a List variable.");
  }
  if (object.action.kind === "aggregateList" && (!object.action.listVariableName || !listNames.has(object.action.listVariableName))) {
    warnings.push("Selected source list is missing or is not a List variable.");
  }
  if (object.action.kind === "listOperation" && (!object.action.leftListVariableName || !listNames.has(object.action.leftListVariableName))) {
    warnings.push("Selected source list is missing or is not a List variable.");
  }
  return warnings;
}

function flattenObjects(collection: MicroflowObjectCollection): MicroflowObject[] {
  return collection.objects.flatMap(object => object.kind === "loopedActivity" ? [object, ...flattenObjects(object.objectCollection)] : [object]);
}
