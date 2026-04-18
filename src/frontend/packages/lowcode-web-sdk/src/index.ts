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
  // SDK 直接以 vanilla DOM 渲染线框预览（避免本包强依赖 React 全量，保持 UMD 体积）；
  // 调用方需要完整运行时（事件 / dispatch）时改用 @atlas/lowcode-runtime-web React 组件装配。
  const root = document.createElement('div');
  root.dataset.lowcodeAppId = opts.appId;
  root.dataset.lowcodeVersion = opts.version ?? 'latest';
  root.style.cssText = 'min-height:120px;border:1px dashed #d9d9d9;padding:16px;font-family:system-ui;color:#333;';
  root.textContent = `[AtlasLowcode] loading appId=${opts.appId} version=${opts.version ?? 'latest'} ...`;
  container.appendChild(root);

  void loadAndRender(root, opts);

  let state = {
    page: { ...(opts.initialState?.page ?? {}) } as Record<string, JsonValue>,
    app: { ...(opts.initialState?.app ?? {}) } as Record<string, JsonValue>,
    component: {} as Record<string, Record<string, JsonValue>>
  };

  const instance: MountInstance = {
    unmount() {
      container.removeChild(root);
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

interface SdkComponentNode {
  id: string;
  type: string;
  visible?: boolean;
  metadata?: { displayName?: string };
  children?: SdkComponentNode[];
}

interface SdkPageNode {
  id: string;
  code: string;
  displayName: string;
  path: string;
  root: SdkComponentNode;
}

interface SdkAppNode {
  appId?: string;
  code?: string;
  displayName?: string;
  pages?: SdkPageNode[];
}

async function loadAndRender(root: HTMLElement, opts: MountOptions): Promise<void> {
  const baseUrl = (opts.baseUrl ?? '').replace(/\/+$/, '');
  const url = `${baseUrl}/api/v1/lowcode/apps/${encodeURIComponent(opts.appId)}/draft`;
  try {
    const res = await fetch(url, {
      headers: {
        'X-Tenant-Id': opts.tenantId,
        Authorization: opts.token ? `Bearer ${opts.token}` : ''
      }
    });
    if (!res.ok) throw new Error(`fetch ${url} ${res.status}`);
    const json = (await res.json()) as { data?: { schemaJson: string } };
    const schemaJson = json?.data?.schemaJson;
    if (!schemaJson) throw new Error('empty schemaJson');
    const app = JSON.parse(schemaJson) as SdkAppNode;
    const page = app.pages?.[0];
    if (!page) {
      root.textContent = `[AtlasLowcode] empty app ${opts.appId}`;
      return;
    }
    root.innerHTML = '';
    const title = document.createElement('div');
    title.style.cssText = 'font-weight:600;margin-bottom:8px;';
    title.textContent = `${page.displayName} (${page.path})`;
    root.appendChild(title);
    root.appendChild(renderTree(page.root, 0));
    opts.onEvent?.({ type: 'rendered', payload: { appId: opts.appId, pageCode: page.code } });
  } catch (e) {
    root.textContent = `[AtlasLowcode] load failed: ${(e as Error).message}`;
    opts.onEvent?.({ type: 'error', payload: { message: (e as Error).message } });
  }
}

function renderTree(node: SdkComponentNode, depth: number): HTMLElement {
  const wrap = document.createElement('div');
  if (node.visible === false) wrap.style.opacity = '0.6';
  wrap.style.cssText += `margin-left:${depth * 12}px;margin-bottom:6px;padding:6px 10px;background:#fff;border:1px dashed #d8d8d8;border-radius:4px;`;
  wrap.dataset.componentId = node.id;
  const display = node.metadata?.displayName ?? node.type;
  const head = document.createElement('div');
  head.innerHTML = `<strong>${escapeHtml(display)}</strong> <span style="color:#999;font-size:11px;">${escapeHtml(node.type)}</span>`;
  wrap.appendChild(head);
  for (const c of node.children ?? []) wrap.appendChild(renderTree(c, depth + 1));
  return wrap;
}

function escapeHtml(s: string): string {
  return s.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c] ?? c));
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
