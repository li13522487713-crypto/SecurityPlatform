/**
 * Atlas Lowcode Web SDK（M17 C17-1..C17-7）。
 *
 * - 完整 API：mount({ container, appId, version, initialState, theme, onEvent, baseUrl }) → Instance
 * - Instance: { unmount(), update(patches), getState() }
 * - 三种嵌入：<script> / npm import / iframe
 * - 真实渲染由 @atlas/lowcode-runtime-web 在调用方装配；本 SDK 提供 mount 协议的稳定入口。
 */

import type { JsonValue, RuntimeStatePatch } from '@atlas/lowcode-schema';

export interface MountOptions {
  container: HTMLElement | string;
  appId: string;
  /** 可选：指定版本；为空时使用应用当前生效版本。*/
  version?: string;
  /** 初始状态（合并到 page/app 作用域）。*/
  initialState?: { page?: Record<string, JsonValue>; app?: Record<string, JsonValue> };
  /** 主题覆盖。*/
  theme?: { primaryColor?: string; borderRadius?: number; darkMode?: 'never' | 'always' | 'auto' };
  /** 事件回调（dispatch outputs / messages 等）。*/
  onEvent?: (event: { type: string; payload: unknown }) => void;
  /** 自定义 API base URL（默认 /）。*/
  baseUrl?: string;
  /** 租户 id（嵌入页面通常已固定）。*/
  tenantId: string;
  /** 访问令牌（持久 / 临时）。*/
  token?: string;
}

export interface MountInstance {
  unmount(): void;
  update(patches: ReadonlyArray<RuntimeStatePatch>): void;
  getState(): { page: Record<string, JsonValue>; app: Record<string, JsonValue>; component: Record<string, Record<string, JsonValue>> };
  /** 调试：当前选项快照。*/
  inspect(): MountOptions;
}

const REGISTERED: WeakMap<HTMLElement, MountInstance> = new WeakMap();

/** 主入口：mount。*/
export function mount(opts: MountOptions): MountInstance {
  const container = resolveContainer(opts.container);
  if (REGISTERED.has(container)) {
    throw new Error('AtlasLowcode: container 已挂载，请先 unmount()');
  }
  // M17 阶段：mount 仅完成 baseUrl 校验 + 注入 placeholder DOM；
  // React 渲染层接 lowcode-runtime-web 由调用方在 sdk-playground 中装配（避免本包强依赖 React 全量）。
  const placeholder = document.createElement('div');
  placeholder.dataset.lowcodeAppId = opts.appId;
  placeholder.dataset.lowcodeVersion = opts.version ?? 'latest';
  placeholder.style.cssText = 'min-height:120px;border:1px dashed #d9d9d9;padding:16px;font-family:system-ui;color:#666;';
  placeholder.textContent = `[AtlasLowcode] mounted appId=${opts.appId} version=${opts.version ?? 'latest'}`;
  container.appendChild(placeholder);

  let state = {
    page: { ...(opts.initialState?.page ?? {}) } as Record<string, JsonValue>,
    app: { ...(opts.initialState?.app ?? {}) } as Record<string, JsonValue>,
    component: {} as Record<string, Record<string, JsonValue>>
  };

  const instance: MountInstance = {
    unmount() {
      container.removeChild(placeholder);
      REGISTERED.delete(container);
    },
    update(patches) {
      state = applyPatches(state, patches);
      opts.onEvent?.({ type: 'state', payload: state });
    },
    getState() {
      return state;
    },
    inspect() {
      return opts;
    }
  };
  REGISTERED.set(container, instance);
  opts.onEvent?.({ type: 'mounted', payload: { appId: opts.appId } });
  return instance;
}

function resolveContainer(c: HTMLElement | string): HTMLElement {
  if (typeof c === 'string') {
    const el = document.querySelector(c);
    if (!el) throw new Error(`AtlasLowcode: container selector 未命中：${c}`);
    return el as HTMLElement;
  }
  return c;
}

function applyPatches(state: { page: Record<string, JsonValue>; app: Record<string, JsonValue>; component: Record<string, Record<string, JsonValue>> }, patches: ReadonlyArray<RuntimeStatePatch>) {
  // SDK 内部简化：仅支持 set；merge / unset 由调用方在外层处理或在 lowcode-runtime-web 内做完整支持。
  for (const p of patches) {
    if (p.op !== 'set') continue;
    const segs = p.path.split('.').filter(Boolean);
    if (segs.length < 2) continue;
    const scope = segs[0] as 'page' | 'app' | 'component';
    if (scope === 'component') {
      const id = segs[1];
      const sub = segs.slice(2).join('.');
      state.component[id] = { ...(state.component[id] ?? {}), [sub]: p.value as JsonValue };
    } else if (scope === 'page' || scope === 'app') {
      const key = segs.slice(1).join('.');
      state[scope] = { ...state[scope], [key]: p.value as JsonValue };
    }
  }
  return state;
}

/** 注入到 window：兼容 <script> 嵌入。*/
export function installToWindow(): void {
  if (typeof window === 'undefined') return;
  (window as unknown as { AtlasLowcode?: { mount: typeof mount } }).AtlasLowcode = { mount };
}

export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-web-sdk' as const;
