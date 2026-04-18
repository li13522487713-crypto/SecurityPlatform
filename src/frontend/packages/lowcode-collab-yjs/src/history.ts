/**
 * 协同模式 IHistoryProvider 实现（M16 C16-6）。
 * 与 lowcode-editor-canvas/history 中的 LocalSliceHistoryProvider 互斥；同一时刻仅启用一种。
 *
 * 协同模式下使用 Y.UndoManager 管理历史。
 */

import * as Y from 'yjs';
import type { IHistoryProvider, HistoryEntry } from '@atlas/lowcode-editor-canvas';

export class YjsCollabHistoryProvider implements IHistoryProvider {
  private readonly um: Y.UndoManager;

  constructor(public readonly typesToTrack: ReadonlyArray<Y.AbstractType<unknown>>, captureTimeout = 500) {
    this.um = new Y.UndoManager(typesToTrack as Y.AbstractType<unknown>[], { captureTimeout });
  }

  record(entry: HistoryEntry): void {
    // Yjs 自动记录 Y.AbstractType 上的所有 update；entry 仅作为 label 标记参与协同审计；
    // 实际值不需要再 push。
    void entry;
  }

  canUndo(): boolean { return this.um.canUndo(); }
  canRedo(): boolean { return this.um.canRedo(); }

  undo(): HistoryEntry | null {
    if (!this.um.canUndo()) return null;
    this.um.undo();
    return { redoPayload: 'yjs-undo', undoPayload: 'yjs-undo', timestamp: Date.now(), label: 'yjs-undo' };
  }

  redo(): HistoryEntry | null {
    if (!this.um.canRedo()) return null;
    this.um.redo();
    return { redoPayload: 'yjs-redo', undoPayload: 'yjs-redo', timestamp: Date.now(), label: 'yjs-redo' };
  }

  clear(): void {
    this.um.clear();
  }

  getCurrentLabel(): string | undefined {
    return undefined;
  }

  size(): number {
    // Y.UndoManager 不直接暴露 size；以可 undo + redo 数量做近似。
    return (this.canUndo() ? 1 : 0) + (this.canRedo() ? 1 : 0);
  }
}
