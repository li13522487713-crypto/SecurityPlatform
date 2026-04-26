import type { MicroflowEditorGraphPatch, MicroflowSchema } from "../../schema";
import type { FlowGramMicroflowSelection } from "../FlowGramMicroflowTypes";
import { flowGramSelectionPatch } from "./flowgram-to-authoring-patch";

export function selectionFromFlowGramEntityId(schema: MicroflowSchema, id?: string): FlowGramMicroflowSelection {
  if (!id) {
    return {};
  }
  if (schema.flows.some(flow => flow.id === id)) {
    return { flowId: id, objectId: undefined };
  }
  return { objectId: id, flowId: undefined };
}

export function selectionPatchFromFlowGramEntityId(schema: MicroflowSchema, id?: string): MicroflowEditorGraphPatch {
  return flowGramSelectionPatch(selectionFromFlowGramEntityId(schema, id));
}

