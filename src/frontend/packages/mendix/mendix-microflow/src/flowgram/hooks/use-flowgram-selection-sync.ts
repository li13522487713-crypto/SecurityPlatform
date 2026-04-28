import type { MicroflowSchema } from "../../schema";
import type { FlowGramMicroflowSelection } from "../FlowGramMicroflowTypes";
import { selectionFromFlowGramEntityId } from "../adapters/flowgram-selection-sync";

export function useFlowGramSelectionSync() {
  return {
    selectionFromEntityId(schema: MicroflowSchema, id?: string): FlowGramMicroflowSelection {
      return selectionFromFlowGramEntityId(schema, id);
    },
  };
}

