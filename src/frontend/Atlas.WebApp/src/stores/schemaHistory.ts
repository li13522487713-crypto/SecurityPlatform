import { defineStore } from "pinia";
import {
  applyStructuralPatch,
  cloneStructuralValue,
  createStructuralPatchPair,
  type StructuralPatchOperation,
} from "@/utils/structuralPatch";

const MAX_HISTORY = 10;

interface SchemaPatchEntry {
  forward: StructuralPatchOperation[];
  backward: StructuralPatchOperation[];
}

interface SchemaHistoryState {
  baseSchema: Record<string, unknown> | null;
  currentSchemaValue: Record<string, unknown> | null;
  stack: SchemaPatchEntry[];
  pointer: number;
}

export const useSchemaHistoryStore = defineStore("schemaHistory", {
  state: (): SchemaHistoryState => ({
    baseSchema: null,
    currentSchemaValue: null,
    stack: [],
    pointer: -1,
  }),

  getters: {
    canUndo: (state): boolean => state.pointer >= 0,
    canRedo: (state): boolean => state.pointer < state.stack.length - 1,
    currentSchema: (state): Record<string, unknown> | null =>
      state.currentSchemaValue ? cloneStructuralValue(state.currentSchemaValue) : null,
  },

  actions: {
    reset() {
      this.baseSchema = null;
      this.currentSchemaValue = null;
      this.stack = [];
      this.pointer = -1;
    },

    init(schema: Record<string, unknown>) {
      const initial = cloneStructuralValue(schema);
      this.baseSchema = initial;
      this.currentSchemaValue = cloneStructuralValue(initial);
      this.stack = [];
      this.pointer = -1;
    },

    pushState(schema: Record<string, unknown>) {
      if (!this.currentSchemaValue || !this.baseSchema) {
        this.init(schema);
        return;
      }

      const patchEntry = createStructuralPatchPair(this.currentSchemaValue, schema);
      if (patchEntry.forward.length === 0) {
        return;
      }

      if (this.pointer < this.stack.length - 1) {
        this.stack = this.stack.slice(0, this.pointer + 1);
      }

      this.stack.push(patchEntry);
      this.pointer += 1;

      this.currentSchemaValue = applyStructuralPatch(this.currentSchemaValue, patchEntry.forward);

      if (this.stack.length > MAX_HISTORY) {
        const removed = this.stack.shift();
        if (removed) {
          this.baseSchema = applyStructuralPatch(this.baseSchema, removed.forward);
          this.pointer = Math.max(this.pointer - 1, -1);
        }
      }
    },

    undo(): Record<string, unknown> | null {
      if (!this.canUndo || !this.currentSchemaValue) {
        return null;
      }

      const entry = this.stack[this.pointer];
      this.currentSchemaValue = applyStructuralPatch(this.currentSchemaValue, entry.backward);
      this.pointer -= 1;
      return cloneStructuralValue(this.currentSchemaValue);
    },

    redo(): Record<string, unknown> | null {
      if (!this.canRedo || !this.currentSchemaValue) {
        return null;
      }

      const entry = this.stack[this.pointer + 1];
      this.currentSchemaValue = applyStructuralPatch(this.currentSchemaValue, entry.forward);
      this.pointer += 1;
      return cloneStructuralValue(this.currentSchemaValue);
    },
  },
});
