import { defineStore } from "pinia";
import {
  initSnapshotHistory,
  pushSnapshotHistory,
  redoSnapshotHistory,
  undoSnapshotHistory,
} from "@atlas/designer-core";

const MAX_HISTORY = 20;

const cloneSchema = (schema: Record<string, object | string | number | boolean | null>) =>
  JSON.parse(JSON.stringify(schema)) as Record<string, object | string | number | boolean | null>;

interface SchemaHistoryState {
  stack: Array<Record<string, object | string | number | boolean | null>>;
  pointer: number;
}

const snapshotOptions = {
  maxHistory: MAX_HISTORY,
  clone: cloneSchema,
};

export const useSchemaHistoryStore = defineStore("schemaHistory", {
  state: (): SchemaHistoryState => ({
    stack: [],
    pointer: -1,
  }),
  getters: {
    canUndo: (state) => state.pointer > 0,
    canRedo: (state) => state.pointer >= 0 && state.pointer < state.stack.length - 1,
    currentSchema: (state) =>
      state.pointer >= 0 ? cloneSchema(state.stack[state.pointer]) : null,
  },
  actions: {
    reset() {
      this.stack = [];
      this.pointer = -1;
    },
    init(schema: Record<string, object | string | number | boolean | null>) {
      initSnapshotHistory(this, schema, snapshotOptions);
    },
    pushState(schema: Record<string, object | string | number | boolean | null>) {
      pushSnapshotHistory(this, schema, snapshotOptions);
    },
    undo() {
      return undoSnapshotHistory(this, snapshotOptions);
    },
    redo() {
      return redoSnapshotHistory(this, snapshotOptions);
    },
  },
});
