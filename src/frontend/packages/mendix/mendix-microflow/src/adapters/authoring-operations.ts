import {
  getMicroflowNodeRegistryKey,
  microflowActionRegistryByActivityType,
  type MicroflowNodeRegistryEntry
} from "../node-registry";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import { buildVariableIndex } from "../variables";
import type { MicroflowMetadataCatalog } from "../metadata";
import { canonicalizeFlowLine } from "../flowgram/FlowGramMicroflowTypes";
import type {
  MicroflowAction,
  MicroflowAnnotationFlow,
  MicroflowCaseValue,
  MicroflowDataType,
  MicroflowEditorGraphPatch,
  MicroflowExpression,
  MicroflowFlow,
  MicroflowLine,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowParameter,
  MicroflowPoint,
  MicroflowDesignSchema,
  MicroflowSchema,
  MicroflowSequenceFlow,
  MicroflowSize
} from "../schema/types";
import {
  addFlowToCollection,
  collectFlowsRecursive,
  findFlowWithCollection,
  findObjectWithCollection,
  removeFlowFromCollection,
} from "../schema/utils/object-utils";
import { normalizeDesignSchemaVariables } from "../schema/utils/design-schema-variables";
import { caseValueIdentity } from "../schema/utils/case-utils";
import { createStableId } from "../schema/utils/ids";
import { emptyVariableIndex, flattenObjectCollection, toEditorGraph } from "./microflow-adapters";

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

export function defaultMicroflowLine(points: MicroflowPoint[] = []): MicroflowLine {
  return {
    kind: "orthogonal",
    points,
    routing: { mode: "auto", bendPoints: [] },
    style: { strokeType: "solid", strokeWidth: 2, arrow: "target" }
  };
}

function mapObjectCollection(
  collection: MicroflowObjectCollection,
  objectId: string,
  mapper: (object: MicroflowObject) => MicroflowObject
): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects.map(object => {
      const mapped = object.id === objectId ? mapper(object) : object;
      if (mapped.kind === "loopedActivity") {
        return {
          ...mapped,
          objectCollection: mapObjectCollection(mapped.objectCollection, objectId, mapper)
        };
      }
      return mapped;
    })
  };
}

function removeObjectFromCollection(collection: MicroflowObjectCollection, objectId: string): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects
      .filter(object => object.id !== objectId)
      .map(object => object.kind === "loopedActivity"
        ? { ...object, objectCollection: removeObjectFromCollection(object.objectCollection, objectId) }
        : object)
  };
}

function removeFlowsFromCollection(collection: MicroflowObjectCollection, removedObjectIds: Set<string>): MicroflowObjectCollection {
  return {
    ...collection,
    flows: collection.flows?.filter(flow => !removedObjectIds.has(flow.originObjectId) && !removedObjectIds.has(flow.destinationObjectId)),
    objects: collection.objects.map(object => object.kind === "loopedActivity"
      ? { ...object, objectCollection: removeFlowsFromCollection(object.objectCollection, removedObjectIds) }
      : object),
  };
}

function collectObjectAndDescendantIds(collection: MicroflowObjectCollection, objectId: string): Set<string> {
  for (const object of collection.objects) {
    if (object.id === objectId) {
      return collectDescendantIds(object);
    }
    if (object.kind === "loopedActivity") {
      const nested = collectObjectAndDescendantIds(object.objectCollection, objectId);
      if (nested.size > 0) {
        return nested;
      }
    }
  }
  return new Set();
}

function collectDescendantIds(object: MicroflowObject): Set<string> {
  const ids = new Set([object.id]);
  if (object.kind === "loopedActivity") {
    for (const child of object.objectCollection.objects) {
      for (const childId of collectDescendantIds(child)) {
        ids.add(childId);
      }
    }
  }
  return ids;
}

function collectSchemaIds(schema: MicroflowSchema): Set<string> {
  const ids = new Set<string>([
    schema.id,
    schema.stableId,
    schema.objectCollection.id,
    ...schema.parameters.flatMap(parameter => [parameter.id, parameter.stableId].filter((value): value is string => Boolean(value))),
    ...collectFlowsRecursive(schema).flatMap(flow => [flow.id, flow.stableId].filter((value): value is string => Boolean(value))),
  ].filter((value): value is string => Boolean(value)));

  for (const object of flattenObjectCollection(schema.objectCollection)) {
    ids.add(object.id);
    ids.add(object.stableId);
    if (object.kind === "actionActivity") {
      ids.add(object.action.id);
    }
    if (object.kind === "loopedActivity") {
      ids.add(object.objectCollection.id);
    }
  }

  return ids;
}

function uniqueSchemaId(schema: MicroflowSchema, prefix: string): string {
  const existingIds = collectSchemaIds(schema);
  for (let attempt = 0; attempt < 20; attempt += 1) {
    const id = createStableId(prefix);
    if (!existingIds.has(id)) {
      return id;
    }
  }
  throw new Error(`Unable to generate unique id for ${prefix}.`);
}

export function createMicroflowObjectId(schema: MicroflowSchema, prefix = "object"): string {
  return uniqueSchemaId(schema, prefix);
}

export function createMicroflowFlowId(schema: MicroflowSchema, prefix = "flow"): string {
  return uniqueSchemaId(schema, prefix);
}

function nextDuplicateCaption(caption: string | undefined, fallback: string): string {
  const base = caption?.trim() || fallback;
  return `${base} Copy`;
}

function nextParameterCopyName(schema: MicroflowSchema, sourceName: string | undefined): string {
  const names = new Set(schema.parameters.map(parameter => parameter.name.toLocaleLowerCase()));
  const base = `${sourceName?.trim() || "parameter"}_Copy`;
  if (!names.has(base.toLocaleLowerCase())) {
    return base;
  }
  let index = 2;
  while (names.has(`${base}${index}`.toLocaleLowerCase())) {
    index += 1;
  }
  return `${base}${index}`;
}

function collectVariableNames(schema: MicroflowSchema): Set<string> {
  const names = new Set(schema.parameters.map(parameter => parameter.name.toLocaleLowerCase()));
  for (const object of flattenObjectCollection(schema.objectCollection)) {
    if (object.kind === "actionActivity" && object.action.kind === "createVariable") {
      names.add(object.action.variableName.toLocaleLowerCase());
    }
    if (object.kind === "actionActivity" && (object.action.kind === "createObject" || object.action.kind === "retrieve") && object.action.outputVariableName) {
      names.add(object.action.outputVariableName.toLocaleLowerCase());
    }
    if (object.kind === "actionActivity" && object.action.kind === "createList") {
      const name = object.action.outputListVariableName || object.action.listVariableName;
      if (name) {
        names.add(name.toLocaleLowerCase());
      }
    }
    if (object.kind === "actionActivity" && (object.action.kind === "aggregateList" || object.action.kind === "listOperation")) {
      const name = object.action.outputVariableName;
      if (name) {
        names.add(name.toLocaleLowerCase());
      }
    }
    if (object.kind === "loopedActivity" && object.loopSource.kind === "iterableList" && object.loopSource.iteratorVariableName) {
      names.add(object.loopSource.iteratorVariableName.toLocaleLowerCase());
    }
  }
  return names;
}

function nextVariableCopyName(schema: MicroflowSchema, sourceName: string | undefined): string {
  const names = collectVariableNames(schema);
  const base = `${sourceName?.trim() || "variable"}_Copy`;
  if (!names.has(base.toLocaleLowerCase())) {
    return base;
  }
  let index = 2;
  while (names.has(`${base}${index}`.toLocaleLowerCase())) {
    index += 1;
  }
  return `${base}${index}`;
}

function appendObjectToLoop(collection: MicroflowObjectCollection, loopObjectId: string, object: MicroflowObject): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects.map(item => {
      if (item.kind === "loopedActivity" && item.id === loopObjectId) {
        return {
          ...item,
          objectCollection: {
            ...item.objectCollection,
            objects: [...item.objectCollection.objects, object]
          }
        };
      }
      if (item.kind === "loopedActivity") {
        return { ...item, objectCollection: appendObjectToLoop(item.objectCollection, loopObjectId, object) };
      }
      return item;
    })
  };
}

export function findObject(schema: MicroflowSchema, objectId: string): MicroflowObject | undefined {
  return flattenObjectCollection(schema.objectCollection).find(object => object.id === objectId);
}

export function addObject(schema: MicroflowSchema, object: MicroflowObject, parentLoopObjectId?: string): MicroflowSchema {
  const objectCollection = parentLoopObjectId
    ? appendObjectToLoop(schema.objectCollection, parentLoopObjectId, object)
    : { ...schema.objectCollection, objects: [...schema.objectCollection.objects, object] };
  return refreshDerivedState({ ...schema, objectCollection });
}

export function updateObject(schema: MicroflowSchema, objectId: string, mapper: (object: MicroflowObject) => MicroflowObject): MicroflowSchema {
  return refreshDerivedState({
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, objectId, mapper)
  });
}

export function deleteObject(schema: MicroflowSchema, objectId: string): MicroflowSchema {
  const target = findObject(schema, objectId);
  if (target?.kind === "startEvent") {
    return schema;
  }
  const removedObjectIds = collectObjectAndDescendantIds(schema.objectCollection, objectId);
  const removedObjects = flattenObjectCollection(schema.objectCollection).filter(object => removedObjectIds.has(object.id));
  const removedParameterIds = new Set(removedObjects
    .filter((object): object is Extract<MicroflowObject, { kind: "parameterObject" }> => object.kind === "parameterObject")
    .map(object => object.parameterId));
  const safeFlows = Array.isArray(schema.flows) ? schema.flows : [];
  const flows = safeFlows.filter(flow => !removedObjectIds.has(flow.originObjectId) && !removedObjectIds.has(flow.destinationObjectId));
  const objectCollection = removeFlowsFromCollection(
    removeObjectFromCollection(schema.objectCollection, objectId),
    removedObjectIds,
  );
  const selectedFlowStillExists = schema.editor.selection.flowId
    ? [...flows, ...collectFlowsRecursive({ ...schema, flows, objectCollection })].some(flow => flow.id === schema.editor.selection.flowId)
    : false;
  const remainingFlows = [...flows, ...collectFlowsRecursive({ ...schema, flows, objectCollection })];
  const selectedObjectIds = (schema.editor.selection.objectIds ?? []).filter(id => !removedObjectIds.has(id));
  const selectedFlowIds = (schema.editor.selection.flowIds ?? []).filter(id => remainingFlows.some(flow => flow.id === id));
  const selectionCount = selectedObjectIds.length + selectedFlowIds.length;
  return refreshDerivedState({
    ...schema,
    objectCollection,
    flows,
    parameters: schema.parameters.filter(parameter => !removedParameterIds.has(parameter.id)),
    editor: {
      ...schema.editor,
      selection: {
        objectId: schema.editor.selection.objectId && removedObjectIds.has(schema.editor.selection.objectId)
          ? undefined
          : schema.editor.selection.objectId,
        flowId: selectedFlowStillExists ? schema.editor.selection.flowId : undefined,
        collectionId: schema.editor.selection.objectId && removedObjectIds.has(schema.editor.selection.objectId)
          ? undefined
          : schema.editor.selection.collectionId,
        objectIds: selectedObjectIds,
        flowIds: selectedFlowIds,
        mode: selectionCount === 0 ? "none" : selectionCount === 1 ? "single" : "multi"
      },
      selectedObjectId: schema.editor.selectedObjectId && removedObjectIds.has(schema.editor.selectedObjectId)
        ? undefined
        : schema.editor.selectedObjectId,
      selectedFlowId: selectedFlowStillExists ? schema.editor.selectedFlowId : undefined,
      selectedCollectionId: schema.editor.selection.objectId && removedObjectIds.has(schema.editor.selection.objectId)
        ? undefined
        : schema.editor.selectedCollectionId
    }
  });
}

export function duplicateObject(schema: MicroflowSchema, objectId: string): MicroflowSchema {
  const located = findObjectWithCollection(schema, objectId);
  const object = located?.object;
  if (!object || object.kind === "startEvent") {
    return schema;
  }
  const copy = clone(object) as MicroflowObject;
  copy.id = uniqueSchemaId(schema, `${object.kind}-copy`);
  copy.stableId = copy.id;
  copy.caption = nextDuplicateCaption(object.caption, object.kind);
  copy.relativeMiddlePoint = { x: object.relativeMiddlePoint.x + 80, y: object.relativeMiddlePoint.y + 60 };

  let parameters = schema.parameters;
  if (copy.kind === "actionActivity") {
    copy.action = { ...copy.action, id: uniqueSchemaId(schema, `action-${copy.action.kind}-copy`) } as MicroflowAction;
    if (copy.action.kind === "createVariable" && object.kind === "actionActivity" && object.action.kind === "createVariable") {
      copy.action.variableName = nextVariableCopyName(schema, object.action.variableName);
    }
    if (copy.action.kind === "createObject" && object.kind === "actionActivity" && object.action.kind === "createObject" && object.action.outputVariableName) {
      copy.action.outputVariableName = nextVariableCopyName(schema, object.action.outputVariableName);
    }
    if (copy.action.kind === "retrieve" && object.kind === "actionActivity" && object.action.kind === "retrieve" && object.action.outputVariableName) {
      copy.action.outputVariableName = nextVariableCopyName(schema, object.action.outputVariableName);
    }
    if (copy.action.kind === "createList" && object.kind === "actionActivity" && object.action.kind === "createList") {
      const nextName = nextVariableCopyName(schema, object.action.outputListVariableName || object.action.listVariableName);
      copy.action.outputListVariableName = nextName;
      copy.action.listVariableName = nextName;
      copy.action.listVariableId = copy.action.id;
    }
    if (copy.action.kind === "aggregateList" && object.kind === "actionActivity" && object.action.kind === "aggregateList" && object.action.outputVariableName) {
      const nextName = nextVariableCopyName(schema, object.action.outputVariableName);
      copy.action.outputVariableName = nextName;
      copy.action.resultVariableName = nextName;
      copy.action.resultVariableId = copy.action.id;
    }
    if (copy.action.kind === "listOperation" && object.kind === "actionActivity" && object.action.kind === "listOperation" && object.action.outputVariableName) {
      const nextName = nextVariableCopyName(schema, object.action.outputVariableName);
      copy.action.outputVariableName = nextName;
      copy.action.outputListVariableName = nextName;
      copy.action.targetListVariableId = copy.action.id;
    }
  }
  if (copy.kind === "parameterObject") {
    const sourceParameterId = object.kind === "parameterObject" ? object.parameterId : undefined;
    const parameter = schema.parameters.find(item => item.id === sourceParameterId);
    const parameterId = uniqueSchemaId(schema, "param-copy");
    const parameterName = nextParameterCopyName(schema, parameter?.name ?? copy.parameterName ?? copy.caption);
    copy.parameterId = parameterId;
    copy.parameterName = parameterName;
    copy.caption = parameterName;
    parameters = [
      ...schema.parameters,
      {
        ...(parameter ?? {
          dataType: { kind: "string" as const },
          type: { kind: "primitive" as const, name: "String" },
          required: true
        }),
        id: parameterId,
        stableId: parameterId,
        name: parameterName
      }
    ];
  }
  if (copy.kind === "loopedActivity") {
    if (copy.loopSource.kind === "iterableList" && object.kind === "loopedActivity" && object.loopSource.kind === "iterableList") {
      copy.loopSource = {
        ...copy.loopSource,
        iteratorVariableName: nextVariableCopyName(schema, object.loopSource.iteratorVariableName)
      };
    }
    copy.objectCollection = {
      ...copy.objectCollection,
      id: uniqueSchemaId(schema, "loop-collection-copy"),
      objects: [],
      flows: []
    };
  }

  const next = addObject({ ...schema, parameters }, copy, located.parentLoopObjectId);
  return {
    ...next,
    editor: {
      ...next.editor,
      selection: {
        objectId: copy.id,
        flowId: undefined,
        collectionId: located.collectionId,
        objectIds: [copy.id],
        flowIds: [],
        mode: "single"
      },
      selectedObjectId: copy.id,
      selectedFlowId: undefined,
      selectedCollectionId: located.collectionId
    }
  };
}

export interface DuplicateObjectSelectionOptions {
  objectIds: string[];
  flowIds?: string[];
  offset?: MicroflowPoint;
}

function remapObjectIdsForDuplicate(
  schema: MicroflowSchema,
  object: MicroflowObject,
  idMap: Map<string, string>,
  offset: MicroflowPoint,
): MicroflowObject {
  const copy = clone(object) as MicroflowObject;
  const nextId = uniqueSchemaId(schema, `${object.kind}-copy`);
  idMap.set(object.id, nextId);
  copy.id = nextId;
  copy.stableId = nextId;
  copy.caption = nextDuplicateCaption(object.caption, object.kind);
  copy.relativeMiddlePoint = {
    x: object.relativeMiddlePoint.x + offset.x,
    y: object.relativeMiddlePoint.y + offset.y,
  };
  if (copy.kind === "actionActivity") {
    copy.action = { ...copy.action, id: uniqueSchemaId(schema, `action-${copy.action.kind}-copy`) } as MicroflowAction;
  }
  if (copy.kind === "loopedActivity") {
    copy.objectCollection = {
      ...copy.objectCollection,
      id: uniqueSchemaId(schema, "loop-collection-copy"),
      objects: copy.objectCollection.objects.map(child => remapObjectIdsForDuplicate(schema, child, idMap, { x: 0, y: 0 })),
      flows: (copy.objectCollection.flows ?? []).map(flow => ({
        ...flow,
        id: uniqueSchemaId(schema, `${flow.kind}-copy`),
        stableId: uniqueSchemaId(schema, `${flow.kind}-stable-copy`),
        originObjectId: idMap.get(flow.originObjectId) ?? flow.originObjectId,
        destinationObjectId: idMap.get(flow.destinationObjectId) ?? flow.destinationObjectId,
      } as MicroflowFlow)),
    };
  }
  return copy;
}

export function duplicateObjectSelection(schema: MicroflowSchema, options: DuplicateObjectSelectionOptions): MicroflowSchema {
  const selectedObjectIds = [...new Set(options.objectIds)].filter(id => Boolean(findObjectWithCollection(schema, id)));
  if (selectedObjectIds.length === 0) {
    return schema;
  }

  const offset = options.offset ?? { x: 80, y: 60 };
  const idMap = new Map<string, string>();
  const copiedObjects = selectedObjectIds
    .map(id => findObjectWithCollection(schema, id))
    .filter((item): item is NonNullable<ReturnType<typeof findObjectWithCollection>> => Boolean(item))
    .map(item => ({
      located: item,
      copy: remapObjectIdsForDuplicate(schema, item.object, idMap, offset),
    }));

  let next = schema;
  for (const item of copiedObjects) {
    next = addObject(next, item.copy, item.located.parentLoopObjectId);
  }

  const selectedObjectIdSet = new Set(selectedObjectIds);
  const explicitFlowIds = new Set(options.flowIds ?? []);
  const candidateFlows = collectFlowsRecursive(schema).filter(flow =>
    (selectedObjectIdSet.has(flow.originObjectId) && selectedObjectIdSet.has(flow.destinationObjectId))
    || explicitFlowIds.has(flow.id),
  );
  for (const flow of candidateFlows) {
    const originObjectId = idMap.get(flow.originObjectId);
    const destinationObjectId = idMap.get(flow.destinationObjectId);
    if (!originObjectId || !destinationObjectId) {
      continue;
    }
    next = addFlow(next, {
      ...clone(flow),
      id: uniqueSchemaId(next, `${flow.kind}-copy`),
      stableId: uniqueSchemaId(next, `${flow.kind}-stable-copy`),
      originObjectId,
      destinationObjectId,
    } as MicroflowFlow);
  }

  const copiedObjectIds = copiedObjects.map(item => item.copy.id);
  const first = copiedObjects[0];
  return refreshDerivedState({
    ...next,
    editor: {
      ...next.editor,
      selection: {
        objectId: copiedObjectIds[0],
        flowId: undefined,
        collectionId: first?.located.collectionId,
        objectIds: copiedObjectIds,
        flowIds: [],
        mode: copiedObjectIds.length > 1 ? "multi" : "single",
      },
      selectedObjectId: copiedObjectIds[0],
      selectedFlowId: undefined,
      selectedCollectionId: first?.located.collectionId,
    },
  });
}

export function moveObject(schema: MicroflowSchema, objectId: string, position: MicroflowPoint): MicroflowSchema {
  const target = findObject(schema, objectId);
  if (target?.kind === "startEvent") {
    return schema;
  }
  return updateObject(schema, objectId, object => ({ ...object, relativeMiddlePoint: position }));
}

export function resizeObject(schema: MicroflowSchema, objectId: string, size: MicroflowSize): MicroflowSchema {
  return updateObject(schema, objectId, object => ({ ...object, size }));
}

export function addFlow(schema: MicroflowSchema, flow: MicroflowFlow): MicroflowSchema {
  const caseKey = (item: MicroflowFlow) => item.kind === "sequence"
    ? item.caseValues.map(caseValueIdentity).join("|")
    : "";
  const duplicate = collectFlowsRecursive(schema).some(item =>
    item.originObjectId === flow.originObjectId &&
    item.destinationObjectId === flow.destinationObjectId &&
    (item.originConnectionIndex ?? 0) === (flow.originConnectionIndex ?? 0) &&
    (item.destinationConnectionIndex ?? 0) === (flow.destinationConnectionIndex ?? 0) &&
    item.kind === flow.kind &&
    (item.kind === "annotation" || flow.kind === "annotation" || item.editor.edgeKind === flow.editor.edgeKind) &&
    caseKey(item) === caseKey(flow)
  );
  if (duplicate) {
    return schema;
  }
  const sourceLocation = findObjectWithCollection(schema, flow.originObjectId);
  const targetLocation = findObjectWithCollection(schema, flow.destinationObjectId);
  if (!sourceLocation || !targetLocation) {
    return schema;
  }
  const isAnnotationFlow = flow.kind === "annotation";
  const isSameCollection = sourceLocation.collectionId === targetLocation.collectionId;
  const isLoopBodyEntry = flow.kind === "sequence"
    && sourceLocation.object.kind === "loopedActivity"
    && targetLocation.parentLoopObjectId === sourceLocation.object.id
    && flow.editor.edgeKind === "loopBody"
    && (flow.originConnectionIndex ?? 0) === 2;
  if (!isAnnotationFlow && !isSameCollection && !isLoopBodyEntry) {
    return schema;
  }
  const collectionId = isSameCollection
    ? sourceLocation.collectionId
    : isLoopBodyEntry
      ? targetLocation.collectionId
      : schema.objectCollection.id;
  return refreshDerivedState(addFlowToCollection(schema, collectionId, flow));
}

export function updateFlow(schema: MicroflowSchema, flowId: string, mapper: (flow: MicroflowFlow) => MicroflowFlow): MicroflowSchema {
  const location = findFlowWithCollection(schema, flowId);
  if (location && location.collectionId !== schema.objectCollection.id) {
    const withoutFlow = removeFlowFromCollection(schema, location.collectionId, flowId);
    return refreshDerivedState(addFlowToCollection(withoutFlow, location.collectionId, mapper(location.flow)));
  }
  return refreshDerivedState({
    ...schema,
    flows: (Array.isArray(schema.flows) ? schema.flows : []).map(flow => flow.id === flowId ? mapper(flow) : flow)
  });
}

export function deleteFlow(schema: MicroflowSchema, flowId: string): MicroflowSchema {
  const location = findFlowWithCollection(schema, flowId);
  const clearSelection = (target: MicroflowSchema): MicroflowSchema => ({
    ...target,
    editor: {
      ...target.editor,
      selection: {
        ...target.editor.selection,
        flowId: target.editor.selection.flowId === flowId ? undefined : target.editor.selection.flowId,
        collectionId: target.editor.selection.flowId === flowId ? undefined : target.editor.selection.collectionId,
        flowIds: (target.editor.selection.flowIds ?? []).filter(id => id !== flowId),
        mode: ((target.editor.selection.objectIds ?? []).length + (target.editor.selection.flowIds ?? []).filter(id => id !== flowId).length) > 1
          ? "multi"
          : ((target.editor.selection.objectIds ?? []).length + (target.editor.selection.flowIds ?? []).filter(id => id !== flowId).length) === 1
            ? "single"
            : "none"
      },
      selectedFlowId: target.editor.selectedFlowId === flowId ? undefined : target.editor.selectedFlowId,
      selectedCollectionId: target.editor.selectedFlowId === flowId ? undefined : target.editor.selectedCollectionId
    }
  });
  if (location && location.collectionId !== schema.objectCollection.id) {
    return refreshDerivedState(clearSelection(removeFlowFromCollection(schema, location.collectionId, flowId)));
  }
  return refreshDerivedState(clearSelection({
    ...schema,
    flows: (Array.isArray(schema.flows) ? schema.flows : []).filter(flow => flow.id !== flowId)
  }));
}

export function splitFlowWithObject(schema: MicroflowSchema, flowId: string, object: MicroflowObject): MicroflowSchema {
  const located = findFlowWithCollection(schema, flowId);
  const flow = located?.flow;
  if (!flow || flow.kind !== "sequence") {
    return addObject(schema, object);
  }
  const first = createSequenceFlow({
    originObjectId: flow.originObjectId,
    destinationObjectId: object.id,
    edgeKind: flow.editor.edgeKind,
    caseValues: flow.caseValues,
    isErrorHandler: flow.isErrorHandler,
    label: flow.editor.label
  });
  const second = createSequenceFlow({
    originObjectId: object.id,
    destinationObjectId: flow.destinationObjectId,
    edgeKind: "sequence",
    label: flow.isErrorHandler ? "error scope" : undefined
  });
  const withObject = addObject(schema, object, located.parentLoopObjectId);
  const withoutOriginal = removeFlowFromCollection(withObject, located.collectionId, flowId);
  return refreshDerivedState(addFlowToCollection(addFlowToCollection(withoutOriginal, located.collectionId, first), located.collectionId, second));
}

export function createObjectFromRegistry(entry: MicroflowNodeRegistryEntry, position: MicroflowPoint, id = createStableId(getMicroflowNodeRegistryKey(entry).replace(":", "-"))): MicroflowObject {
  const config = (entry.createDefaultConfig?.({ position }) ?? entry.defaultConfig) as Record<string, unknown>;
  const base = {
    id,
    stableId: id,
    caption: entry.title,
    documentation: entry.documentation.summary,
    relativeMiddlePoint: position,
    size: {
      width: entry.render.width ?? 176,
      height: entry.render.height ?? 76
    },
    editor: {
      colorToken: entry.colorToken,
      iconKey: entry.iconKey
    }
  };
  if (entry.type === "startEvent") {
    return { ...base, kind: "startEvent", officialType: "Microflows$StartEvent", trigger: { type: "manual" } };
  }
  if (entry.type === "endEvent") {
    return { ...base, kind: "endEvent", officialType: "Microflows$EndEvent", endBehavior: { type: "normalReturn" } };
  }
  if (entry.type === "errorEvent") {
    return {
      ...base,
      kind: "errorEvent",
      officialType: "Microflows$ErrorEvent",
      error: { sourceVariableName: "$latestError", messageExpression: expression("$latestError") }
    };
  }
  if (entry.type === "breakEvent") {
    return { ...base, kind: "breakEvent", officialType: "Microflows$BreakEvent" };
  }
  if (entry.type === "continueEvent") {
    return { ...base, kind: "continueEvent", officialType: "Microflows$ContinueEvent" };
  }
  if (entry.type === "decision") {
    return {
      ...base,
      kind: "exclusiveSplit",
      officialType: "Microflows$ExclusiveSplit",
      splitCondition: { kind: "expression", expression: expression("", { kind: "boolean" }), resultType: "boolean" },
      errorHandlingType: "rollback"
    };
  }
  if (entry.type === "objectTypeDecision") {
    return {
      ...base,
      kind: "inheritanceSplit",
      officialType: "Microflows$InheritanceSplit",
      inputObjectVariableName: "",
      generalizedEntityQualifiedName: "",
      allowedSpecializations: [],
      entity: { generalizedEntityQualifiedName: "", allowedSpecializations: [] },
      errorHandlingType: "rollback"
    };
  }
  if (entry.type === "merge") {
    return {
      ...base,
      kind: "exclusiveMerge",
      officialType: "Microflows$ExclusiveMerge",
      mergeBehavior: { strategy: "firstArrived" }
    };
  }
  if (entry.type === "parallelGateway") {
    return {
      ...base,
      kind: "parallelGateway",
      officialType: "Microflows$ParallelGateway",
      gatewayMode: "auto",
      branches: [],
      joinPolicy: "waitAll"
    };
  }
  if (entry.type === "inclusiveGateway") {
    return {
      ...base,
      kind: "inclusiveGateway",
      officialType: "Microflows$InclusiveGateway",
      branches: [],
      defaultBranch: null,
      mergePolicy: "waitAll"
    };
  }
  if (entry.type === "loop") {
    return {
      ...base,
      kind: "loopedActivity",
      officialType: "Microflows$LoopedActivity",
      documentation: entry.documentation.summary,
      errorHandlingType: "rollback",
      loopSource: {
        kind: "iterableList",
        officialType: "Microflows$IterableList",
        listVariableName: String(config.iterableVariableName ?? ""),
        iteratorVariableName: String(config.itemVariableName ?? ""),
        currentIndexVariableName: "$currentIndex"
      },
      objectCollection: {
        id: `${id}-collection`,
        officialType: "Microflows$MicroflowObjectCollection",
        objects: [],
        flows: []
      }
    };
  }
  if (entry.type === "parameter") {
    const parameter = (config.parameter as { id?: string } | undefined) ?? {};
    return {
      ...base,
      kind: "parameterObject",
      officialType: "Microflows$MicroflowParameterObject",
      parameterId: parameter.id ?? id
    };
  }
  if (entry.type === "annotation") {
    return {
      ...base,
      kind: "annotation",
      officialType: "Microflows$Annotation",
      text: String(config.text ?? "")
    };
  }
  const action = defaultActionFromRegistry(entry, id, config);
  return {
    ...base,
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: entry.title,
    autoGenerateCaption: false,
    backgroundColor: "default",
    disabled: false,
    action
  };
}

function expression(raw: string, inferredType?: MicroflowDataType): MicroflowExpression {
  return {
    raw,
    inferredType,
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: []
  };
}

function defaultActionFromRegistry(entry: MicroflowNodeRegistryEntry, objectId: string, config: Record<string, unknown>): MicroflowAction {
  const registryAction = entry.activityType ? microflowActionRegistryByActivityType.get(entry.activityType) : undefined;
  if (registryAction) {
    return registryAction.createAction({
      id: `action-${objectId}`,
      config,
      caption: entry.defaultCaption
    });
  }
  const kind = (config.actionKind as MicroflowAction["kind"] | undefined) ?? "logMessage";
  const base = {
    id: `action-${objectId}`,
    officialType: "Microflows$LogMessageAction",
    kind,
    errorHandlingType: "rollback" as const,
    documentation: entry.documentation.summary,
    editor: {
      category: "logging" as const,
      iconKey: entry.iconKey,
      availability: "supported" as const
    }
  };
  if (kind === "retrieve") {
    return {
      ...base,
      kind: "retrieve",
      officialType: "Microflows$RetrieveAction",
      outputVariableName: String(config.resultVariableName ?? config.objectVariableName ?? ""),
      retrieveSource: String(config.retrieveMode ?? "database") === "association"
        ? {
            kind: "association",
            officialType: "Microflows$AssociationRetrieveSource",
            associationQualifiedName: typeof config.association === "string" ? config.association : null,
            startVariableName: String(config.objectVariableName ?? "")
          }
        : {
            kind: "database",
            officialType: "Microflows$DatabaseRetrieveSource",
            entityQualifiedName: typeof config.entity === "string" ? config.entity : null,
            xPathConstraint: null,
            sortItemList: { items: [] },
            range: { kind: "first", officialType: "Microflows$ConstantRange", value: "first" }
          }
    } as MicroflowAction;
  }
  if (kind === "createObject") {
    return {
      ...base,
      kind: "createObject",
      officialType: "Microflows$CreateObjectAction",
      entityQualifiedName: String(config.entity ?? ""),
      outputVariableName: String(config.resultVariableName ?? config.objectVariableName ?? ""),
      memberChanges: [],
      commit: { enabled: false, withEvents: true, refreshInClient: false }
    } as MicroflowAction;
  }
  if (kind === "changeMembers") {
    return {
      ...base,
      kind: "changeMembers",
      officialType: "Microflows$ChangeMembersAction",
      changeVariableName: String(config.objectVariableName ?? ""),
      memberChanges: [],
      commit: { enabled: false, withEvents: true, refreshInClient: false },
      validateObject: true
    } as MicroflowAction;
  }
  if (kind === "commit") {
    return {
      ...base,
      kind: "commit",
      officialType: "Microflows$CommitAction",
      objectOrListVariableName: String(config.objectVariableName ?? config.listVariableName ?? ""),
      withEvents: Boolean(config.withEvents ?? true),
      refreshInClient: Boolean(config.refreshClient ?? false)
    } as MicroflowAction;
  }
  if (kind === "delete") {
    return {
      ...base,
      kind: "delete",
      officialType: "Microflows$DeleteAction",
      objectOrListVariableName: String(config.objectVariableName ?? config.listVariableName ?? ""),
      withEvents: Boolean(config.withEvents ?? true),
      deleteBehavior: "deleteOnly"
    } as MicroflowAction;
  }
  if (kind === "rollback") {
    return {
      ...base,
      kind: "rollback",
      officialType: "Microflows$RollbackAction",
      objectOrListVariableName: String(config.objectVariableName ?? config.listVariableName ?? ""),
      refreshInClient: Boolean(config.refreshClient ?? false)
    } as MicroflowAction;
  }
  if (kind === "callMicroflow") {
    return {
      ...base,
      kind: "callMicroflow",
      officialType: "Microflows$MicroflowCallAction",
      targetMicroflowId: String(config.targetMicroflowId ?? ""),
      targetMicroflowName: String(config.targetMicroflowName ?? ""),
      targetMicroflowDisplayName: String(config.targetMicroflowDisplayName ?? config.targetMicroflowName ?? ""),
      targetMicroflowQualifiedName: String(config.targetMicroflowQualifiedName ?? ""),
      targetModuleId: String(config.targetModuleId ?? ""),
      parameterMappings: [],
      returnValue: { storeResult: false },
      callMode: "sync"
    } as MicroflowAction;
  }
  if (kind === "restCall") {
    return {
      ...base,
      kind: "restCall",
      officialType: "Microflows$RestCallAction",
      request: {
        method: String(config.method ?? "GET") as "GET" | "POST" | "PUT" | "PATCH" | "DELETE",
        urlExpression: expression(String(config.url ?? "https://api.example.com"), { kind: "string" }),
        headers: [],
        queryParameters: [],
        body: { kind: "none" }
      },
      response: { handling: { kind: "ignore" } },
      timeoutSeconds: 30
    } as MicroflowAction;
  }
  if (kind === "logMessage") {
    return {
      ...base,
      kind: "logMessage",
      officialType: "Microflows$LogMessageAction",
      level: "info",
      logNodeName: String(config.logNodeName ?? "Microflow"),
      template: { text: String(config.message ?? "Log message"), arguments: [] },
      includeContextVariables: false,
      includeTraceId: true
    } as MicroflowAction;
  }
  if (kind === "createVariable") {
    return {
      ...base,
      kind: "createVariable",
      officialType: "Microflows$CreateVariableAction",
      variableName: String(config.variableName ?? "newVariable"),
      dataType: { kind: "string" },
      initialValue: undefined,
      readonly: false
    } as MicroflowAction;
  }
  if (kind === "changeVariable") {
    return {
      ...base,
      kind: "changeVariable",
      officialType: "Microflows$ChangeVariableAction",
      targetVariableName: String(config.variableName ?? ""),
      newValueExpression: expression("")
    } as MicroflowAction;
  }
  return {
    ...base,
    kind,
    officialType: "Microflows$GenericAction"
  } as MicroflowAction;
}

export function createSequenceFlow(input: {
  id?: string;
  originObjectId: string;
  destinationObjectId: string;
  originConnectionIndex?: number;
  destinationConnectionIndex?: number;
  caseValues?: MicroflowCaseValue[];
  isErrorHandler?: boolean;
  edgeKind?: MicroflowSequenceFlow["editor"]["edgeKind"];
  label?: string;
  description?: string;
}): MicroflowSequenceFlow {
  const id = input.id ?? createStableId("flow");
  return {
    id,
    stableId: id,
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    originObjectId: input.originObjectId,
    destinationObjectId: input.destinationObjectId,
    originConnectionIndex: input.originConnectionIndex ?? 0,
    destinationConnectionIndex: input.destinationConnectionIndex ?? 0,
    caseValues: input.caseValues ?? [],
    isErrorHandler: Boolean(input.isErrorHandler),
    line: defaultMicroflowLine(),
    editor: {
      edgeKind: input.isErrorHandler ? "errorHandler" : input.edgeKind ?? "sequence",
      label: input.label,
      description: input.description
    }
  };
}

export function createAnnotationFlow(input: {
  id?: string;
  originObjectId: string;
  destinationObjectId: string;
  label?: string;
  description?: string;
}): MicroflowAnnotationFlow {
  const id = input.id ?? createStableId("annotation-flow");
  return {
    id,
    stableId: id,
    kind: "annotation",
    officialType: "Microflows$AnnotationFlow",
    originObjectId: input.originObjectId,
    destinationObjectId: input.destinationObjectId,
    originConnectionIndex: 0,
    destinationConnectionIndex: 0,
    line: {
      ...defaultMicroflowLine(),
      style: { strokeType: "dashed", strokeWidth: 2, arrow: "none" },
    },
    editor: {
      label: input.label,
      description: input.description,
      showInExport: true
    }
  };
}

export function applyEditorGraphPatchToAuthoring(schema: MicroflowSchema, patch: MicroflowEditorGraphPatch): MicroflowSchema {
  let next = schema;
  for (const moved of patch.movedNodes ?? []) {
    next = moveObject(next, moved.objectId, moved.position);
  }
  for (const resized of patch.resizedNodes ?? []) {
    next = resizeObject(next, resized.objectId, resized.size);
  }
  for (const update of patch.updatedFlows ?? []) {
    next = updateFlow(next, update.flowId, flow => flow.kind === "annotation"
      ? { ...flow, line: canonicalizeFlowLine(update.line, flow.line), editor: { ...flow.editor, label: update.label ?? flow.editor.label } }
      : { ...flow, line: canonicalizeFlowLine(update.line, flow.line), editor: { ...flow.editor, label: update.label ?? flow.editor.label } });
  }
  if (patch.addObject) {
    next = addObject(next, patch.addObject.object, patch.addObject.parentLoopObjectId);
  }
  if (patch.deleteObjectId) {
    next = deleteObject(next, patch.deleteObjectId);
  }
  if (patch.addFlow) {
    next = addFlow(next, patch.addFlow);
  }
  if (patch.deleteFlowId) {
    next = deleteFlow(next, patch.deleteFlowId);
  }
  const hasSelectedObject = Object.prototype.hasOwnProperty.call(patch, "selectedObjectId");
  const hasSelectedFlow = Object.prototype.hasOwnProperty.call(patch, "selectedFlowId");
  const hasSelectedCollection = Object.prototype.hasOwnProperty.call(patch, "selectedCollectionId");
  const hasSelectedObjects = Object.prototype.hasOwnProperty.call(patch, "selectedObjectIds");
  const hasSelectedFlows = Object.prototype.hasOwnProperty.call(patch, "selectedFlowIds");
  const hasSelectionMode = Object.prototype.hasOwnProperty.call(patch, "selectionMode");
  if (patch.viewport || hasSelectedObject || hasSelectedFlow || hasSelectedCollection || hasSelectedObjects || hasSelectedFlows || hasSelectionMode) {
    const objectIds = hasSelectedObjects
      ? [...(patch.selectedObjectIds ?? [])]
      : hasSelectedObject
        ? (patch.selectedObjectId ? [patch.selectedObjectId] : [])
        : [...(next.editor.selection.objectIds ?? [])];
    const flowIds = hasSelectedFlows
      ? [...(patch.selectedFlowIds ?? [])]
      : hasSelectedFlow
        ? (patch.selectedFlowId ? [patch.selectedFlowId] : [])
        : [...(next.editor.selection.flowIds ?? [])];
    const mode = hasSelectionMode
      ? patch.selectionMode
      : objectIds.length + flowIds.length > 1
        ? "multi"
        : objectIds.length + flowIds.length === 1
          ? "single"
          : "none";
    next = {
      ...next,
      editor: {
        ...next.editor,
        viewport: patch.viewport ?? next.editor.viewport,
        selectedObjectId: hasSelectedObject ? patch.selectedObjectId : next.editor.selectedObjectId,
        selectedFlowId: hasSelectedFlow ? patch.selectedFlowId : next.editor.selectedFlowId,
        selectedCollectionId: hasSelectedCollection ? patch.selectedCollectionId : next.editor.selectedCollectionId,
        selection: {
          objectId: hasSelectedObject ? patch.selectedObjectId : next.editor.selection.objectId,
          flowId: hasSelectedFlow ? patch.selectedFlowId : next.editor.selection.flowId,
          collectionId: hasSelectedCollection ? patch.selectedCollectionId : next.editor.selection.collectionId,
          objectIds,
          flowIds,
          mode
        }
      }
    };
  }
  return refreshDerivedState(next);
}

export function addParameter(schema: MicroflowSchema, parameter: MicroflowParameter, position: MicroflowPoint): MicroflowSchema {
  const object: MicroflowObject = {
    id: `parameter-object-${parameter.id}`,
    stableId: `parameter-object-${parameter.stableId ?? parameter.id}`,
    kind: "parameterObject",
    officialType: "Microflows$MicroflowParameterObject",
    caption: parameter.name,
    documentation: parameter.documentation,
    relativeMiddlePoint: position,
    size: { width: 172, height: 70 },
    editor: { iconKey: "parameter" },
    parameterId: parameter.id
  };
  return refreshDerivedState({
    ...schema,
    parameters: [...schema.parameters, parameter],
    objectCollection: {
      ...schema.objectCollection,
      objects: [...schema.objectCollection.objects, object]
    }
  });
}

export function renameParameter(schema: MicroflowSchema, parameterId: string, nextName: string): MicroflowSchema {
  return refreshDerivedState({
    ...schema,
    parameters: schema.parameters.map(parameter => parameter.id === parameterId ? { ...parameter, name: nextName } : parameter),
    objectCollection: mapObjectCollection(schema.objectCollection, `parameter-object-${parameterId}`, object => ({ ...object, caption: nextName }))
  });
}

export function deleteParameter(schema: MicroflowSchema, parameterId: string): MicroflowSchema {
  return refreshDerivedState({
    ...schema,
    parameters: schema.parameters.filter(parameter => parameter.id !== parameterId),
    objectCollection: removeObjectFromCollection(schema.objectCollection, `parameter-object-${parameterId}`)
  });
}

export function refreshDerivedState(
  schema: MicroflowSchema,
  metadata: MicroflowMetadataCatalog = EMPTY_MICROFLOW_METADATA_CATALOG,
): MicroflowSchema {
  if ((schema as unknown as { workflow?: unknown }).workflow) {
    const designSchema = normalizeDesignSchemaVariables(schema as unknown as MicroflowDesignSchema);
    return {
      ...(designSchema as unknown as MicroflowSchema),
      validation: schema.validation,
      editor: {
        ...schema.editor,
        selection: schema.editor.selection
      }
    };
  }
  return {
    ...schema,
    variables: buildVariableIndex(schema, metadata),
    validation: schema.validation,
    editor: {
      ...schema.editor,
      selection: schema.editor.selection
    }
  };
}

function rebuildVariableIndex(schema: MicroflowSchema) {
  const graph = toEditorGraph(schema);
  const index = emptyVariableIndex();
  for (const parameter of schema.parameters) {
    index.parameters[parameter.name] = {
      name: parameter.name,
      dataType: parameter.dataType ?? { kind: "unknown", reason: "parameter type missing" },
      source: { kind: "parameter", parameterId: parameter.id },
      scope: { collectionId: schema.objectCollection.id },
      readonly: true
    };
  }
  for (const node of graph.nodes) {
    const object = findObject(schema, node.objectId);
    if (object?.kind === "loopedActivity" && object.loopSource.kind === "iterableList") {
      index.loopVariables[object.loopSource.iteratorVariableName] = {
        name: object.loopSource.iteratorVariableName,
        dataType: { kind: "unknown", reason: object.loopSource.listVariableName },
        source: { kind: "loopIterator", loopObjectId: object.id },
        scope: { collectionId: object.objectCollection.id, loopObjectId: object.id },
        readonly: true
      };
      index.systemVariables.$currentIndex = {
        name: "$currentIndex",
        dataType: { kind: "integer" },
        source: { kind: "system", name: "$currentIndex" },
        scope: { collectionId: object.objectCollection.id, loopObjectId: object.id },
        readonly: true
      };
    }
    if (object?.kind === "actionActivity") {
      const action = object.action;
      if (action.kind === "retrieve") {
        index.objectOutputs[action.outputVariableName] = {
          name: action.outputVariableName,
          dataType: action.retrieveSource.kind === "database" && action.retrieveSource.entityQualifiedName
            ? { kind: "object", entityQualifiedName: action.retrieveSource.entityQualifiedName }
            : { kind: "unknown", reason: "retrieve output" },
          source: { kind: "actionOutput", objectId: object.id, actionId: action.id },
          scope: { collectionId: node.collectionId, startObjectId: object.id },
          readonly: false
        };
      }
    }
  }
  for (const flow of collectFlowsRecursive(schema).filter((item): item is MicroflowSequenceFlow => item.kind === "sequence" && item.isErrorHandler)) {
    for (const name of ["$latestError", "$latestHttpResponse", "$latestSoapFault"] as const) {
      index.errorVariables[name] = {
        name,
        dataType: { kind: "object", entityQualifiedName: name === "$latestError" ? "System.Error" : name === "$latestHttpResponse" ? "System.HttpResponse" : "System.SoapFault" },
        source: { kind: "errorContext", flowId: flow.id },
        scope: { collectionId: schema.objectCollection.id, errorHandlerFlowId: flow.id, startObjectId: flow.destinationObjectId },
        readonly: true
      };
    }
  }
  return index;
}
