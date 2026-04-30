import type { MicroflowEditorGraphPatch, MicroflowSchema } from "../../schema";
import { findFlowWithCollection, findObjectWithCollection } from "../../schema/utils/object-utils";
import type { FlowGramMicroflowSelection } from "../FlowGramMicroflowTypes";
import { toMicroflowFlowId, toMicroflowObjectId } from "./flowgram-identity";
import { flowGramSelectionPatch } from "./flowgram-to-authoring-patch";

export function selectionFromFlowGramEntityId(schema: MicroflowSchema, id?: string): FlowGramMicroflowSelection {
  if (!id) {
    return { objectIds: [], flowIds: [], mode: "none" };
  }
  const flowId = toMicroflowFlowId(id);
  const flow = findFlowWithCollection(schema, flowId);
  if (flow) {
    return { flowId, objectId: undefined, collectionId: flow.collectionId, objectIds: [], flowIds: [flowId], mode: "single" };
  }
  const objectId = toMicroflowObjectId(id);
  const object = findObjectWithCollection(schema, objectId);
  return { objectId, flowId: undefined, collectionId: object?.collectionId, objectIds: object ? [objectId] : [], flowIds: [], mode: object ? "single" : "none" };
}

export function selectionFromFlowGramEntityIds(schema: MicroflowSchema, ids: readonly string[] = []): FlowGramMicroflowSelection {
  const objectIds: string[] = [];
  const flowIds: string[] = [];
  let collectionId: string | undefined;

  for (const id of ids) {
    const flowId = toMicroflowFlowId(id);
    const flow = findFlowWithCollection(schema, flowId);
    if (flow) {
      flowIds.push(flowId);
      collectionId ??= flow.collectionId;
      continue;
    }
    const objectId = toMicroflowObjectId(id);
    const object = findObjectWithCollection(schema, objectId);
    if (object) {
      objectIds.push(objectId);
      collectionId ??= object.collectionId;
    }
  }

  const objectId = objectIds[0];
  const flowId = objectId ? undefined : flowIds[0];
  const count = objectIds.length + flowIds.length;
  return {
    objectId,
    flowId,
    collectionId,
    objectIds,
    flowIds,
    mode: count === 0 ? "none" : count === 1 ? "single" : "multi",
  };
}

export function selectionPatchFromFlowGramEntityId(schema: MicroflowSchema, id?: string): MicroflowEditorGraphPatch {
  return flowGramSelectionPatch(selectionFromFlowGramEntityId(schema, id));
}
