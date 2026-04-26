import type { MicroflowEditorGraphPatch, MicroflowSchema } from "../../schema";
import { findFlowWithCollection, findObjectWithCollection } from "../../schema/utils/object-utils";
import type { FlowGramMicroflowSelection } from "../FlowGramMicroflowTypes";
import { flowGramSelectionPatch } from "./flowgram-to-authoring-patch";

export function selectionFromFlowGramEntityId(schema: MicroflowSchema, id?: string): FlowGramMicroflowSelection {
  if (!id) {
    return {};
  }
  const flow = findFlowWithCollection(schema, id);
  if (flow) {
    return { flowId: id, objectId: undefined, collectionId: flow.collectionId };
  }
  const object = findObjectWithCollection(schema, id);
  return { objectId: id, flowId: undefined, collectionId: object?.collectionId };
}

export function selectionPatchFromFlowGramEntityId(schema: MicroflowSchema, id?: string): MicroflowEditorGraphPatch {
  return flowGramSelectionPatch(selectionFromFlowGramEntityId(schema, id));
}

