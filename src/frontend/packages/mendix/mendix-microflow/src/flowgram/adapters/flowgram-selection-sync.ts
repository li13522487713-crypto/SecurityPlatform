import type { MicroflowEditorGraphPatch, MicroflowSchema } from "../../schema";
import { findFlowWithCollection, findObjectWithCollection } from "../../schema/utils/object-utils";
import type { FlowGramMicroflowSelection } from "../FlowGramMicroflowTypes";
import { flowGramSelectionPatch } from "./flowgram-to-authoring-patch";

export function selectionFromFlowGramEntityId(schema: MicroflowSchema, id?: string): FlowGramMicroflowSelection {
  if (!id) {
    return { objectIds: [], flowIds: [], mode: "none" };
  }
  const flow = findFlowWithCollection(schema, id);
  if (flow) {
    return { flowId: id, objectId: undefined, collectionId: flow.collectionId, objectIds: [], flowIds: [id], mode: "single" };
  }
  const object = findObjectWithCollection(schema, id);
  return { objectId: id, flowId: undefined, collectionId: object?.collectionId, objectIds: object ? [id] : [], flowIds: [], mode: object ? "single" : "none" };
}

export function selectionFromFlowGramEntityIds(schema: MicroflowSchema, ids: readonly string[] = []): FlowGramMicroflowSelection {
  const objectIds: string[] = [];
  const flowIds: string[] = [];
  let collectionId: string | undefined;

  for (const id of ids) {
    const flow = findFlowWithCollection(schema, id);
    if (flow) {
      flowIds.push(id);
      collectionId ??= flow.collectionId;
      continue;
    }
    const object = findObjectWithCollection(schema, id);
    if (object) {
      objectIds.push(id);
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
