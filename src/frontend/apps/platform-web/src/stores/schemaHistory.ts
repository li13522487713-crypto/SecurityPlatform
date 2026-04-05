import { defineStore } from "pinia";

const MAX_HISTORY = 20;

const cloneSchema = (schema: Record<string, object | string | number | boolean | null>) =>
  JSON.parse(JSON.stringify(schema)) as Record<string, object | string | number | boolean | null>;

interface SchemaHistoryState {
  stack: Array<Record<string, object | string | number | boolean | null>>;
  pointer: number;
}

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
      this.stack = [cloneSchema(schema)];
      this.pointer = 0;
    },
    pushState(schema: Record<string, object | string | number | boolean | null>) {
      const nextSchema = cloneSchema(schema);
      if (this.pointer >= 0) {
        const current = this.stack[this.pointer];
        if (JSON.stringify(current) === JSON.stringify(nextSchema)) {
          return;
        }
      }
      if (this.pointer < this.stack.length - 1) {
        this.stack = this.stack.slice(0, this.pointer + 1);
      }
      this.stack.push(nextSchema);
      if (this.stack.length > MAX_HISTORY) {
        this.stack.shift();
      }
      this.pointer = this.stack.length - 1;
    },
    undo() {
      if (!this.canUndo) {
        return null;
      }
      this.pointer -= 1;
      return cloneSchema(this.stack[this.pointer]);
    },
    redo() {
      if (!this.canRedo) {
        return null;
      }
      this.pointer += 1;
      return cloneSchema(this.stack[this.pointer]);
    },
  },
});
