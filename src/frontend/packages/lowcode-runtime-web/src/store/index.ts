/**
 * 运行时状态切片（M08 C08-4）。
 *
 * 三作用域：page / app / component；component 状态按 id 索引。
 * 基于 zustand + immer，与 lowcode-action-runtime 的 commitPatches 配合提交补丁。
 */

import { create } from 'zustand';
import { produce } from 'immer';
import type { JsonValue, RuntimeStatePatch } from '@atlas/lowcode-schema';
import { commitPatches } from '@atlas/lowcode-action-runtime/state-patch';

export interface RuntimeStateSlice {
  page: Record<string, JsonValue>;
  app: Record<string, JsonValue>;
  component: Record<string, Record<string, JsonValue>>;
}

export interface RuntimeStoreApi {
  state: RuntimeStateSlice;
  applyPatches: (patches: ReadonlyArray<RuntimeStatePatch>) => void;
  /** 读取任意路径（page.* / app.* / component.<id>.*）。*/
  read: (path: string) => JsonValue | undefined;
  reset: () => void;
}

const EMPTY: RuntimeStateSlice = { page: {}, app: {}, component: {} };

export const useRuntimeStore = create<RuntimeStoreApi>((set, get) => ({
  state: EMPTY,
  applyPatches: (patches) => {
    set((s) => {
      // 把 patches 按 scope 分发后再合并；commitPatches 期望平坦 JsonObject，
      // 这里给一个聚合视图供 applyPatch 使用。
      const next = produce(s.state, (draft) => {
        for (const p of patches) {
          if (p.scope === 'page') applyToScope(draft.page, stripScope(p.path, 'page'), p);
          else if (p.scope === 'app') applyToScope(draft.app, stripScope(p.path, 'app'), p);
          else if (p.scope === 'component') {
            const id = p.componentId ?? extractComponentId(p.path);
            if (!id) continue;
            if (!draft.component[id]) draft.component[id] = {};
            applyToScope(draft.component[id], stripScope(p.path, `component.${id}`), p);
          }
        }
      });
      return { state: next };
    });
    void commitPatches; // 保留 import 以让外部 adapter 使用一致 API
  },
  read: (path) => {
    const s = get().state;
    if (path.startsWith('page.')) return readPath(s.page, path.slice('page.'.length));
    if (path.startsWith('app.')) return readPath(s.app, path.slice('app.'.length));
    if (path.startsWith('component.')) {
      const rest = path.slice('component.'.length);
      const idx = rest.indexOf('.');
      const id = idx === -1 ? rest : rest.slice(0, idx);
      const sub = idx === -1 ? '' : rest.slice(idx + 1);
      const compState = s.component[id] ?? {};
      return sub ? readPath(compState, sub) : (compState as JsonValue);
    }
    return undefined;
  },
  reset: () => set({ state: EMPTY })
}));

function applyToScope(target: Record<string, JsonValue>, restPath: string, patch: RuntimeStatePatch): void {
  const segs = restPath.split('.').filter(Boolean);
  if (segs.length === 0) return;
  let cur: Record<string, unknown> = target as unknown as Record<string, unknown>;
  for (let i = 0; i < segs.length - 1; i++) {
    const seg = segs[i];
    if (typeof cur[seg] !== 'object' || cur[seg] === null) cur[seg] = {};
    cur = cur[seg] as Record<string, unknown>;
  }
  const last = segs[segs.length - 1];
  switch (patch.op) {
    case 'set':
      cur[last] = patch.value as unknown;
      break;
    case 'merge': {
      const ex = cur[last];
      if (ex && typeof ex === 'object' && !Array.isArray(ex) && patch.value && typeof patch.value === 'object' && !Array.isArray(patch.value)) {
        cur[last] = { ...(ex as object), ...(patch.value as object) };
      } else {
        cur[last] = patch.value as unknown;
      }
      break;
    }
    case 'unset':
      delete cur[last];
      break;
  }
}

function readPath(target: Record<string, JsonValue>, path: string): JsonValue | undefined {
  const segs = path.split('.').filter(Boolean);
  let cur: unknown = target;
  for (const s of segs) {
    if (cur === undefined || cur === null || typeof cur !== 'object') return undefined;
    cur = (cur as Record<string, unknown>)[s];
  }
  return cur as JsonValue | undefined;
}

function stripScope(path: string, prefix: string): string {
  if (path === prefix) return '';
  return path.startsWith(`${prefix}.`) ? path.slice(prefix.length + 1) : path;
}

function extractComponentId(path: string): string | undefined {
  if (!path.startsWith('component.')) return undefined;
  const rest = path.slice('component.'.length);
  const idx = rest.indexOf('.');
  return idx === -1 ? rest : rest.slice(0, idx);
}
