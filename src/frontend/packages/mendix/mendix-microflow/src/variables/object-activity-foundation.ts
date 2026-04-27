import {
  getAssociationByQualifiedName,
  getAttributeByQualifiedName,
  getEntityByQualifiedName,
  type MicroflowMetadataCatalog,
} from "../metadata";
import type {
  MicroflowAction,
  MicroflowActionActivity,
  MicroflowAuthoringSchema,
  MicroflowDataType,
  MicroflowMemberChange,
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

function flattenObjects(collection: MicroflowObjectCollection): MicroflowObject[] {
  return collection.objects.flatMap(object => object.kind === "loopedActivity" ? [object, ...flattenObjects(object.objectCollection)] : [object]);
}

function refreshVariableIndex(schema: MicroflowAuthoringSchema): MicroflowAuthoringSchema {
  return {
    ...schema,
    variables: buildMicroflowVariableIndex(schema),
  };
}

function updateObjectAction<TKind extends MicroflowAction["kind"]>(
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

export function buildObjectVariableIndex(schema: MicroflowAuthoringSchema): MicroflowVariableSymbol[] {
  const index = buildMicroflowVariableIndex(schema);
  return [
    ...Object.values(index.objectOutputs),
    ...Object.values(index.listOutputs).filter(symbol => symbol.dataType.kind === "list" && symbol.dataType.itemType.kind === "object"),
  ];
}

export function getObjectVariables(schema: MicroflowAuthoringSchema, entityQualifiedName?: string): MicroflowVariableSymbol[] {
  return Object.values(buildMicroflowVariableIndex(schema).objectOutputs).filter(symbol => {
    if (!entityQualifiedName || symbol.dataType.kind !== "object") {
      return true;
    }
    return symbol.dataType.entityQualifiedName === entityQualifiedName;
  });
}

export function getListObjectVariables(schema: MicroflowAuthoringSchema, entityQualifiedName?: string): MicroflowVariableSymbol[] {
  return Object.values(buildMicroflowVariableIndex(schema).listOutputs).filter(symbol => {
    if (symbol.dataType.kind !== "list" || symbol.dataType.itemType.kind !== "object") {
      return false;
    }
    return !entityQualifiedName || symbol.dataType.itemType.entityQualifiedName === entityQualifiedName;
  });
}

export function updateObjectActionEntity(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  entityRef: { qualifiedName: string },
): MicroflowAuthoringSchema {
  return updateObjectAction(schema, objectId, "createObject", action => ({
    ...action,
    entityQualifiedName: entityRef.qualifiedName,
    memberChanges: action.memberChanges.filter(change => !change.memberQualifiedName || change.memberQualifiedName.startsWith(`${entityRef.qualifiedName}.`)),
  }));
}

export function updateObjectOutputVariable(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  variable: { name: string; dataType?: MicroflowDataType },
): MicroflowAuthoringSchema {
  return refreshVariableIndex({
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => {
      if (object.kind !== "actionActivity" || (object.id !== objectId && object.action.id !== objectId)) {
        return object;
      }
      if (object.action.kind === "createObject") {
        return { ...object, action: { ...object.action, outputVariableName: variable.name.trim() } };
      }
      if (object.action.kind === "retrieve") {
        return { ...object, action: { ...object.action, outputVariableName: variable.name.trim() } };
      }
      return object;
    }),
  });
}

export function upsertObjectMemberChange(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  memberChange: MicroflowMemberChange,
): MicroflowAuthoringSchema {
  return refreshVariableIndex({
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => {
      if (object.kind !== "actionActivity" || (object.id !== objectId && object.action.id !== objectId)) {
        return object;
      }
      if (object.action.kind !== "createObject" && object.action.kind !== "changeMembers") {
        return object;
      }
      const exists = object.action.memberChanges.some(change => change.id === memberChange.id);
      const memberChanges = exists
        ? object.action.memberChanges.map(change => change.id === memberChange.id ? memberChange : change)
        : [...object.action.memberChanges, memberChange];
      return { ...object, action: { ...object.action, memberChanges } };
    }),
  });
}

export function removeObjectMemberChange(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  memberKey: string,
): MicroflowAuthoringSchema {
  return refreshVariableIndex({
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => {
      if (object.kind !== "actionActivity" || (object.id !== objectId && object.action.id !== objectId)) {
        return object;
      }
      if (object.action.kind !== "createObject" && object.action.kind !== "changeMembers") {
        return object;
      }
      return { ...object, action: { ...object.action, memberChanges: object.action.memberChanges.filter(change => change.id !== memberKey && change.memberQualifiedName !== memberKey) } };
    }),
  });
}

export function updateRetrieveObjectSource(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  source: Extract<MicroflowAction, { kind: "retrieve" }>["retrieveSource"],
): MicroflowAuthoringSchema {
  return updateObjectAction(schema, objectId, "retrieve", action => ({ ...action, retrieveSource: source }));
}

export function updateRetrieveObjectFilter(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  expression: Extract<Extract<MicroflowAction, { kind: "retrieve" }>["retrieveSource"], { kind: "database" }>["xPathConstraint"],
): MicroflowAuthoringSchema {
  return updateObjectAction(schema, objectId, "retrieve", action => action.retrieveSource.kind === "database"
    ? { ...action, retrieveSource: { ...action.retrieveSource, xPathConstraint: expression } }
    : action);
}

export function updateRetrieveObjectRange(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  range: Extract<Extract<MicroflowAction, { kind: "retrieve" }>["retrieveSource"], { kind: "database" }>["range"],
): MicroflowAuthoringSchema {
  return updateObjectAction(schema, objectId, "retrieve", action => action.retrieveSource.kind === "database"
    ? { ...action, retrieveSource: { ...action.retrieveSource, range } }
    : action);
}

export function updateCommitObjectTarget(schema: MicroflowAuthoringSchema, objectId: string, variableRef: { name: string }): MicroflowAuthoringSchema {
  return updateObjectAction(schema, objectId, "commit", action => ({ ...action, objectOrListVariableName: variableRef.name }));
}

export function updateDeleteObjectTarget(schema: MicroflowAuthoringSchema, objectId: string, variableRef: { name: string }): MicroflowAuthoringSchema {
  return updateObjectAction(schema, objectId, "delete", action => ({ ...action, objectOrListVariableName: variableRef.name }));
}

export function resolveEntityMetadata(metadataCatalog: MicroflowMetadataCatalog, entityQualifiedName?: string) {
  return getEntityByQualifiedName(metadataCatalog, entityQualifiedName);
}

export function resolveMemberMetadata(metadataCatalog: MicroflowMetadataCatalog, memberQualifiedName?: string) {
  return getAttributeByQualifiedName(metadataCatalog, memberQualifiedName) ?? getAssociationByQualifiedName(metadataCatalog, memberQualifiedName);
}

export function buildObjectActionWarnings(schema: MicroflowAuthoringSchema, objectId: string, metadataCatalog: MicroflowMetadataCatalog): string[] {
  const object = flattenObjects(schema.objectCollection).find(item => item.id === objectId);
  if (!object || object.kind !== "actionActivity") {
    return [];
  }
  const action = object.action;
  const warnings: string[] = [];
  const objectVariables = new Set(getObjectVariables(schema).map(symbol => symbol.name));
  const objectOrListVariables = new Set([...getObjectVariables(schema), ...getListObjectVariables(schema)].map(symbol => symbol.name));
  const checkMemberChanges = (memberChanges: MicroflowMemberChange[]) => {
    for (const change of memberChanges) {
      if (!change.memberQualifiedName) {
        warnings.push("Member change is missing a selected attribute or association.");
      } else if (!resolveMemberMetadata(metadataCatalog, change.memberQualifiedName)) {
        warnings.push(`Metadata unavailable / stale member: ${change.memberQualifiedName}`);
      }
      if (change.assignmentKind !== "clear" && !change.valueExpression?.raw.trim()) {
        warnings.push(`Member "${change.memberQualifiedName || change.id}" has an empty value expression.`);
      }
    }
  };

  if (action.kind === "createObject") {
    if (!action.entityQualifiedName) {
      warnings.push("Create Object entity is required.");
    } else if (!resolveEntityMetadata(metadataCatalog, action.entityQualifiedName)) {
      warnings.push(`Metadata unavailable / stale entity: ${action.entityQualifiedName}`);
    }
    checkMemberChanges(action.memberChanges);
  }
  if (action.kind === "retrieve" && action.retrieveSource.kind === "database") {
    if (!action.retrieveSource.entityQualifiedName) {
      warnings.push("Retrieve Object entity is required.");
    } else if (!resolveEntityMetadata(metadataCatalog, action.retrieveSource.entityQualifiedName)) {
      warnings.push(`Metadata unavailable / stale entity: ${action.retrieveSource.entityQualifiedName}`);
    }
  }
  if (action.kind === "retrieve" && action.retrieveSource.kind === "association" && action.retrieveSource.associationQualifiedName && !getAssociationByQualifiedName(metadataCatalog, action.retrieveSource.associationQualifiedName)) {
    warnings.push(`Metadata unavailable / stale association: ${action.retrieveSource.associationQualifiedName}`);
  }
  if (action.kind === "changeMembers") {
    if (!action.changeVariableName || !objectVariables.has(action.changeVariableName)) {
      warnings.push("Change Members target is missing or is not an Object variable.");
    }
    checkMemberChanges(action.memberChanges);
  }
  if ((action.kind === "commit" || action.kind === "delete" || action.kind === "rollback") && (!action.objectOrListVariableName || !objectOrListVariables.has(action.objectOrListVariableName))) {
    warnings.push(`${action.kind} target is missing or is not an Object/List<Object> variable.`);
  }
  return warnings;
}
