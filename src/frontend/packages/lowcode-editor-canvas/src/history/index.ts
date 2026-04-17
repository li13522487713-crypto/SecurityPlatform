/**
 * 撤销/重做历史栈抽象（M04 C04-8）。
 *
 * 强约束（PLAN.md §M16）：
 * - 协同模式（M16 yjs CRDT 历史）与本地模式互斥；通过 IHistoryProvider 接口切换。
 * - 本地实现：基于切片栈（≥ 50 步），带索引指针，支持 record/undo/redo/clear。
 *
 * 历史 entry 不强制为完整 schema 快照——可由调用方提供 patch / inverse-patch 形式。
 */

import type { JsonValue } from '@atlas/lowcode-schema';

export interface HistoryEntry {
  /** 应用本 entry 时使用的载荷（可能是完整 snapshot 或 diff）。*/
  redoPayload: JsonValue;
  /** 反向回退时使用的载荷。*/
  undoPayload: JsonValue;
  /** 描述性 label（用于命令面板列出"撤销 XX"）。*/
  label?: string;
  /** 时间戳。*/
  timestamp: number;
}

export interface IHistoryProvider {
  record(entry: HistoryEntry): void;
  canUndo(): boolean;
  canRedo(): boolean;
  undo(): HistoryEntry | null;
  redo(): HistoryEntry | null;
  clear(): void;
  getCurrentLabel(): string | undefined;
  /** 当前历史深度（含已撤销项）。*/
  size(): number;
}

export class LocalSliceHistoryProvider implements IHistoryProvider {
  private readonly capacity: number;
  private stack: HistoryEntry[] = [];
  /** 当前指针：指向"下一次 redo 起点"。-1 表示无可 redo。*/
  private pointer = -1;

  constructor(capacity = 50) {
    this.capacity = Math.max(1, capacity);
  }

  record(entry: HistoryEntry): void {
    // 落入新 entry → 截断 pointer 之后的项
    this.stack = this.stack.slice(0, this.pointer + 1);
    this.stack.push(entry);
    if (this.stack.length > this.capacity) {
      this.stack.shift();
    }
    this.pointer = this.stack.length - 1;
  }

  canUndo(): boolean {
    return this.pointer >= 0;
  }

  canRedo(): boolean {
    return this.pointer < this.stack.length - 1;
  }

  undo(): HistoryEntry | null {
    if (!this.canUndo()) return null;
    const entry = this.stack[this.pointer];
    this.pointer -= 1;
    return entry;
  }

  redo(): HistoryEntry | null {
    if (!this.canRedo()) return null;
    this.pointer += 1;
    return this.stack[this.pointer];
  }

  clear(): void {
    this.stack = [];
    this.pointer = -1;
  }

  getCurrentLabel(): string | undefined {
    if (this.pointer < 0) return undefined;
    return this.stack[this.pointer]?.label;
  }

  size(): number {
    return this.stack.length;
  }
}
