import { describe, it, expect, vi } from 'vitest';
import { installToWindow, mount } from '..';

describe('AtlasLowcode SDK', () => {
  it('mount + update + getState + unmount 完整生命周期', () => {
    document.body.innerHTML = '<div id="host"></div>';
    const events: Array<{ type: string; payload: unknown }> = [];
    const inst = mount({
      container: '#host',
      appId: 'demo',
      tenantId: '00000000-0000-0000-0000-000000000001',
      initialState: { page: { count: 0 }, app: {} },
      onEvent: (e) => events.push(e)
    });
    expect(events.find((e) => e.type === 'mounted')).toBeDefined();
    expect(inst.getState().page.count).toBe(0);
    inst.update([{ scope: 'page', path: 'page.count', op: 'set', value: 5 }]);
    expect(inst.getState().page.count).toBe(5);
    inst.update([{ scope: 'component', componentId: 'btn-1', path: 'component.btn-1.disabled', op: 'set', value: true }]);
    expect(inst.getState().component['btn-1'].disabled).toBe(true);
    inst.unmount();
    expect(document.body.querySelector('#host')!.children.length).toBe(0);
  });

  it('重复 mount 抛错', () => {
    document.body.innerHTML = '<div id="host2"></div>';
    mount({ container: '#host2', appId: 'a', tenantId: 't' });
    expect(() => mount({ container: '#host2', appId: 'b', tenantId: 't' })).toThrow();
  });

  it('selector 未命中抛错', () => {
    expect(() => mount({ container: '#not-exist', appId: 'a', tenantId: 't' })).toThrow();
  });

  it('installToWindow 注入 window.AtlasLowcode', () => {
    installToWindow();
    expect((window as unknown as { AtlasLowcode?: unknown }).AtlasLowcode).toBeDefined();
  });

  it('支持 merge 操作 + 嵌套对象', () => {
    document.body.innerHTML = '<div id="host3"></div>';
    const inst = mount({
      container: '#host3',
      appId: 'a',
      tenantId: 't',
      initialState: { page: { user: { name: 'a', age: 1 } }, app: {} }
    });
    inst.update([{ scope: 'page', path: 'page.user', op: 'merge', value: { age: 2, email: 'x@y' } }]);
    expect(inst.getState().page.user).toEqual({ name: 'a', age: 2, email: 'x@y' });
  });

  it('支持 unset 操作', () => {
    document.body.innerHTML = '<div id="host4"></div>';
    const inst = mount({
      container: '#host4',
      appId: 'a',
      tenantId: 't',
      initialState: { page: { foo: 'x', bar: 'y' }, app: {} }
    });
    inst.update([{ scope: 'page', path: 'page.foo', op: 'unset' }]);
    expect(inst.getState().page.foo).toBeUndefined();
    expect(inst.getState().page.bar).toBe('y');
  });

  it('支持 [index] 数组路径与中间路径自动创建', () => {
    document.body.innerHTML = '<div id="host5"></div>';
    const inst = mount({
      container: '#host5',
      appId: 'a',
      tenantId: 't',
      initialState: { page: { items: [{ title: 'a' }, { title: 'b' }] }, app: {} }
    });
    inst.update([{ scope: 'page', path: 'page.items[0].title', op: 'set', value: 'A' }]);
    expect((inst.getState().page.items as unknown as Array<{ title: string }>)[0]!.title).toBe('A');

    // 中间路径自动建：原 page.deep 不存在
    inst.update([{ scope: 'page', path: 'page.deep.list[0]', op: 'set', value: 'first' }]);
    expect((inst.getState().page.deep as unknown as { list: string[] }).list[0]).toBe('first');
  });

  it('dispatch：成功时自动 apply statePatches 到 state + 触发 onEvent', async () => {
    document.body.innerHTML = '<div id="dispatchHost"></div>';
    const events: Array<{ type: string; payload: unknown }> = [];
    const inst = mount({
      container: '#dispatchHost',
      appId: 'demo',
      tenantId: 't',
      onEvent: (e) => events.push(e)
    });
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        success: true,
        data: {
          traceId: 'tr-1',
          outputs: { ok: 1 },
          statePatches: [{ scope: 'page', path: 'page.x', op: 'set', value: 42 }]
        }
      })
    });
    const restore = (globalThis as { fetch: typeof fetch }).fetch;
    (globalThis as { fetch: typeof fetch }).fetch = fetchMock as unknown as typeof fetch;
    try {
      const r = await inst.dispatch({ eventName: 'click', actions: [{ kind: 'set_variable' }] });
      expect(r.traceId).toBe('tr-1');
      expect(inst.getState().page.x).toBe(42);
      expect(events.find((e) => e.type === 'dispatch')).toBeDefined();
    } finally {
      (globalThis as { fetch: typeof fetch }).fetch = restore;
    }
  });
});
