import { defineStore } from "pinia";

const MAX_HISTORY = 10;

function deepClone<T>(value: T): T {
  if (typeof structuredClone === "function") {
    try {
      return structuredClone(value);
    } catch {
      /* fall through */
    }
  }
  return JSON.parse(JSON.stringify(value)) as T;
}

interface SchemaHistoryState {
  stack: string[];
  pointer: number;
}

export const useSchemaHistoryStore = defineStore("schemaHistory", {
  state: (): SchemaHistoryState => ({
    stack: [],
    pointer: -1,
  }),

  getters: {
    canUndo: (state): boolean => state.pointer > 0,
    canRedo: (state): boolean => state.pointer < state.stack.length - 1,
    currentSchema: (state): Record<string, unknown> | null => {
      if (state.pointer < 0 || state.pointer >= state.stack.length) {
        return null;
      }
      try {
        return JSON.parse(state.stack[state.pointer]) as Record<string, unknown>;
      } catch {
        return null;
      }
    },
  },

  actions: {
    reset() {
      this.stack = [];
      this.pointer = -1;
    },

    init(schema: Record<string, unknown>) {
      const json = JSON.stringify(deepClone(schema));
      this.stack = [json];
      this.pointer = 0;
    },

    pushState(schema: Record<string, unknown>) {
      const json = JSON.stringify(deepClone(schema));

      if (this.pointer >= 0 && this.stack[this.pointer] === json) {
        return;
      }

      if (this.pointer < this.stack.length - 1) {
        this.stack = this.stack.slice(0, this.pointer + 1);
      }

      this.stack.push(json);

      if (this.stack.length > MAX_HISTORY) {
        this.stack = this.stack.slice(this.stack.length - MAX_HISTORY);
      }

      this.pointer = this.stack.length - 1;
    },

    undo(): Record<string, unknown> | null {
      if (!this.canUndo) {
        return null;
      }
      this.pointer -= 1;
      return this.currentSchema;
    },

    redo(): Record<string, unknown> | null {
      if (!this.canRedo) {
        return null;
      }
      this.pointer += 1;
      return this.currentSchema;
    },
  },
});
