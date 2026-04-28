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

export interface DispatchActionLite {
  kind: string;
  id?: string;
  payload?: unknown;
}

export interface DispatchInstanceRequest {
  pageId?: string;
  componentId?: string;
  eventName?: string;
  inputs?: Record<string, JsonValue>;
  actions: ReadonlyArray<DispatchActionLite>;
}

export interface DispatchInstanceResponse {
  traceId: string;
  outputs?: Record<string, JsonValue>;
  statePatches?: ReadonlyArray<RuntimeStatePatch>;
  messages?: ReadonlyArray<{ kind: string; text: string }>;
  errors?: ReadonlyArray<{ kind: string; message: string }>;
}

export interface MountInstance {
  unmount(): void;
  update(patches: ReadonlyArray<RuntimeStatePatch>): void;
  getState(): { page: Record<string, JsonValue>; app: Record<string, JsonValue>; component: Record<string, Record<string, JsonValue>> };
  /**
   * 主动触发 dispatch（M13 唯一桥梁）。
   * 内部 POST /api/runtime/events/dispatch；返回的 statePatches 自动 apply 到本地 state。
   */
  dispatch(req: DispatchInstanceRequest, signal?: AbortSignal): Promise<DispatchInstanceResponse>;
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

  const baseUrl = (opts.baseUrl ?? '').replace(/\/+$/, '');

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
    async dispatch(req, signal) {
      const res = await fetch(`${baseUrl}/api/runtime/events/dispatch`, {
        method: 'POST',
        signal,
        headers: {
          'Content-Type': 'application/json',
          'X-Tenant-Id': opts.tenantId,
          Authorization: opts.token ? `Bearer ${opts.token}` : ''
        },
        body: JSON.stringify({
          appId: opts.appId,
          versionId: opts.version,
          pageId: req.pageId,
          componentId: req.componentId,
          eventName: req.eventName,
          inputs: req.inputs,
          actions: req.actions
        })
      });
      if (!res.ok) {
        const text = await res.text().catch(() => '');
        throw new Error(`AtlasLowcode dispatch ${res.status}: ${text}`);
      }
      const json = (await res.json()) as { success: boolean; data: DispatchInstanceResponse; code?: string; message?: string };
      if (!json.success) throw new Error(`${json.code ?? 'DISPATCH_ERROR'}: ${json.message ?? ''}`);
      const data = json.data;
      if (data.statePatches && data.statePatches.length > 0) {
        state = applyPatches(state, data.statePatches);
        opts.onEvent?.({ type: 'state', payload: state });
      }
      opts.onEvent?.({ type: 'dispatch', payload: data });
      return data;
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
  // P2-1 修复（PLAN §M17 C17-1）：
  // 此前 SDK 拉的是 /api/v1/lowcode/apps/{id}/draft（设计态草稿）—— 嵌入页面仅应消费"已发布运行时 schema"。
  // 现修正为：当 appId 为 numeric long → 走运行时 /api/runtime/apps/{id}/schema 或 /versions/{ver}/schema；
  // 当 appId 非 numeric（设计期开发 / preview demo）→ 回退到设计态 draft 并发出 console.warn。
  // version 优先：若调用方提供 version（且为 numeric） → /versions/{ver}/schema。
  const numericAppId = /^\d+$/.test(opts.appId) ? opts.appId : null;
  const numericVersion = opts.version && /^\d+$/.test(opts.version) ? opts.version : null;
  let url: string;
  let isRuntime: boolean;
  if (numericAppId && numericVersion) {
    url = `${baseUrl}/api/runtime/apps/${numericAppId}/versions/${numericVersion}/schema`;
    isRuntime = true;
  } else if (numericAppId) {
    url = `${baseUrl}/api/runtime/apps/${numericAppId}/schema`;
    isRuntime = true;
  } else {
    url = `${baseUrl}/api/v1/lowcode/apps/${encodeURIComponent(opts.appId)}/draft`;
    isRuntime = false;
    if (typeof console !== 'undefined') {
      console.warn(`[AtlasLowcode] appId="${opts.appId}" 非 numeric long，已回退到设计态 draft；生产环境请使用已发布版本的 numeric appId。`);
    }
  }

  try {
    const res = await fetch(url, {
      headers: {
        'X-Tenant-Id': opts.tenantId,
        Authorization: opts.token ? `Bearer ${opts.token}` : ''
      }
    });
    if (!res.ok) throw new Error(`fetch ${url} ${res.status}`);
    const json = (await res.json()) as {
      // 运行时 schema 直接是 AppSchemaSnapshotDto（含 pages）；设计态 draft 含 schemaJson 字段
      data?: { schemaJson?: string; pages?: SdkPageNode[]; appId?: string; code?: string };
    };
    let app: SdkAppNode;
    if (isRuntime && json.data && Array.isArray(json.data.pages)) {
      // 运行时 schema 路径：snapshot 已是结构化对象
      app = {
        appId: json.data.appId,
        code: json.data.code,
        pages: json.data.pages
      };
    } else if (json.data?.schemaJson) {
      // 设计态 draft 路径：schemaJson 是序列化字符串
      app = JSON.parse(json.data.schemaJson) as SdkAppNode;
    } else {
      throw new Error('empty schema response');
    }
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
    opts.onEvent?.({ type: 'rendered', payload: { appId: opts.appId, pageCode: page.code, source: isRuntime ? 'runtime' : 'design-draft' } });
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

type SdkState = {
  page: Record<string, JsonValue>;
  app: Record<string, JsonValue>;
  component: Record<string, Record<string, JsonValue>>;
};

/**
 * 应用 RuntimeStatePatch 数组到 SDK 状态。
 *
 * 完整支持 set / merge / unset，并支持 dot-path 与 [index] 数组路径
 * （a.list[0].title 与 a.list.0.title 等价）。与 lowcode-action-runtime/state-patch
 * 保持语义一致；SDK 不依赖 action-runtime 是为了控制 UMD bundle 体积。
 */
function applyPatches(state: SdkState, patches: ReadonlyArray<RuntimeStatePatch>): SdkState {
  // 浅克隆顶层 + 三个作用域，避免外部引用看到中途中间态
  const next: SdkState = {
    page: { ...state.page },
    app: { ...state.app },
    component: { ...state.component }
  };
  for (const p of patches) {
    const segs = parseSegments(p.path);
    if (segs.length < 2) continue;
    const scopeSeg = segs[0]!;
    if (scopeSeg.kind !== 'key') continue;
    const scope = scopeSeg.value;
    if (scope !== 'page' && scope !== 'app' && scope !== 'component') continue;

    const remaining = segs.slice(1);
    let root: Record<string, unknown>;
    if (scope === 'component') {
      // component 子树：第二段为组件 id
      if (remaining.length < 1 || remaining[0]!.kind !== 'key') continue;
      const compId = remaining[0]!.value;
      next.component[compId] = { ...(next.component[compId] ?? {}) };
      root = next.component[compId] as Record<string, unknown>;
      walkAndApply(root, remaining.slice(1), p);
    } else {
      root = next[scope] as Record<string, unknown>;
      walkAndApply(root, remaining, p);
    }
  }
  return next;
}

type Seg = { kind: 'key'; value: string } | { kind: 'index'; value: number };

function parseSegments(path: string): Seg[] {
  if (!path) return [];
  const normalized = path.replace(/\[(\d+)\]/g, '.$1');
  const out: Seg[] = [];
  for (const raw of normalized.split('.')) {
    if (raw.length === 0) continue;
    if (/^\d+$/.test(raw)) out.push({ kind: 'index', value: Number(raw) });
    else out.push({ kind: 'key', value: raw });
  }
  return out;
}

function walkAndApply(root: unknown, segs: Seg[], p: RuntimeStatePatch): void {
  if (segs.length === 0) return;
  let cur: unknown = root;
  for (let i = 0; i < segs.length - 1; i++) {
    const seg = segs[i]!;
    const nextSeg = segs[i + 1]!;
    let child = readAt(cur, seg);
    if (child === undefined || child === null || typeof child !== 'object') {
      child = nextSeg.kind === 'index' ? [] : {};
      writeAt(cur, seg, child);
    }
    cur = child;
  }
  const last = segs[segs.length - 1]!;
  switch (p.op) {
    case 'set':
      writeAt(cur, last, p.value as unknown);
      break;
    case 'merge': {
      const existing = readAt(cur, last);
      if (
        existing && typeof existing === 'object' && !Array.isArray(existing)
        && p.value && typeof p.value === 'object' && !Array.isArray(p.value)
      ) {
        writeAt(cur, last, { ...(existing as object), ...(p.value as object) });
      } else {
        writeAt(cur, last, p.value as unknown);
      }
      break;
    }
    case 'unset':
      deleteAt(cur, last);
      break;
  }
}

function readAt(parent: unknown, seg: Seg): unknown {
  if (seg.kind === 'index') return Array.isArray(parent) ? (parent as unknown[])[seg.value] : undefined;
  return (parent as Record<string, unknown>)[seg.value];
}
function writeAt(parent: unknown, seg: Seg, value: unknown): void {
  if (seg.kind === 'index') {
    if (Array.isArray(parent)) (parent as unknown[])[seg.value] = value;
    return;
  }
  (parent as Record<string, unknown>)[seg.value] = value;
}
function deleteAt(parent: unknown, seg: Seg): void {
  if (seg.kind === 'index') {
    if (Array.isArray(parent)) (parent as unknown[]).splice(seg.value, 1);
    return;
  }
  delete (parent as Record<string, unknown>)[seg.value];
}

/** 注入到 window：兼容 <script> 嵌入。*/
export function installToWindow(): void {
  if (typeof window === 'undefined') return;
  (window as unknown as { AtlasLowcode?: { mount: typeof mount } }).AtlasLowcode = { mount };
}

export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-web-sdk' as const;
