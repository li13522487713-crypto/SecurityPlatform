import {
  getMicroflowNodeRegistryKey,
  microflowActionRegistryByActivityType,
  type MicroflowNodeRegistryEntry
} from "../node-registry";
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
  return refreshDerivedState({
    ...schema,
    objectCollection: removeObjectFromCollection(schema.objectCollection, objectId),
    flows: schema.flows.filter(flow => flow.originObjectId !== objectId && flow.destinationObjectId !== objectId),
    editor: {
      ...schema.editor,
      selection: {
        objectId: schema.editor.selection.objectId === objectId ? undefined : schema.editor.selection.objectId,
        flowId: schema.editor.selection.flowId
      }
    }
  });
}

export function duplicateObject(schema: MicroflowSchema, objectId: string): MicroflowSchema {
  const object = findObject(schema, objectId);
  if (!object) {
    return schema;
  }
  const copy = clone(object);
  copy.id = `${object.id}-copy-${Date.now()}`;
  copy.stableId = copy.id;
  copy.caption = `${object.caption ?? object.id} Copy`;
  copy.relativeMiddlePoint = { x: object.relativeMiddlePoint.x + 36, y: object.relativeMiddlePoint.y + 36 };
  return addObject(schema, copy);
}

export function moveObject(schema: MicroflowSchema, objectId: string, position: MicroflowPoint): MicroflowSchema {
  return updateObject(schema, objectId, object => ({ ...object, relativeMiddlePoint: position }));
}

export function resizeObject(schema: MicroflowSchema, objectId: string, size: MicroflowSize): MicroflowSchema {
  return updateObject(schema, objectId, object => ({ ...object, size }));
}

export function addFlow(schema: MicroflowSchema, flow: MicroflowFlow): MicroflowSchema {
  return refreshDerivedState({ ...schema, flows: [...schema.flows, flow] });
}

export function updateFlow(schema: MicroflowSchema, flowId: string, mapper: (flow: MicroflowFlow) => MicroflowFlow): MicroflowSchema {
  return refreshDerivedState({
    ...schema,
    flows: schema.flows.map(flow => flow.id === flowId ? mapper(flow) : flow)
  });
}

export function deleteFlow(schema: MicroflowSchema, flowId: string): MicroflowSchema {
  return refreshDerivedState({
    ...schema,
    flows: schema.flows.filter(flow => flow.id !== flowId),
    editor: {
      ...schema.editor,
      selection: {
        ...schema.editor.selection,
        flowId: schema.editor.selection.flowId === flowId ? undefined : schema.editor.selection.flowId
      }
    }
  });
}

export function splitFlowWithObject(schema: MicroflowSchema, flowId: string, object: MicroflowObject): MicroflowSchema {
  const flow = schema.flows.find(item => item.id === flowId);
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

export function createObjectFromRegistry(entry: MicroflowNodeRegistryEntry, position: MicroflowPoint, id = `${getMicroflowNodeRegistryKey(entry).replace(":", "-")}-${Date.now()}`): MicroflowObject {
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
  if (entry.type === "event") {
    const eventType = String(config.eventType ?? "start");
    if (eventType === "start") {
      return { ...base, kind: "startEvent", officialType: "Microflows$StartEvent", trigger: { type: "manual" } };
    }
    if (eventType === "end") {
      return { ...base, kind: "endEvent", officialType: "Microflows$EndEvent", endBehavior: { type: "normalReturn" } };
    }
    if (eventType === "error") {
      return {
        ...base,
        kind: "errorEvent",
        officialType: "Microflows$ErrorEvent",
        error: { sourceVariableName: "$latestError", messageExpression: expression("$latestError") }
      };
    }
    if (eventType === "break") {
      return { ...base, kind: "breakEvent", officialType: "Microflows$BreakEvent" };
    }
    return { ...base, kind: "continueEvent", officialType: "Microflows$ContinueEvent" };
  }
  if (entry.type === "decision") {
    return {
      ...base,
      kind: "exclusiveSplit",
      officialType: "Microflows$ExclusiveSplit",
      splitCondition: { kind: "expression", expression: expression("true", { kind: "boolean" }), resultType: "boolean" },
      errorHandlingType: "rollback"
    };
  }
  if (entry.type === "objectTypeDecision") {
    return {
      ...base,
      kind: "inheritanceSplit",
      officialType: "Microflows$InheritanceSplit",
      inputObjectVariableName: "object",
      entity: { generalizedEntityQualifiedName: "System.Object", allowedSpecializations: [] },
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
        listVariableName: String(config.iterableVariableName ?? "items"),
        iteratorVariableName: String(config.itemVariableName ?? "item"),
        currentIndexVariableName: "$currentIndex"
      },
      objectCollection: {
        id: `${id}-collection`,
        officialType: "Microflows$MicroflowObjectCollection",
        objects: []
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
  const kind = registryAction?.kind ?? "logMessage";
  const base = {
    id: `action-${objectId}`,
    officialType: registryAction?.officialType ?? "Microflows$LogMessageAction",
    kind,
    errorHandlingType: "rollback" as const,
    documentation: entry.documentation.summary,
    editor: {
      category: registryAction?.category ?? "logging",
      iconKey: registryAction?.iconKey ?? entry.iconKey,
      availability: registryAction?.availability ?? "supported"
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
    };
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
    };
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
    };
  }
  if (kind === "commit") {
    return {
      ...base,
      kind: "commit",
      officialType: "Microflows$CommitAction",
      objectOrListVariableName: String(config.objectVariableName ?? "object"),
      withEvents: Boolean(config.withEvents ?? true),
      refreshInClient: Boolean(config.refreshClient ?? false)
    };
  }
  if (kind === "delete") {
    return {
      ...base,
      kind: "delete",
      officialType: "Microflows$DeleteAction",
      objectOrListVariableName: String(config.objectVariableName ?? "object"),
      withEvents: Boolean(config.withEvents ?? true),
      deleteBehavior: "deleteOnly"
    };
  }
  if (kind === "rollback") {
    return {
      ...base,
      kind: "rollback",
      officialType: "Microflows$RollbackAction",
      objectOrListVariableName: String(config.objectVariableName ?? "object"),
      refreshInClient: Boolean(config.refreshClient ?? false)
    };
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
    };
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
    };
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
    };
  }
  return {
    ...base,
    kind,
    officialType: registryAction?.officialType ?? "Microflows$GenericAction"
  };
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
  const id = `flow-${Date.now()}-${Math.round(Math.random() * 10000)}`;
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
  const id = `flow-${Date.now()}-${Math.round(Math.random() * 10000)}`;
  return {
    id,
    stableId: id,
    kind: "annotation",
    officialType: "Microflows$AnnotationFlow",
    originObjectId: input.originObjectId,
    destinationObjectId: input.destinationObjectId,
    originConnectionIndex: 0,
    destinationConnectionIndex: 0,
    line: defaultMicroflowLine(),
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
  if (patch.viewport || patch.selectedObjectId !== undefined || patch.selectedFlowId !== undefined) {
    next = {
      ...next,
      editor: {
        ...next.editor,
        viewport: patch.viewport ?? next.editor.viewport,
        selection: {
          objectId: patch.selectedObjectId ?? next.editor.selection.objectId,
          flowId: patch.selectedFlowId ?? next.editor.selection.flowId
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

export function refreshDerivedState(schema: MicroflowSchema): MicroflowSchema {
  return {
    ...schema,
    variables: rebuildVariableIndex(schema),
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
  for (const flow of schema.flows.filter((item): item is MicroflowSequenceFlow => item.kind === "sequence" && item.isErrorHandler)) {
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
