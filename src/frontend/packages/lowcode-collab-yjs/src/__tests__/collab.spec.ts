import { describe, it, expect } from 'vitest';
import * as Y from 'yjs';
import { LowCodeCollabDoc } from '../doc';
import { CollabLockManager } from '../lock';
import { YjsCollabHistoryProvider } from '../history';

describe('LowCodeCollabDoc', () => {
  it('fromJson + toJson 双向同构', () => {
    const d = new LowCodeCollabDoc({ appId: 'a', initialJson: { name: 'demo', pages: [{ id: 'home' }, { id: 'about' }], theme: { primary: '#1677ff' } } });
    expect(d.toJson()).toEqual({ name: 'demo', pages: [{ id: 'home' }, { id: 'about' }], theme: { primary: '#1677ff' } });
    d.destroy();
  });

  it('双 doc 通过 update 同步', () => {
    const a = new LowCodeCollabDoc({ appId: 'a' });
    const b = new LowCodeCollabDoc({ appId: 'a' });
    a.root.set('count', 1);
    Y.applyUpdate(b.doc, Y.encodeStateAsUpdate(a.doc));
    expect(b.toJson().count).toBe(1);
    a.destroy(); b.destroy();
  });
});

describe('CollabLockManager', () => {
  it('acquire / isLocked / release', () => {
    const a = new Y.Doc();
    const b = new Y.Doc();
    const lockA = new CollabLockManager(a);
    const lockB = new CollabLockManager(b);

    expect(lockA.acquire('btn-1')).toBe(true);
    // 同步状态到 b
    Y.applyUpdate(b, Y.encodeStateAsUpdate(a));
    expect(lockB.isLocked('btn-1')).toBe(true);
    expect(lockB.acquire('btn-1')).toBe(false);

    lockA.release('btn-1');
    Y.applyUpdate(b, Y.encodeStateAsUpdate(a));
    expect(lockB.isLocked('btn-1')).toBe(false);
    expect(lockB.acquire('btn-1')).toBe(true);
  });
});

describe('YjsCollabHistoryProvider', () => {
  it('undo / redo / clear', () => {
    const doc = new Y.Doc();
    const m = doc.getMap('root');
    const provider = new YjsCollabHistoryProvider([m], 50);

    m.set('count', 1);
    // 等同一捕获窗口
    expect(provider.canUndo()).toBe(true);
    provider.undo();
    expect(m.get('count')).toBeUndefined();
    expect(provider.canRedo()).toBe(true);
    provider.redo();
    expect(m.get('count')).toBe(1);
    provider.clear();
    expect(provider.canUndo()).toBe(false);
  });
});
