import type { MicroflowAuthoringSchema } from "../schema/types";
import type {
  MicroflowHistoryReason,
  MicroflowHistoryRestoreResult,
  MicroflowHistorySelection,
  MicroflowHistorySnapshot,
  MicroflowHistoryState,
} from "./history-types";
import {
  cloneMicroflowSchema,
  defaultMicroflowHistoryLimit,
  labelForHistoryReason,
  microflowSchemasEqual,
  selectionFromSchema,
} from "./history-utils";

export interface MicroflowHistoryManagerOptions {
  limit?: number;
}

export class MicroflowHistoryManager {
  private snapshots: MicroflowHistorySnapshot[] = [];
  private currentIndex = -1;
  private isRestoringValue = false;
  private readonly limit: number;

  constructor(options: MicroflowHistoryManagerOptions = {}) {
    this.limit = options.limit ?? defaultMicroflowHistoryLimit;
  }

  init(schema: MicroflowAuthoringSchema): MicroflowHistorySnapshot {
    const snapshot = this.createSnapshot(schema, "init", labelForHistoryReason("init"));
    this.snapshots = [snapshot];
    this.currentIndex = 0;
    this.isRestoringValue = false;
    return snapshot;
  }

  push(
    schema: MicroflowAuthoringSchema,
    reason: MicroflowHistoryReason,
    label = labelForHistoryReason(reason),
    selection?: MicroflowHistorySelection,
  ): MicroflowHistorySnapshot | undefined {
    const current = this.snapshots[this.currentIndex];
    if (current && microflowSchemasEqual(current.schema, schema)) {
      return undefined;
    }

    const nextSnapshots = this.currentIndex >= 0
      ? this.snapshots.slice(0, this.currentIndex + 1)
      : [];
    nextSnapshots.push(this.createSnapshot(schema, reason, label, selection));

    if (nextSnapshots.length > this.limit) {
      const overflow = nextSnapshots.length - this.limit;
      this.snapshots = nextSnapshots.slice(overflow);
      this.currentIndex = this.snapshots.length - 1;
    } else {
      this.snapshots = nextSnapshots;
      this.currentIndex = nextSnapshots.length - 1;
    }

    return this.snapshots[this.currentIndex];
  }

  undo(): MicroflowHistoryRestoreResult | undefined {
    if (!this.canUndo()) {
      return undefined;
    }
    this.isRestoringValue = true;
    this.currentIndex -= 1;
    return this.toRestoreResult(this.snapshots[this.currentIndex]);
  }

  redo(): MicroflowHistoryRestoreResult | undefined {
    if (!this.canRedo()) {
      return undefined;
    }
    this.isRestoringValue = true;
    this.currentIndex += 1;
    return this.toRestoreResult(this.snapshots[this.currentIndex]);
  }

  reset(schema: MicroflowAuthoringSchema): MicroflowHistorySnapshot {
    return this.init(schema);
  }

  clear(): void {
    this.snapshots = [];
    this.currentIndex = -1;
    this.isRestoringValue = false;
  }

  replaceCurrent(schema: MicroflowAuthoringSchema, reason: MicroflowHistoryReason = "bulkUpdate"): MicroflowHistorySnapshot | undefined {
    if (this.currentIndex < 0) {
      return this.init(schema);
    }
    const previous = this.snapshots[this.currentIndex];
    const snapshot = this.createSnapshot(
      schema,
      reason,
      previous.label,
      previous.selection ?? selectionFromSchema(schema),
    );
    this.snapshots = this.snapshots.map((item, index) => index === this.currentIndex ? snapshot : item);
    return snapshot;
  }

  finishRestoring(): void {
    this.isRestoringValue = false;
  }

  getState(): MicroflowHistoryState {
    return {
      snapshots: this.snapshots,
      currentIndex: this.currentIndex,
      canUndo: this.canUndo(),
      canRedo: this.canRedo(),
      isRestoring: this.isRestoringValue,
    };
  }

  canUndo(): boolean {
    return this.currentIndex > 0;
  }

  canRedo(): boolean {
    return this.currentIndex >= 0 && this.currentIndex < this.snapshots.length - 1;
  }

  private createSnapshot(
    schema: MicroflowAuthoringSchema,
    reason: MicroflowHistoryReason,
    label: string,
    selection: MicroflowHistorySelection = selectionFromSchema(schema),
  ): MicroflowHistorySnapshot {
    return {
      id: `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`,
      timestamp: new Date().toISOString(),
      reason,
      label,
      schema: cloneMicroflowSchema(schema),
      selection,
    };
  }

  private toRestoreResult(snapshot: MicroflowHistorySnapshot): MicroflowHistoryRestoreResult {
    return {
      schema: cloneMicroflowSchema(snapshot.schema),
      selection: snapshot.selection,
      snapshot,
    };
  }
}
