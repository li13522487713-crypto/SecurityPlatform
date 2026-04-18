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
});
