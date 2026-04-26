import type {
  MicroflowAction,
  MicroflowActionActivity,
  MicroflowFlow,
  MicroflowObject,
  MicroflowParameter,
  MicroflowAuthoringSchema,
} from "../../schema";

export function updateObject<TObject extends MicroflowObject>(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  updater: (object: TObject) => TObject,
): MicroflowAuthoringSchema {
  return {
    ...schema,
    objectCollection: {
      ...schema.objectCollection,
      objects: schema.objectCollection.objects.map(object => object.id === objectId ? updater(object as TObject) : object),
    },
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
  };
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
