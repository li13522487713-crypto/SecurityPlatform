/**
 * 组件级操作锁（M16 C16-3）。
 *
 * - 同一组件同一时刻仅允许一人编辑属性；
 * - 锁超时自动释放（默认 30s）。
 *
 * 实现：在 Y.Map 中维护 componentId → { ownerClientId, acquiredAt, expiresAt }。
 */

import * as Y from 'yjs';

export interface LockEntry {
  ownerClientId: number;
  acquiredAt: number;
  expiresAt: number;
}

export class CollabLockManager {
  private readonly map: Y.Map<LockEntry>;

  constructor(public readonly doc: Y.Doc, mapName = 'componentLocks') {
    this.map = doc.getMap(mapName);
  }

  acquire(componentId: string, ttlMs = 30_000): boolean {
    const now = Date.now();
    const cur = this.map.get(componentId);
    if (cur && cur.expiresAt > now && cur.ownerClientId !== this.doc.clientID) {
      return false;
    }
    this.map.set(componentId, {
      ownerClientId: this.doc.clientID,
      acquiredAt: now,
      expiresAt: now + ttlMs
    });
    return true;
  }

  renew(componentId: string, ttlMs = 30_000): boolean {
    const cur = this.map.get(componentId);
    if (!cur || cur.ownerClientId !== this.doc.clientID) return false;
    this.map.set(componentId, { ...cur, expiresAt: Date.now() + ttlMs });
    return true;
  }

  release(componentId: string): void {
    const cur = this.map.get(componentId);
    if (cur && cur.ownerClientId === this.doc.clientID) {
      this.map.delete(componentId);
    }
  }

  isLocked(componentId: string): boolean {
    const cur = this.map.get(componentId);
    if (!cur) return false;
    if (cur.expiresAt <= Date.now()) return false;
    return cur.ownerClientId !== this.doc.clientID;
  }

  getOwner(componentId: string): number | undefined {
    const cur = this.map.get(componentId);
    if (!cur) return undefined;
    if (cur.expiresAt <= Date.now()) return undefined;
    return cur.ownerClientId;
  }
}
