import type {
  MicroflowAction,
  MicroflowActionActivity,
  MicroflowAnnotation,
  MicroflowDataType,
  MicroflowEndEvent,
  MicroflowExclusiveMerge,
  MicroflowExclusiveSplit,
  MicroflowFlow,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowParameter,
  MicroflowStartEvent,
  MicroflowAuthoringSchema,
} from "../../schema";
import {
  renameMicroflowParameter,
  syncParameterDefinitionToObject,
  updateEndEventReturnValue,
  updateMicroflowReturnType,
  updateMicroflowParameterType,
  upsertMicroflowParameter,
} from "../../schema/utils";

function mapObjectCollection(
  collection: MicroflowObjectCollection,
  objectId: string,
  updater: (object: MicroflowObject) => MicroflowObject,
): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects.map(object => {
      const next = object.id === objectId ? updater(object) : object;
      return next.kind === "loopedActivity"
        ? { ...next, objectCollection: mapObjectCollection(next.objectCollection, objectId, updater) }
        : next;
    }),
  };
}

function mapFlowCollection(
  collection: MicroflowObjectCollection,
  flowId: string,
  updater: (flow: MicroflowFlow) => MicroflowFlow,
): MicroflowObjectCollection {
  return {
    ...collection,
    flows: collection.flows?.map(flow => flow.id === flowId ? updater(flow) : flow),
    objects: collection.objects.map(object => object.kind === "loopedActivity"
      ? { ...object, objectCollection: mapFlowCollection(object.objectCollection, flowId, updater) }
      : object),
  };
}

export function updateObject<TObject extends MicroflowObject>(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  updater: (object: TObject) => TObject,
): MicroflowAuthoringSchema {
  return {
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, objectId, object => updater(object as TObject)),
  };
}

export function updateObjectEditor(
  object: MicroflowObject,
  patch: Partial<MicroflowObject["editor"]>,
): MicroflowObject {
  return { ...object, editor: { ...object.editor, ...patch } } as MicroflowObject;
}

export function updateActionActivity(
  activity: MicroflowActionActivity,
  actionPatch: Partial<MicroflowAction>,
): MicroflowActionActivity {
  return {
    ...activity,
    action: { ...activity.action, ...actionPatch } as MicroflowAction,
  };
}

export function updateActionConfig(
  activity: MicroflowActionActivity,
  actionKind: MicroflowAction["kind"],
  patch: Partial<MicroflowAction>,
): MicroflowActionActivity {
  if (activity.action.kind !== actionKind) {
    return activity;
  }
  return updateActionActivity(activity, patch);
}

export function updateFlow(
  schema: MicroflowAuthoringSchema,
  flowId: string,
  updater: (flow: MicroflowFlow) => MicroflowFlow,
): MicroflowAuthoringSchema {
  return {
    ...schema,
    flows: schema.flows.map(flow => flow.id === flowId ? updater(flow) : flow),
    objectCollection: mapFlowCollection(schema.objectCollection, flowId, updater),
  };
}

export function updateObjectCaption<TObject extends MicroflowObject>(object: TObject, caption: string): TObject {
  return { ...object, caption } as TObject;
}

export function updateObjectDescription<TObject extends MicroflowObject>(object: TObject, documentation: string): TObject {
  return { ...object, documentation } as TObject;
}

export function updateMicroflowDocumentProperties(
  schema: MicroflowAuthoringSchema,
  patch: Partial<Pick<MicroflowAuthoringSchema, "description" | "documentation" | "returnType">>,
): MicroflowAuthoringSchema {
  const withText = {
    ...schema,
    description: patch.description ?? schema.description,
    documentation: patch.documentation ?? schema.documentation,
  };
  return patch.returnType ? updateMicroflowReturnType(withText, patch.returnType) : withText;
}

export function updateMicroflowObjectBase(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  patch: Partial<Pick<MicroflowObject, "caption" | "documentation" | "disabled">>,
): MicroflowAuthoringSchema {
  return updateObject(schema, objectId, object => ({ ...object, ...patch }) as MicroflowObject);
}

export function updateMicroflowObjectCaption(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  caption: string,
): MicroflowAuthoringSchema {
  return updateMicroflowObjectBase(schema, objectId, { caption });
}

export function updateMicroflowObjectDescription(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  description: string,
): MicroflowAuthoringSchema {
  return updateMicroflowObjectBase(schema, objectId, { documentation: description });
}

export function updateStartEventConfig(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  patch: Partial<MicroflowStartEvent>,
): MicroflowAuthoringSchema {
  return updateObject<MicroflowStartEvent>(schema, objectId, object => ({ ...object, ...patch }));
}

export function updateEndEventConfig(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  patch: Partial<MicroflowEndEvent> & { returnType?: MicroflowDataType },
): MicroflowAuthoringSchema {
  const { returnType, returnValue, ...objectPatch } = patch;
  let nextSchema = updateObject<MicroflowEndEvent>(schema, objectId, object => ({ ...object, ...objectPatch }));
  if (returnType) {
    nextSchema = updateMicroflowReturnType(nextSchema, returnType);
  }
  if ("returnValue" in patch) {
    nextSchema = updateEndEventReturnValue(nextSchema, objectId, returnValue);
  }
  return nextSchema;
}

export function updateParameterObjectConfig(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  patch: Partial<MicroflowParameter>,
): MicroflowAuthoringSchema {
  const object = findObjectInCollection(schema.objectCollection, objectId);
  if (!object || object.kind !== "parameterObject") {
    return schema;
  }
  return updateParameter(schema, object.parameterId, patch);
}

export function updateAnnotationObjectConfig(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  patch: Partial<MicroflowAnnotation> & { colorToken?: string },
): MicroflowAuthoringSchema {
  return updateObject<MicroflowAnnotation>(schema, objectId, object => ({
    ...object,
    ...patch,
    editor: patch.colorToken !== undefined ? { ...object.editor, colorToken: patch.colorToken } : object.editor,
  }));
}

export function updateDecisionObjectConfig(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  patch: Partial<MicroflowExclusiveSplit>,
): MicroflowAuthoringSchema {
  return updateObject<MicroflowExclusiveSplit>(schema, objectId, object => ({ ...object, ...patch }));
}

export function updateMergeObjectConfig(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  patch: Partial<MicroflowExclusiveMerge>,
): MicroflowAuthoringSchema {
  return updateObject<MicroflowExclusiveMerge>(schema, objectId, object => ({ ...object, ...patch }));
}

export function updateMicroflowFlowBase(
  schema: MicroflowAuthoringSchema,
  flowId: string,
  patch: Partial<MicroflowFlow>,
): MicroflowAuthoringSchema {
  return updateFlow(schema, flowId, flow => ({
    ...flow,
    ...patch,
    editor: patch.editor ? { ...flow.editor, ...patch.editor } : flow.editor,
  } as MicroflowFlow));
}

export function updateFlowLabel<TFlow extends MicroflowFlow>(flow: TFlow, label: string): TFlow {
  return {
    ...flow,
    editor: {
      ...flow.editor,
      label,
    },
  } as TFlow;
}

export function updateFlowEditor(
  flow: MicroflowFlow,
  patch: Partial<MicroflowFlow["editor"]>,
): MicroflowFlow {
  return {
    ...flow,
    editor: { ...flow.editor, ...patch },
  } as MicroflowFlow;
}

export function updateParameter(
  schema: MicroflowAuthoringSchema,
  parameterId: string,
  patch: Partial<MicroflowParameter>,
): MicroflowAuthoringSchema {
  const current = schema.parameters.find(parameter => parameter.id === parameterId);
  if (!current) {
    return schema;
  }
  let nextSchema = schema;
  if (patch.name !== undefined) {
    nextSchema = renameMicroflowParameter(nextSchema, parameterId, patch.name);
  }
  if (patch.dataType !== undefined) {
    nextSchema = updateMicroflowParameterType(nextSchema, parameterId, patch.dataType);
  }
  const remainingPatch = { ...patch };
  delete remainingPatch.name;
  delete remainingPatch.dataType;
  if (Object.keys(remainingPatch).length > 0) {
    const latest = nextSchema.parameters.find(parameter => parameter.id === parameterId) ?? current;
    nextSchema = upsertMicroflowParameter(nextSchema, { ...latest, ...remainingPatch });
  }
  return syncParameterDefinitionToObject(nextSchema, parameterId);
}

export function updateLoopObjectCollection(
  schema: MicroflowAuthoringSchema,
  loopObjectId: string,
  updater: (collection: Extract<MicroflowObject, { kind: "loopedActivity" }>["objectCollection"]) => Extract<MicroflowObject, { kind: "loopedActivity" }>["objectCollection"],
): MicroflowAuthoringSchema {
  return updateObject<Extract<MicroflowObject, { kind: "loopedActivity" }>>(schema, loopObjectId, loop => ({
    ...loop,
    objectCollection: updater(loop.objectCollection),
  }));
}

function findObjectInCollection(collection: MicroflowObjectCollection, objectId: string): MicroflowObject | undefined {
  for (const object of collection.objects) {
    if (object.id === objectId) {
      return object;
    }
    if (object.kind === "loopedActivity") {
      const nested = findObjectInCollection(object.objectCollection, objectId);
      if (nested) {
        return nested;
      }
    }
  }
  return undefined;
}
