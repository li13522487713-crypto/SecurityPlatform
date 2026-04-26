import type { MicroflowObject, MicroflowObjectCollection, MicroflowSchema, MicroflowValidationIssue } from "../schema/types";

export function issue(
  code: MicroflowValidationIssue["code"],
  message: string,
  target: Partial<Pick<MicroflowValidationIssue, "objectId" | "flowId" | "actionId" | "fieldPath" | "nodeId" | "edgeId">> = {},
  severity: MicroflowValidationIssue["severity"] = "error"
): MicroflowValidationIssue {
  return {
    id: `${code}:${target.objectId ?? target.flowId ?? target.actionId ?? target.nodeId ?? target.edgeId ?? target.fieldPath ?? "schema"}`,
    code,
    message,
    severity,
    ...target
  };
}

export function flattenObjects(collection: MicroflowObjectCollection): Array<{ object: MicroflowObject; loopObjectId?: string; collectionId: string }> {
  const result: Array<{ object: MicroflowObject; loopObjectId?: string; collectionId: string }> = [];
  for (const object of collection.objects) {
    if (object.kind !== "loopedActivity") {
      result.push({ object, collectionId: collection.id });
      continue;
    }
    result.push({ object, collectionId: collection.id });
    result.push(...flattenObjects(object.objectCollection).map(item => ({ ...item, loopObjectId: item.loopObjectId ?? object.id })));
  }
  return result;
}

export function objectMap(schema: MicroflowSchema): Map<string, MicroflowObject> {
  return new Map(flattenObjects(schema.objectCollection).map(item => [item.object.id, item.object]));
}

export interface MicroflowObjectLocation {
  object: MicroflowObject;
  collectionId: string;
  parentLoopObjectId?: string;
  ancestorLoopObjectIds: string[];
}

export function flattenObjectLocations(
  collection: MicroflowObjectCollection,
  ancestorLoopObjectIds: string[] = [],
  parentLoopObjectId?: string
): MicroflowObjectLocation[] {
  return collection.objects.flatMap(object => {
    const location: MicroflowObjectLocation = {
      object,
      collectionId: collection.id,
      parentLoopObjectId,
      ancestorLoopObjectIds,
    };
    if (object.kind !== "loopedActivity") {
      return [location];
    }
    return [
      location,
      ...flattenObjectLocations(object.objectCollection, [...ancestorLoopObjectIds, object.id], object.id),
    ];
  });
}

export function objectLocationMap(schema: MicroflowSchema): Map<string, MicroflowObjectLocation> {
  return new Map(flattenObjectLocations(schema.objectCollection).map(item => [item.object.id, item]));
}
