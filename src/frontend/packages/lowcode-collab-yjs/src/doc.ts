/**
 * Yjs CRDT 文档（M16 C16-1）。
 *
 * 适配 AppSchema 嵌套结构：使用 Y.Map 作为应用根，pages 为 Y.Array，每页 root 组件为嵌套 Y.Map。
 * 真实业务的 schema ↔ Y.Doc 同步由 Studio 在 M16 接入时绑定（lowcode-editor-canvas 的 IHistoryProvider）。
 */

import * as Y from 'yjs';

export interface CollabDocOptions {
  appId: string;
  initialJson?: Record<string, unknown>;
}

export class LowCodeCollabDoc {
  public readonly doc: Y.Doc;
  public readonly root: Y.Map<unknown>;

  constructor(public readonly opts: CollabDocOptions) {
    this.doc = new Y.Doc();
    this.root = this.doc.getMap('root');
    if (opts.initialJson) {
      this.fromJson(opts.initialJson);
    }
  }

  fromJson(value: Record<string, unknown>): void {
    this.doc.transact(() => {
      this.root.clear();
      for (const [k, v] of Object.entries(value)) this.root.set(k, deepToY(v));
    });
  }

  toJson(): Record<string, unknown> {
    return mapToObject(this.root);
  }

  destroy(): void {
    this.doc.destroy();
  }
}

function deepToY(value: unknown): unknown {
  if (Array.isArray(value)) {
    const arr = new Y.Array<unknown>();
    arr.push(value.map((v) => deepToY(v)));
    return arr;
  }
  if (value !== null && typeof value === 'object') {
    const m = new Y.Map<unknown>();
    for (const [k, v] of Object.entries(value)) m.set(k, deepToY(v));
    return m;
  }
  return value;
}

function mapToObject(m: Y.Map<unknown>): Record<string, unknown> {
  const o: Record<string, unknown> = {};
  for (const [k, v] of m.entries()) {
    o[k] = yToValue(v);
  }
  return o;
}

function yToValue(value: unknown): unknown {
  if (value instanceof Y.Map) return mapToObject(value as Y.Map<unknown>);
  if (value instanceof Y.Array) return (value as Y.Array<unknown>).toArray().map(yToValue);
  return value;
}
