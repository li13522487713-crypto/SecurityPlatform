import type {
  MicroflowAction,
  MicroflowActionActivity,
  MicroflowFlow,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowParameter,
  MicroflowAuthoringSchema,
} from "../../schema";

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
  return {
    ...schema,
    parameters: schema.parameters.map(parameter => parameter.id === parameterId ? { ...parameter, ...patch } : parameter),
  };
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
