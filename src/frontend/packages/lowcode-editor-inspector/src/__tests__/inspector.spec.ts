import { describe, it, expect } from 'vitest';
import type { ComponentSchema } from '@atlas/lowcode-schema';
import { INSPECTOR_TABS, appendActionToEvent, removeActionAt, moveActionAt, setActionResilience } from '..';

describe('inspector', () => {
  it('INSPECTOR_TABS 三 Tab', () => {
    expect(INSPECTOR_TABS).toEqual(['property', 'style', 'events']);
  });

  it('appendActionToEvent 创建事件并追加', () => {
    const c: ComponentSchema = { id: 'btn', type: 'Button' };
    const c2 = appendActionToEvent(c, 'onClick', { kind: 'navigate', to: '/home' });
    expect(c2.events?.[0].actions.length).toBe(1);
  });

  it('removeActionAt / moveActionAt', () => {
    let c: ComponentSchema = { id: 'btn', type: 'Button' };
    c = appendActionToEvent(c, 'onClick', { kind: 'navigate', to: '/a' });
    c = appendActionToEvent(c, 'onClick', { kind: 'navigate', to: '/b' });
    c = moveActionAt(c, 'onClick', 0, 1);
    expect((c.events![0].actions[0] as { to: string }).to).toBe('/b');
    c = removeActionAt(c, 'onClick', 0);
    expect(c.events![0].actions.length).toBe(1);
  });

  it('setActionResilience 设置 / 清除', () => {
    let c: ComponentSchema = { id: 'btn', type: 'Button' };
    c = appendActionToEvent(c, 'onClick', { kind: 'call_workflow', workflowId: 'wf1' });
    c = setActionResilience(c, 'onClick', 0, { timeoutMs: 5000 });
    expect(c.events![0].actions[0].resilience?.timeoutMs).toBe(5000);
    c = setActionResilience(c, 'onClick', 0, undefined);
    expect(c.events![0].actions[0].resilience).toBeUndefined();
  });
});
