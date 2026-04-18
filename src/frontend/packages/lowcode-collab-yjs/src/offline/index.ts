/**
 * Yjs 离线 IndexedDB persistence（M16 P1-6，PLAN §M16 C16-4）。
 *
 * 通过 y-indexeddb 把 Y.Doc 持久化到浏览器 IndexedDB：
 *  - 浏览器关闭/网络断开后再打开仍能从本地恢复编辑状态；
 *  - 重连 SignalR provider 后，本地 update 自动 merge 到服务端（CRDT 自动合并）；
 *  - 冲突由 Yjs CRDT 解决，无需用户参与。
 *
 * 注：本模块对 indexeddb 不可用环境（如 SSR / node 测试）做安全降级（noop）。
 */

import * as Y from 'yjs';
import { IndexeddbPersistence } from 'y-indexeddb';

export interface OfflinePersistenceOptions {
  appId: string;
}

export interface OfflinePersistence {
  ready: Promise<void>;
  /** 销毁本地 IndexedDB 持久化（不删除已存数据，仅断开监听）。*/
  destroy(): Promise<void>;
  /** 显式删除本地存储（如用户主动"清空本地草稿"）。*/
  clear(): Promise<void>;
}

class IndexeddbPersistenceWrapper implements OfflinePersistence {
  ready: Promise<void>;
  constructor(private readonly inner: IndexeddbPersistence) {
    this.ready = (inner.whenSynced as unknown as Promise<void>) ?? Promise.resolve();
  }
  async destroy(): Promise<void> {
    await this.inner.destroy();
  }
  async clear(): Promise<void> {
    await this.inner.clearData();
  }
}

class NoopPersistence implements OfflinePersistence {
  ready = Promise.resolve();
  async destroy(): Promise<void> { /* noop */ }
  async clear(): Promise<void> { /* noop */ }
}

/**
 * 把 doc 绑定到 IndexedDB；db 名为 `atlas-lowcode:{appId}`。
 * 浏览器无 IndexedDB 时返回 NoopPersistence（不抛错）。
 */
export function bindOfflinePersistence(doc: Y.Doc, opts: OfflinePersistenceOptions): OfflinePersistence {
  if (typeof indexedDB === 'undefined') {
    return new NoopPersistence();
  }
  try {
    const db = `atlas-lowcode:${opts.appId}`;
    const inner = new IndexeddbPersistence(db, doc);
    return new IndexeddbPersistenceWrapper(inner);
  } catch {
    // IndexedDB 不可用（隐私模式 / 配额满）→ 安全降级
    return new NoopPersistence();
  }
}

export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-collab-yjs/offline' as const;
