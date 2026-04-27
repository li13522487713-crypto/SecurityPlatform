import {
  getMicroflowNodeRegistryKey,
  microflowActionRegistryByActivityType,
  type MicroflowNodeRegistryEntry
} from "../node-registry";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import { buildVariableIndex } from "../variables";
import type { MicroflowMetadataCatalog } from "../metadata";
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

function nextDuplicateCaption(caption: string | undefined, fallback: string): string {
  const base = caption?.trim() || fallback;
  return `${base} Copy`;
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
  const removedObjectIds = collectObjectAndDescendantIds(schema.objectCollection, objectId);
  const removedObjects = flattenObjectCollection(schema.objectCollection).filter(object => removedObjectIds.has(object.id));
  const removedParameterIds = new Set(removedObjects
    .filter((object): object is Extract<MicroflowObject, { kind: "parameterObject" }> => object.kind === "parameterObject")
    .map(object => object.parameterId));
  const flows = schema.flows.filter(flow => !removedObjectIds.has(flow.originObjectId) && !removedObjectIds.has(flow.destinationObjectId));
  const objectCollection = removeFlowsFromCollection(
    removeObjectFromCollection(schema.objectCollection, objectId),
    removedObjectIds,
  );
  const selectedFlowStillExists = schema.editor.selection.flowId
    ? [...flows, ...collectFlowsRecursive({ ...schema, flows, objectCollection })].some(flow => flow.id === schema.editor.selection.flowId)
    : false;
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
          : schema.editor.selection.collectionId
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
  }
  if (copy.kind === "parameterObject") {
    const sourceParameterId = object.kind === "parameterObject" ? object.parameterId : undefined;
    const parameter = schema.parameters.find(item => item.id === sourceParameterId);
    const parameterId = uniqueSchemaId(schema, "param-copy");
    const parameterName = nextParameterName(schema);
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
        collectionId: located.collectionId
      },
      selectedObjectId: copy.id,
      selectedFlowId: undefined,
      selectedCollectionId: located.collectionId
    }
  };
}

export function moveObject(schema: MicroflowSchema, objectId: string, position: MicroflowPoint): MicroflowSchema {
  return updateObject(schema, objectId, object => ({ ...object, relativeMiddlePoint: position }));
}

export function resizeObject(schema: MicroflowSchema, objectId: string, size: MicroflowSize): MicroflowSchema {
  return updateObject(schema, objectId, object => ({ ...object, size }));
}

export function addFlow(schema: MicroflowSchema, flow: MicroflowFlow): MicroflowSchema {
  const duplicate = collectFlowsRecursive(schema).some(item =>
    item.originObjectId === flow.originObjectId &&
    item.destinationObjectId === flow.destinationObjectId &&
    (item.originConnectionIndex ?? 0) === (flow.originConnectionIndex ?? 0) &&
    (item.destinationConnectionIndex ?? 0) === (flow.destinationConnectionIndex ?? 0) &&
    item.kind === flow.kind &&
    (item.kind === "annotation" || flow.kind === "annotation" || item.editor.edgeKind === flow.editor.edgeKind)
  );
  if (duplicate) {
    return schema;
  }
  const sourceLocation = findObjectWithCollection(schema, flow.originObjectId);
  const targetLocation = findObjectWithCollection(schema, flow.destinationObjectId);
  const collectionId = sourceLocation && targetLocation && sourceLocation.collectionId === targetLocation.collectionId
    ? sourceLocation.collectionId
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
    flows: schema.flows.map(flow => flow.id === flowId ? mapper(flow) : flow)
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
        collectionId: target.editor.selection.flowId === flowId ? undefined : target.editor.selection.collectionId
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
    flows: schema.flows.filter(flow => flow.id !== flowId)
  }));
}

export function splitFlowWithObject(schema: MicroflowSchema, flowId: string, object: MicroflowObject): MicroflowSchema {
  const flow = collectFlowsRecursive(schema).find(item => item.id === flowId);
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
  return refreshDerivedState({
    ...addObject(schema, object),
    flows: schema.flows.filter(item => item.id !== flowId).concat(first, second)
  });
}

export function createObjectFromRegistry(entry: MicroflowNodeRegistryEntry, position: MicroflowPoint, id = createStableId(getMicroflowNodeRegistryKey(entry).replace(":", "-"))): MicroflowObject {
  const config = entry.defaultConfig as Record<string, unknown>;
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
        iteratorVariableName: String(config.itemVariableName ?? "currentItem"),
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
      text: String(config.text ?? entry.title)
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
      outputVariableName: String(config.resultVariableName ?? config.objectVariableName ?? "result"),
      retrieveSource: String(config.retrieveMode ?? "database") === "association"
        ? {
            kind: "association",
            officialType: "Microflows$AssociationRetrieveSource",
            associationQualifiedName: typeof config.association === "string" ? config.association : null,
            startVariableName: String(config.objectVariableName ?? "context")
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
      entityQualifiedName: String(config.entity ?? "System.Object"),
      outputVariableName: String(config.resultVariableName ?? "object"),
      memberChanges: [],
      commit: { enabled: false, withEvents: true, refreshInClient: false }
    } as MicroflowAction;
  }
  if (kind === "changeMembers") {
    return {
      ...base,
      kind: "changeMembers",
      officialType: "Microflows$ChangeMembersAction",
      changeVariableName: String(config.objectVariableName ?? "object"),
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
      objectOrListVariableName: String(config.objectVariableName ?? "object"),
      withEvents: Boolean(config.withEvents ?? true),
      refreshInClient: Boolean(config.refreshClient ?? false)
    } as MicroflowAction;
  }
  if (kind === "delete") {
    return {
      ...base,
      kind: "delete",
      officialType: "Microflows$DeleteAction",
      objectOrListVariableName: String(config.objectVariableName ?? "object"),
      withEvents: Boolean(config.withEvents ?? true),
      deleteBehavior: "deleteOnly"
    } as MicroflowAction;
  }
  if (kind === "rollback") {
    return {
      ...base,
      kind: "rollback",
      officialType: "Microflows$RollbackAction",
      objectOrListVariableName: String(config.objectVariableName ?? "object"),
      refreshInClient: Boolean(config.refreshClient ?? false)
    } as MicroflowAction;
  }
  if (kind === "callMicroflow") {
    return {
      ...base,
      kind: "callMicroflow",
      officialType: "Microflows$MicroflowCallAction",
      targetMicroflowId: String(config.targetMicroflowId ?? ""),
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
      variableName: String(config.variableName ?? "variable"),
      dataType: { kind: "unknown", reason: "defaultActionFromRegistry" },
      initialValue: undefined,
      readonly: false
    } as MicroflowAction;
  }
  if (kind === "changeVariable") {
    return {
      ...base,
      kind: "changeVariable",
      officialType: "Microflows$ChangeVariableAction",
      targetVariableName: String(config.variableName ?? "variable"),
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
  const id = createStableId("flow");
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
  originObjectId: string;
  destinationObjectId: string;
  label?: string;
  description?: string;
}): MicroflowAnnotationFlow {
  const id = createStableId("annotation-flow");
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
      ? { ...flow, line: update.line ?? flow.line, editor: { ...flow.editor, label: update.label ?? flow.editor.label } }
      : { ...flow, line: update.line ?? flow.line, editor: { ...flow.editor, label: update.label ?? flow.editor.label } });
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
  if (patch.viewport || hasSelectedObject || hasSelectedFlow || hasSelectedCollection) {
    next = {
      ...next,
      editor: {
        ...next.editor,
        viewport: patch.viewport ?? next.editor.viewport,
        selection: {
          objectId: hasSelectedObject ? patch.selectedObjectId : next.editor.selection.objectId,
          flowId: hasSelectedFlow ? patch.selectedFlowId : next.editor.selection.flowId,
          collectionId: hasSelectedCollection ? patch.selectedCollectionId : next.editor.selection.collectionId
        },
        selectedCollectionId: hasSelectedCollection ? patch.selectedCollectionId : next.editor.selectedCollectionId
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
