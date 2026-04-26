import { applyEditorGraphPatchToAuthoring, flattenObjectCollection } from "../adapters";
import { findObjectWithCollection, getObjectCollectionById } from "../schema/utils/object-utils";
import type {
  MicroflowEditorGraphPatch,
  MicroflowParameter,
  MicroflowPoint,
  MicroflowSchema,
} from "../schema";
import {
  canDragRegistryItem,
  defaultMicroflowNodePanelRegistry,
  getDisabledDragReason,
  microflowNodeRegistryByKey,
  type MicroflowNodeDragPayload,
} from "./registry";
import { createActionActivityFromActionRegistry, createObjectFromNodeRegistry } from "./factories";

export interface AddMicroflowObjectFromDragPayloadInput {
  schema: MicroflowSchema;
  payload: MicroflowNodeDragPayload;
  position: MicroflowPoint;
  parentLoopObjectId?: string;
}

export interface AddMicroflowObjectFromDragPayloadResult {
  schema: MicroflowSchema;
  objectId?: string;
  warnings: string[];
  blockedReason?: string;
}

function nextParameterName(schema: MicroflowSchema): string {
  const names = new Set(schema.parameters.map(parameter => parameter.name));
  if (!names.has("parameter")) {
    return "parameter";
  }
  let index = 1;
  while (names.has(`parameter${index}`)) {
    index += 1;
  }
  return `parameter${index}`;
}

function availabilityWarning(payload: MicroflowNodeDragPayload): string | undefined {
  if (payload.availability === "beta") {
    return `${payload.title} is beta.`;
  }
  if (payload.availability === "deprecated") {
    return `${payload.title} is deprecated.`;
  }
  return undefined;
}

export function addObjectToRootCollection(schema: MicroflowSchema, patch: MicroflowEditorGraphPatch): MicroflowSchema {
  return applyEditorGraphPatchToAuthoring(schema, patch);
}

export function addObjectToCollectionById(
  schema: MicroflowSchema,
  patch: MicroflowEditorGraphPatch,
  collectionId?: string,
): MicroflowSchema {
  if (!collectionId || collectionId === schema.objectCollection.id || !patch.addObject) {
    return applyEditorGraphPatchToAuthoring(schema, patch);
  }
  const parentLoopObjectId = parentLoopObjectIdForCollection(schema, collectionId);
  return applyEditorGraphPatchToAuthoring(schema, {
    ...patch,
    addObject: { ...patch.addObject, parentLoopObjectId },
  });
}

export function getDropTargetCollectionId(schema: MicroflowSchema, _position: MicroflowPoint, parentLoopObjectId?: string): string {
  if (!parentLoopObjectId) {
    return schema.objectCollection.id;
  }
  const loop = findObjectWithCollection(schema, parentLoopObjectId)?.object;
  return loop?.kind === "loopedActivity" ? loop.objectCollection.id : schema.objectCollection.id;
}

export function addObjectToActiveLoopCollection(schema: MicroflowSchema, patch: MicroflowEditorGraphPatch): MicroflowSchema {
  return applyEditorGraphPatchToAuthoring(schema, patch);
}

export function selectCreatedObject(objectId: string): MicroflowEditorGraphPatch {
  return { selectedObjectId: objectId, selectedFlowId: undefined };
}

export function addMicroflowObjectFromDragPayload(
  input: AddMicroflowObjectFromDragPayloadInput,
): AddMicroflowObjectFromDragPayloadResult {
  const { schema, payload, position, parentLoopObjectId } = input;
  const collectionId = getDropTargetCollectionId(schema, position, parentLoopObjectId);
  const dropCheck = validateDropAllowedInCollection(schema, payload, collectionId);
  if (!dropCheck.allowed) {
    return { schema, warnings: [], blockedReason: dropCheck.message };
  }
  const registryItem = microflowNodeRegistryByKey.get(payload.registryKey)
    ?? defaultMicroflowNodePanelRegistry.find(item => item.actionKind === payload.actionKind);
  if (!registryItem) {
    return { schema, warnings: [], blockedReason: "Unknown microflow node type." };
  }
  if (!canDragRegistryItem(registryItem)) {
    return { schema, warnings: [], blockedReason: getDisabledDragReason(registryItem) };
  }
  if (payload.objectKind === "startEvent" && flattenObjectCollection(schema.objectCollection).some(object => object.kind === "startEvent")) {
    return { schema, warnings: [], blockedReason: "A microflow can only have one Start Event." };
  }
  if ((payload.objectKind === "breakEvent" || payload.objectKind === "continueEvent") && !parentLoopObjectId) {
    return { schema, warnings: [], blockedReason: "Break / Continue can only be placed inside Loop." };
  }

  const warnings = [availabilityWarning(payload)].filter((warning): warning is string => Boolean(warning));
  if (payload.objectKind === "parameterObject") {
    const parameterName = nextParameterName(schema);
    const parameterId = `param-${Date.now()}`;
    const parameter: MicroflowParameter = {
      id: parameterId,
      stableId: parameterId,
      name: parameterName,
      dataType: { kind: "string" },
      type: { kind: "primitive", name: "String" },
      required: true,
      documentation: registryItem.documentation.summary,
    };
    const { object } = createObjectFromNodeRegistry({
      registryKey: payload.registryKey,
      position,
      id: `parameter-object-${parameterId}`,
      overrides: {
        caption: parameterName,
        parameterId,
        parameterName,
      },
    });
    const nextSchema = applyEditorGraphPatchToAuthoring(
      { ...schema, parameters: [...schema.parameters, parameter] },
      { addObject: { object, parentLoopObjectId }, ...selectCreatedObject(object.id) },
    );
    return { schema: nextSchema, objectId: object.id, warnings };
  }

  const object = payload.registryKind === "action" && payload.actionKind
    ? createActionActivityFromActionRegistry({ actionRegistryKey: payload.actionKind, position })
    : createObjectFromNodeRegistry({ registryKey: payload.registryKey, position }).object;
  const nextSchema = applyEditorGraphPatchToAuthoring(schema, {
    addObject: { object, parentLoopObjectId },
    ...selectCreatedObject(object.id),
  });
  return { schema: nextSchema, objectId: object.id, warnings };
}

export interface MicroflowDropValidationResult {
  allowed: boolean;
  message?: string;
}

export function validateDropAllowedInCollection(
  schema: MicroflowSchema,
  payload: MicroflowNodeDragPayload,
  collectionId: string,
): MicroflowDropValidationResult {
  const isRoot = collectionId === schema.objectCollection.id;
  if (!getObjectCollectionById(schema, collectionId)) {
    return { allowed: false, message: "Drop target collection does not exist." };
  }
  if (!isRoot && (payload.objectKind === "startEvent" || payload.objectKind === "endEvent")) {
    return { allowed: false, message: "Start / End events cannot be placed inside Loop." };
  }
  if (isRoot && (payload.objectKind === "breakEvent" || payload.objectKind === "continueEvent")) {
    return { allowed: false, message: "Break / Continue can only be placed inside Loop." };
  }
  if (!isRoot && payload.objectKind === "errorEvent") {
    return { allowed: false, message: "ErrorEvent cannot be placed inside Loop in this version." };
  }
  if (!isRoot && payload.objectKind === "parameterObject") {
    return { allowed: false, message: "ParameterObject cannot be placed inside Loop." };
  }
  return { allowed: true };
}

export function createObjectInCollectionFromDragPayload(
  input: AddMicroflowObjectFromDragPayloadInput & { collectionId: string },
): AddMicroflowObjectFromDragPayloadResult {
  return addMicroflowObjectFromDragPayload({
    ...input,
    parentLoopObjectId: parentLoopObjectIdForCollection(input.schema, input.collectionId),
  });
}

export function addObjectToDropTargetCollection(
  schema: MicroflowSchema,
  patch: MicroflowEditorGraphPatch,
  collectionId: string,
): MicroflowSchema {
  return addObjectToCollectionById(schema, patch, collectionId);
}

function parentLoopObjectIdForCollection(schema: MicroflowSchema, collectionId: string): string | undefined {
  const loop = flattenObjectCollection(schema.objectCollection).find(object =>
    object.kind === "loopedActivity" && object.objectCollection.id === collectionId
  );
  return loop?.kind === "loopedActivity" ? loop.id : undefined;
}

