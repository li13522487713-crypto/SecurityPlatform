import type { MicroflowAuthoringSchema } from "../schema/types";
import type { MicroflowHistoryReason, MicroflowHistorySelection } from "./history-types";

export const defaultMicroflowHistoryLimit = 100;

export function cloneMicroflowSchema(schema: MicroflowAuthoringSchema): MicroflowAuthoringSchema {
  const cloned = JSON.parse(JSON.stringify(schema)) as MicroflowAuthoringSchema;
  if (cloned.debug) {
    cloned.debug = {
      ...cloned.debug,
      traceFrames: [],
      lastTrace: [],
      activeFrameIndex: undefined,
    };
  }
  return cloned;
}

export function microflowSchemasEqual(left: MicroflowAuthoringSchema, right: MicroflowAuthoringSchema): boolean {
  return JSON.stringify(cloneMicroflowSchema(left)) === JSON.stringify(cloneMicroflowSchema(right));
}

export function selectionFromSchema(schema: MicroflowAuthoringSchema): MicroflowHistorySelection {
  return {
    objectId: schema.editor.selection.objectId ?? schema.editor.selectedObjectId,
    flowId: schema.editor.selection.flowId ?? schema.editor.selectedFlowId,
    collectionId: schema.editor.selection.collectionId ?? schema.editor.selectedCollectionId,
  };
}

export function labelForHistoryReason(reason: MicroflowHistoryReason): string {
  switch (reason) {
    case "addNode":
      return "Add node";
    case "deleteNode":
      return "Delete node";
    case "moveNode":
      return "Move node";
    case "addFlow":
      return "Add flow";
    case "deleteFlow":
      return "Delete flow";
    case "updateFlow":
      return "Update flow";
    case "updateFlowCase":
      return "Update flow case";
    case "updateNodeProperty":
      return "Update node property";
    case "updateActionProperty":
      return "Update action property";
    case "updateEdgeProperty":
      return "Update edge property";
    case "addLoopNode":
      return "Add loop node";
    case "deleteLoopNode":
      return "Delete loop node";
    case "addLoopFlow":
      return "Add loop flow";
    case "deleteLoopFlow":
      return "Delete loop flow";
    case "autoLayout":
      return "Auto layout";
    case "schemaMigration":
      return "Schema migration";
    case "bulkUpdate":
      return "Update schema";
    case "init":
    default:
      return "Initialize";
  }
}
