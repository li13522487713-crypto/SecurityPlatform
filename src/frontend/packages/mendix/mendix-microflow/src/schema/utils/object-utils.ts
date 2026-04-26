import type { MicroflowActionActivity, MicroflowLoopedActivity, MicroflowObject, MicroflowObjectCollection } from "../types";

export function isActionActivity(object: MicroflowObject): object is MicroflowActionActivity {
  return object.kind === "actionActivity";
}

export function isLoopedActivity(object: MicroflowObject): object is MicroflowLoopedActivity {
  return object.kind === "loopedActivity";
}

export function collectObjectsRecursive(collection: MicroflowObjectCollection): MicroflowObject[] {
  return collection.objects.flatMap(object => object.kind === "loopedActivity"
    ? [object, ...collectObjectsRecursive(object.objectCollection)]
    : [object]);
}

export function findObjectById(collection: MicroflowObjectCollection, objectId: string): MicroflowObject | undefined {
  return collectObjectsRecursive(collection).find(object => object.id === objectId);
}
