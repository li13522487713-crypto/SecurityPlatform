/**
 * 自定义 Yjs SignalR Provider（M16 C16-8 + P1-6 增强）。
 *
 * 强约束（PLAN.md §M16）：不引入 Node 边车。
 * 协议：把 Yjs 的 update 字节流通过 SignalR Hub 方法 SendUpdate / ReceiveUpdate 互通。
 *
 * P1-6 增强：
 *  - awareness 跨端同步（PLAN §M16 C16-2）：通过 `awareness` Hub 方法广播 awareness 协议数据帧；
 *    使用 y-protocols/awareness 的 encodeAwarenessUpdate / applyAwarenessUpdate；
 *  - awareness 帧格式：base64(Uint8Array)，与 Y.Doc update 帧并行通道；
 *  - 远程 awareness 帧 origin 标记为 this.origin，防回声。
 */

import * as Y from 'yjs';
import {
  Awareness,
  applyAwarenessUpdate,
  encodeAwarenessUpdate,
  removeAwarenessStates
} from 'y-protocols/awareness';

export interface SignalRConnectionLike {
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke(methodName: string, ...args: unknown[]): Promise<unknown>;
  on(methodName: string, handler: (...args: unknown[]) => void): void;
  off(methodName: string, handler?: (...args: unknown[]) => void): void;
}

export interface CollabSignalRProviderOptions {
  appId: string;
  /** 用户 id，用于服务端审计与 awareness 标识。*/
  userId: string;
  /** 可选：协同显示名（光标气泡 / 头像）。*/
  displayName?: string;
}

/**
 * 用法：
 *   const conn = new HubConnectionBuilder().withUrl('/hubs/lowcode-collab').build();
 *   const provider = new YjsSignalRProvider(doc, conn, { appId, userId });
 *   await provider.connect();
 *   provider.awareness.setLocalStateField('cursor', { x, y });
 */
export class YjsSignalRProvider {
  private updateHandler?: (...args: unknown[]) => void;
  private awarenessHandler?: (...args: unknown[]) => void;
  private localUpdateHandler?: (update: Uint8Array, origin: unknown) => void;
  private localAwarenessHandler?: (changes: { added: number[]; updated: number[]; removed: number[] }, origin: unknown) => void;
  public readonly origin = Symbol('YjsSignalRProvider');
  /** Awareness 实例：所有客户端的 cursor / selection / 用户信息共享状态机。*/
  public readonly awareness: Awareness;

  constructor(
    public readonly doc: Y.Doc,
    public readonly connection: SignalRConnectionLike,
    public readonly opts: CollabSignalRProviderOptions
  ) {
    this.awareness = new Awareness(doc);
    // 默认本地 user 信息
    this.awareness.setLocalStateField('user', {
      id: opts.userId,
      name: opts.displayName ?? opts.userId
    });
  }

  async connect(): Promise<void> {
    await this.connection.start();
    await this.connection.invoke('JoinApp', this.opts.appId);

    // 监听服务端广播：base64 update → applyUpdate
    this.updateHandler = (...args: unknown[]) => {
      const payload = args[0] as { from: string; update: string };
      if (!payload || typeof payload.update !== 'string') return;
      const bytes = base64ToBytes(payload.update);
      Y.applyUpdate(this.doc, bytes, this.origin);
    };
    this.connection.on('yjsUpdate', this.updateHandler);

    // P1-6：监听 awareness 协议帧
    this.awarenessHandler = (...args: unknown[]) => {
      const payload = args[0] as { from: string; awareness: string };
      if (!payload || typeof payload.awareness !== 'string') return;
      const bytes = base64ToBytes(payload.awareness);
      applyAwarenessUpdate(this.awareness, bytes, this.origin);
    };
    this.connection.on('awareness', this.awarenessHandler);

    // 本地 doc 更新 → 广播
    this.localUpdateHandler = (update: Uint8Array, origin: unknown) => {
      if (origin === this.origin) return;
      const b64 = bytesToBase64(update);
      void this.connection.invoke('SendUpdate', this.opts.appId, this.opts.userId, b64);
    };
    this.doc.on('update', this.localUpdateHandler);

    // 本地 awareness 变更 → 广播（含本地 client added/updated/removed）
    this.localAwarenessHandler = (changes, origin) => {
      if (origin === this.origin) return;
      const changedClients = [...changes.added, ...changes.updated, ...changes.removed];
      const update = encodeAwarenessUpdate(this.awareness, changedClients);
      const b64 = bytesToBase64(update);
      void this.connection.invoke('SendAwareness', this.opts.appId, this.opts.userId, b64);
    };
    this.awareness.on('update', this.localAwarenessHandler);
  }

  async disconnect(): Promise<void> {
    if (this.updateHandler) this.connection.off('yjsUpdate', this.updateHandler);
    if (this.awarenessHandler) this.connection.off('awareness', this.awarenessHandler);
    if (this.localUpdateHandler) this.doc.off('update', this.localUpdateHandler);
    if (this.localAwarenessHandler) this.awareness.off('update', this.localAwarenessHandler);
    // 离开前主动清理本地 awareness state（让其它客户端立即知晓"对方下线"）
    try {
      removeAwarenessStates(this.awareness, [this.doc.clientID], this.origin);
    } catch {
      // 测试环境下 awareness 可能未启动；忽略
    }
    try { await this.connection.invoke('LeaveApp', this.opts.appId); } catch { /* noop */ }
    await this.connection.stop();
  }
}

function bytesToBase64(bytes: Uint8Array): string {
  let bin = '';
  for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
  return typeof btoa === 'function' ? btoa(bin) : Buffer.from(bin, 'binary').toString('base64');
}

function base64ToBytes(b64: string): Uint8Array {
  const bin = typeof atob === 'function' ? atob(b64) : Buffer.from(b64, 'base64').toString('binary');
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-collab-yjs/signalr-provider' as const;
