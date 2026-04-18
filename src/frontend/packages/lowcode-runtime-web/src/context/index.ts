/**
 * RuntimeContext（M08 C08-3）：注入 7 适配器（workflow / chatflow / asset / session / trigger / webview-policy / plugin）+ store + dispatch 客户端。
 *
 * 设计要点：
 * - 适配器以 lazy 注入方式接入；每个适配器在 M09-M12 / M18 落地后再 attach 真实实现。
 * - 默认实现：workflow / chatflow 委托 dispatchClient 走 dispatch；其它适配器以 Noop 实现，由调用方覆盖。
 */

import type { ActionSchema, JsonValue, RuntimeStatePatch } from '@atlas/lowcode-schema';
import type { DispatchClient, DispatchRequest, DispatchResponse } from '../dispatch-client';

export interface RuntimeWorkflowAdapter {
  invoke(action: ActionSchema, ctx: { state: Record<string, JsonValue> }): Promise<{ patches?: RuntimeStatePatch[]; outputs?: Record<string, JsonValue> }>;
}

export interface RuntimeChatflowAdapter {
  stream(action: ActionSchema, ctx: { state: Record<string, JsonValue> }): AsyncIterable<{ patches?: RuntimeStatePatch[]; outputs?: Record<string, JsonValue> }>;
}

export interface RuntimeAssetAdapter {
  prepareUpload(file: File, opts?: { mime?: string }): Promise<{ token: string; uploadUrl: string }>;
  completeUpload(token: string, blobOrFile: Blob | File): Promise<{ fileHandle: string; url: string }>;
}

export interface RuntimeSessionAdapter {
  list(): Promise<Array<{ id: string; title?: string }>>;
  create(title?: string): Promise<{ id: string }>;
  switch(id: string): Promise<void>;
  clear(id: string): Promise<void>;
}

export interface RuntimeTriggerAdapter {
  upsert(trigger: { id?: string; cron?: string; event?: string }): Promise<{ id: string }>;
  list(): Promise<Array<{ id: string; cron?: string; event?: string }>>;
  delete(id: string): Promise<void>;
}

export interface RuntimeWebviewPolicyAdapter {
  isAllowed(url: string): boolean;
  addDomain(domain: string): Promise<void>;
}

export interface RuntimePluginAdapter {
  invoke(pluginId: string, args: Record<string, JsonValue>): Promise<{ outputs?: Record<string, JsonValue> }>;
}

export interface RuntimeContext {
  appId: string;
  pageId?: string;
  versionId?: string;
  dispatchClient: DispatchClient;
  workflow: RuntimeWorkflowAdapter;
  chatflow: RuntimeChatflowAdapter;
  asset: RuntimeAssetAdapter;
  session: RuntimeSessionAdapter;
  trigger: RuntimeTriggerAdapter;
  webviewPolicy: RuntimeWebviewPolicyAdapter;
  plugin: RuntimePluginAdapter;
  /** 允许调用方对 dispatch 请求做最后定制（例如附带 stateSnapshot）。*/
  buildDispatchRequest?: (action: ActionSchema) => Partial<DispatchRequest>;
}

/** 默认 workflow 适配器：把动作丢给 dispatch 客户端走"标准化协议唯一桥梁"。*/
export function createDefaultWorkflowAdapter(client: DispatchClient, ctx: { appId: string; pageId?: string; versionId?: string }): RuntimeWorkflowAdapter {
  return {
    async invoke(action) {
      const r: DispatchResponse = await client.send({
        appId: ctx.appId,
        pageId: ctx.pageId,
        versionId: ctx.versionId,
        actions: [action]
      });
      return { patches: r.statePatches, outputs: r.outputs };
    }
  };
}

/** 默认 chatflow 适配器：以单帧形式委托 dispatch（M11 接入真正 SSE 时替换）。*/
export function createDefaultChatflowAdapter(client: DispatchClient, ctx: { appId: string; pageId?: string }): RuntimeChatflowAdapter {
  return {
    async *stream(action) {
      const r = await client.send({ appId: ctx.appId, pageId: ctx.pageId, actions: [action] });
      yield { patches: r.statePatches, outputs: r.outputs };
    }
  };
}

export const NOOP_ASSET: RuntimeAssetAdapter = {
  async prepareUpload() { throw new Error('AssetAdapter 未注入（M10 落地后接入）'); },
  async completeUpload() { throw new Error('AssetAdapter 未注入（M10 落地后接入）'); }
};
export const NOOP_SESSION: RuntimeSessionAdapter = {
  async list() { return []; },
  async create() { throw new Error('SessionAdapter 未注入（M11 落地后接入）'); },
  async switch() { /* noop */ },
  async clear() { /* noop */ }
};
export const NOOP_TRIGGER: RuntimeTriggerAdapter = {
  async upsert() { throw new Error('TriggerAdapter 未注入（M12 落地后接入）'); },
  async list() { return []; },
  async delete() { /* noop */ }
};
export const NOOP_WEBVIEW: RuntimeWebviewPolicyAdapter = {
  isAllowed: () => true,
  async addDomain() { /* noop */ }
};
export const NOOP_PLUGIN: RuntimePluginAdapter = {
  async invoke() { throw new Error('PluginAdapter 未注入（M18 落地后接入）'); }
};

export function createRuntimeContext(opts: { appId: string; pageId?: string; versionId?: string; dispatchClient: DispatchClient; overrides?: Partial<Omit<RuntimeContext, 'appId' | 'pageId' | 'versionId' | 'dispatchClient'>> }): RuntimeContext {
  const base: RuntimeContext = {
    appId: opts.appId,
    pageId: opts.pageId,
    versionId: opts.versionId,
    dispatchClient: opts.dispatchClient,
    workflow: createDefaultWorkflowAdapter(opts.dispatchClient, opts),
    chatflow: createDefaultChatflowAdapter(opts.dispatchClient, opts),
    asset: NOOP_ASSET,
    session: NOOP_SESSION,
    trigger: NOOP_TRIGGER,
    webviewPolicy: NOOP_WEBVIEW,
    plugin: NOOP_PLUGIN
  };
  return { ...base, ...opts.overrides };
}
