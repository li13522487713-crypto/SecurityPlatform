import type { MicroflowAuthoringSchema } from "../schema/types";

export type MicroflowHistoryReason =
  | "init"
  | "addNode"
  | "deleteNode"
  | "moveNode"
  | "addFlow"
  | "deleteFlow"
  | "updateFlow"
  | "updateFlowCase"
  | "updateNodeProperty"
  | "updateActionProperty"
  | "updateEdgeProperty"
  | "addLoopNode"
  | "deleteLoopNode"
  | "addLoopFlow"
  | "deleteLoopFlow"
  | "autoLayout"
  | "bulkUpdate"
  | "schemaMigration";

export interface MicroflowHistorySelection {
  objectId?: string;
  flowId?: string;
  collectionId?: string;
}

export interface MicroflowHistorySnapshot {
  id: string;
  timestamp: string;
  reason: MicroflowHistoryReason;
  label: string;
  schema: MicroflowAuthoringSchema;
  selection?: MicroflowHistorySelection;
}

export interface MicroflowHistoryState {
  snapshots: MicroflowHistorySnapshot[];
  currentIndex: number;
  canUndo: boolean;
  canRedo: boolean;
  isRestoring: boolean;
}

export interface MicroflowHistoryRestoreResult {
  schema: MicroflowAuthoringSchema;
  selection?: MicroflowHistorySelection;
  snapshot: MicroflowHistorySnapshot;
}
