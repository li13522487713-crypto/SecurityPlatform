/**
 * 【收尾】useSchemaHistory composable
 * Schema 历史栈：撤销/重做，默认栈深 50
 */
import { ref, computed, type Ref, type ComputedRef } from "vue";
import type { AmisSchema, SchemaHistoryOptions } from "@/types/amis";

export interface SchemaHistoryReturn {
  /** 当前 Schema */
  current: Ref<AmisSchema>;
  /** 是否可撤销 */
  canUndo: ComputedRef<boolean>;
  /** 是否可重做 */
  canRedo: ComputedRef<boolean>;
  /** 历史记录数 */
  historyCount: ComputedRef<number>;
  /** 推入新状态 */
  push: (schema: AmisSchema) => void;
  /** 撤销 */
  undo: () => void;
  /** 重做 */
  redo: () => void;
  /** 清空历史 */
  clear: (initialSchema?: AmisSchema) => void;
}

/**
 * useSchemaHistory — Schema 历史栈管理
 *
 * @example
 * ```ts
 * const { current, canUndo, canRedo, push, undo, redo } = useSchemaHistory(
 *   { type: 'page', body: [] },
 *   { maxDepth: 100 },
 * );
 *
 * // 用户编辑后
 * push(newSchema);
 *
 * // 撤销
 * if (canUndo.value) undo();
 *
 * // 重做
 * if (canRedo.value) redo();
 * ```
 */
export function useSchemaHistory(
  initialSchema: AmisSchema = { type: "page", body: [] },
  options: SchemaHistoryOptions = {},
): SchemaHistoryReturn {
  const maxDepth = options.maxDepth ?? 50;

  /** 历史栈 */
  const undoStack = ref<AmisSchema[]>([]);
  /** 重做栈 */
  const redoStack = ref<AmisSchema[]>([]);
  /** 当前 Schema */
  const current = ref<AmisSchema>(deepClone(initialSchema));

  const canUndo = computed(() => undoStack.value.length > 0);
  const canRedo = computed(() => redoStack.value.length > 0);
  const historyCount = computed(() => undoStack.value.length);

  function deepClone(obj: AmisSchema): AmisSchema {
    if (typeof structuredClone === "function") {
      try {
        return structuredClone(obj);
      } catch {
        // fallback
      }
    }
    return JSON.parse(JSON.stringify(obj)) as AmisSchema;
  }

  function push(schema: AmisSchema): void {
    // 保存当前状态到撤销栈
    undoStack.value.push(deepClone(current.value));

    // 限制栈深度
    if (undoStack.value.length > maxDepth) {
      undoStack.value.shift();
    }

    // 清空重做栈
    redoStack.value = [];

    // 更新当前状态
    current.value = deepClone(schema);
  }

  function undo(): void {
    if (!canUndo.value) return;

    // 当前状态入重做栈
    redoStack.value.push(deepClone(current.value));

    // 从撤销栈取出
    const prev = undoStack.value.pop()!;
    current.value = prev;
  }

  function redo(): void {
    if (!canRedo.value) return;

    // 当前状态入撤销栈
    undoStack.value.push(deepClone(current.value));

    // 从重做栈取出
    const next = redoStack.value.pop()!;
    current.value = next;
  }

  function clear(newInitialSchema?: AmisSchema): void {
    undoStack.value = [];
    redoStack.value = [];
    current.value = deepClone(newInitialSchema ?? initialSchema);
  }

  return {
    current,
    canUndo,
    canRedo,
    historyCount,
    push,
    undo,
    redo,
    clear,
  };
}
