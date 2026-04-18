/**
 * 自定义 Yjs SignalR Provider（M16 C16-8）。
 *
 * 强约束（PLAN.md §M16）：不引入 Node 边车。
 * 协议：把 Yjs 的 update 字节流通过 SignalR Hub 方法 SendUpdate / ReceiveUpdate 互通。
 *
 * Provider 不强依赖 @microsoft/signalr 类型；调用方传入 connection 实例即可。
 */

import * as Y from 'yjs';

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
}

/**
 * 用法：
 *   const conn = new HubConnectionBuilder().withUrl('/hubs/lowcode-collab').build();
 *   const provider = new YjsSignalRProvider(doc, conn, { appId, userId });
 *   await provider.connect();
 */
export class YjsSignalRProvider {
  private updateHandler?: (...args: unknown[]) => void;
  private localUpdateHandler?: (update: Uint8Array, origin: unknown) => void;
  public readonly origin = Symbol('YjsSignalRProvider');

  constructor(
    public readonly doc: Y.Doc,
    public readonly connection: SignalRConnectionLike,
    public readonly opts: CollabSignalRProviderOptions
  ) {}

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

    // 本地更新：广播给服务端
    this.localUpdateHandler = (update: Uint8Array, origin: unknown) => {
      if (origin === this.origin) return; // 不回传远程已 apply 的 update
      const b64 = bytesToBase64(update);
      void this.connection.invoke('SendUpdate', this.opts.appId, this.opts.userId, b64);
    };
    this.doc.on('update', this.localUpdateHandler);
  }

  async disconnect(): Promise<void> {
    if (this.updateHandler) this.connection.off('yjsUpdate', this.updateHandler);
    if (this.localUpdateHandler) this.doc.off('update', this.localUpdateHandler);
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
