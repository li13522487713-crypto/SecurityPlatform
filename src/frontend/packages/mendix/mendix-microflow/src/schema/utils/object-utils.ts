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
  const safeObjects = Array.isArray(collection?.objects) ? collection.objects : [];
  return safeObjects.flatMap(object => object.kind === "loopedActivity"
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
  const rootFlows = Array.isArray(schema.flows) ? schema.flows : [];
  const safeObjectCollection = schema.objectCollection ?? {
    id: "root-collection",
    officialType: "Microflows$MicroflowObjectCollection",
    objects: [],
    flows: [],
  };
  const rootFlow = rootFlows.find(flow => flow.id === flowId);
  if (rootFlow) {
    return { flow: rootFlow, collection: safeObjectCollection, collectionId: safeObjectCollection.id };
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
  const safeFlows = Array.isArray(schema.flows) ? schema.flows : [];
  if (collectionId === schema.objectCollection.id) {
    return { ...schema, flows: [...safeFlows, flow] };
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
  const safeFlows = Array.isArray(schema.flows) ? schema.flows : [];
  if (collectionId === schema.objectCollection.id) {
    return { ...schema, flows: safeFlows.filter(flow => flow.id !== flowId) };
  }
  return {
    ...schema,
    objectCollection: mapCollectionById(schema.objectCollection, collectionId, collection => ({
      ...collection,
      flows: (collection.flows ?? []).filter(flow => flow.id !== flowId),
    })),
  };
}

export function collectFlowsRecursive(schema: Partial<MicroflowAuthoringSchema>): MicroflowFlow[] {
  const safeFlows = Array.isArray(schema.flows) ? schema.flows : [];
  return [...safeFlows, ...collectCollectionFlowsRecursive(schema.objectCollection)];
}

function collectCollectionFlowsRecursive(collection?: MicroflowObjectCollection): MicroflowFlow[] {
  if (!collection) {
    return [];
  }
  const safeObjects = Array.isArray(collection?.objects) ? collection.objects : [];
  const safeFlows = Array.isArray(collection?.flows) ? collection.flows : [];
  return [
    ...safeFlows,
    ...safeObjects.flatMap(object => object.kind === "loopedActivity"
      ? collectCollectionFlowsRecursive(object.objectCollection)
      : []),
  ];
}

function findCollectionById(
  collection: MicroflowObjectCollection | undefined,
  collectionId: string,
): MicroflowObjectCollection | undefined {
  if (!collection) {
    return undefined;
  }
  const safeObjects = Array.isArray(collection?.objects) ? collection.objects : [];
  if (collection.id === collectionId) {
    return collection;
  }
  for (const object of safeObjects) {
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
  collection: MicroflowObjectCollection | undefined,
  objectId: string,
  parentLoopObjectId?: string,
): MicroflowObjectWithCollection | undefined {
  if (!collection) {
    return undefined;
  }
  const safeObjects = Array.isArray(collection?.objects) ? collection.objects : [];
  for (const object of safeObjects) {
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
  collection: MicroflowObjectCollection | undefined,
  flowId: string,
  parentLoopObjectId?: string,
): MicroflowFlowWithCollection | undefined {
  if (!collection) {
    return undefined;
  }
  const safeFlows = Array.isArray(collection.flows) ? collection.flows : [];
  const flow = safeFlows.find(item => item.id === flowId);
  if (flow) {
    return { flow, collection, collectionId: collection.id, parentLoopObjectId };
  }
  const safeObjects = Array.isArray(collection?.objects) ? collection.objects : [];
  for (const object of safeObjects) {
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
  const fallbackCollection: MicroflowObjectCollection = {
    id: "root-collection",
    officialType: "Microflows$MicroflowObjectCollection",
    objects: [],
    flows: [],
  };
  const safeCollection = collection ?? fallbackCollection;
  const safeObjects = Array.isArray(collection?.objects) ? collection.objects : [];
  if (safeCollection.id === collectionId) {
    return mapper(safeCollection);
  }
  return {
    ...safeCollection,
    objects: safeObjects.map(object => object.kind === "loopedActivity"
      ? { ...object, objectCollection: mapCollectionById(object.objectCollection, collectionId, mapper) }
      : object),
  };
}
