import type {
  MicroflowActionActivity,
  MicroflowAuthoringSchema,
  MicroflowFlow,
  MicroflowLoopedActivity,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowSchema,
} from "../types";

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

export interface MicroflowObjectWithCollection {
  object: MicroflowObject;
  collection: MicroflowObjectCollection;
  collectionId: string;
  parentLoopObjectId?: string;
}

export interface MicroflowFlowWithCollection {
  flow: MicroflowFlow;
  collection: MicroflowObjectCollection;
  collectionId: string;
  parentLoopObjectId?: string;
}

export function getRootObjectCollection(schema: MicroflowSchema): MicroflowObjectCollection {
  return schema.objectCollection;
}

export function getObjectCollectionById(
  schema: MicroflowSchema,
  collectionId: string,
): MicroflowObjectCollection | undefined {
  return findCollectionById(schema.objectCollection, collectionId);
}

export function getObjectCollectionForObject(
  schema: MicroflowSchema,
  objectId: string,
): MicroflowObjectCollection | undefined {
  return findObjectWithCollection(schema, objectId)?.collection;
}

export function findObjectWithCollection(
  schema: MicroflowSchema,
  objectId: string,
): MicroflowObjectWithCollection | undefined {
  return findObjectWithCollectionIn(schema.objectCollection, objectId);
}

export function findFlowWithCollection(
  schema: MicroflowSchema,
  flowId: string,
): MicroflowFlowWithCollection | undefined {
  const rootFlow = schema.flows.find(flow => flow.id === flowId);
  if (rootFlow) {
    return { flow: rootFlow, collection: schema.objectCollection, collectionId: schema.objectCollection.id };
  }
  return findFlowWithCollectionIn(schema.objectCollection, flowId);
}

export function addObjectToCollection(
  schema: MicroflowSchema,
  collectionId: string,
  object: MicroflowObject,
): MicroflowSchema {
  return {
    ...schema,
    objectCollection: mapCollectionById(schema.objectCollection, collectionId, collection => ({
      ...collection,
      objects: [...collection.objects, object],
    })),
  };
}

export function updateObjectInCollection(
  schema: MicroflowSchema,
  collectionId: string,
  objectId: string,
  patch: Partial<MicroflowObject>,
): MicroflowSchema {
  return {
    ...schema,
    objectCollection: mapCollectionById(schema.objectCollection, collectionId, collection => ({
      ...collection,
      objects: collection.objects.map(object => object.id === objectId ? ({ ...object, ...patch } as MicroflowObject) : object),
    })),
  };
}

export function removeObjectFromCollection(
  schema: MicroflowSchema,
  collectionId: string,
  objectId: string,
): MicroflowSchema {
  return {
    ...schema,
    objectCollection: mapCollectionById(schema.objectCollection, collectionId, collection => ({
      ...collection,
      objects: collection.objects.filter(object => object.id !== objectId),
    })),
  };
}

export function addFlowToCollection(
  schema: MicroflowSchema,
  collectionId: string,
  flow: MicroflowFlow,
): MicroflowSchema {
  if (collectionId === schema.objectCollection.id) {
    return { ...schema, flows: [...schema.flows, flow] };
  }
  return {
    ...schema,
    objectCollection: mapCollectionById(schema.objectCollection, collectionId, collection => ({
      ...collection,
      flows: [...(collection.flows ?? []), flow],
    })),
  };
}

export function removeFlowFromCollection(
  schema: MicroflowSchema,
  collectionId: string,
  flowId: string,
): MicroflowSchema {
  if (collectionId === schema.objectCollection.id) {
    return { ...schema, flows: schema.flows.filter(flow => flow.id !== flowId) };
  }
  return {
    ...schema,
    objectCollection: mapCollectionById(schema.objectCollection, collectionId, collection => ({
      ...collection,
      flows: (collection.flows ?? []).filter(flow => flow.id !== flowId),
    })),
  };
}

export function collectFlowsRecursive(schema: Pick<MicroflowAuthoringSchema, "objectCollection" | "flows">): MicroflowFlow[] {
  return [...schema.flows, ...collectCollectionFlowsRecursive(schema.objectCollection)];
}

function collectCollectionFlowsRecursive(collection: MicroflowObjectCollection): MicroflowFlow[] {
  return [
    ...(collection.flows ?? []),
    ...collection.objects.flatMap(object => object.kind === "loopedActivity"
      ? collectCollectionFlowsRecursive(object.objectCollection)
      : []),
  ];
}

function findCollectionById(
  collection: MicroflowObjectCollection,
  collectionId: string,
): MicroflowObjectCollection | undefined {
  if (collection.id === collectionId) {
    return collection;
  }
  for (const object of collection.objects) {
    if (object.kind === "loopedActivity") {
      const found = findCollectionById(object.objectCollection, collectionId);
      if (found) {
        return found;
      }
    }
  }
  return undefined;
}

function findObjectWithCollectionIn(
  collection: MicroflowObjectCollection,
  objectId: string,
  parentLoopObjectId?: string,
): MicroflowObjectWithCollection | undefined {
  for (const object of collection.objects) {
    if (object.id === objectId) {
      return { object, collection, collectionId: collection.id, parentLoopObjectId };
    }
    if (object.kind === "loopedActivity") {
      const found = findObjectWithCollectionIn(object.objectCollection, objectId, object.id);
      if (found) {
        return found;
      }
    }
  }
  return undefined;
}

function findFlowWithCollectionIn(
  collection: MicroflowObjectCollection,
  flowId: string,
  parentLoopObjectId?: string,
): MicroflowFlowWithCollection | undefined {
  const flow = collection.flows?.find(item => item.id === flowId);
  if (flow) {
    return { flow, collection, collectionId: collection.id, parentLoopObjectId };
  }
  for (const object of collection.objects) {
    if (object.kind === "loopedActivity") {
      const found = findFlowWithCollectionIn(object.objectCollection, flowId, object.id);
      if (found) {
        return found;
      }
    }
  }
  return undefined;
}

function mapCollectionById(
  collection: MicroflowObjectCollection,
  collectionId: string,
  mapper: (collection: MicroflowObjectCollection) => MicroflowObjectCollection,
): MicroflowObjectCollection {
  if (collection.id === collectionId) {
    return mapper(collection);
  }
  return {
    ...collection,
    objects: collection.objects.map(object => object.kind === "loopedActivity"
      ? { ...object, objectCollection: mapCollectionById(object.objectCollection, collectionId, mapper) }
      : object),
  };
}
