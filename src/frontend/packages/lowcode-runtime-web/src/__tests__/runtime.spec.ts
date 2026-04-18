import { describe, it, expect, beforeEach } from 'vitest';
import type { ActionSchema, ComponentSchema, EventSchema, PageSchema } from '@atlas/lowcode-schema';
import { useRuntimeStore } from '../store';
import { MockDispatchClient } from '../dispatch-client';
import { createRuntimeContext } from '../context';
import { dispatchEvent } from '../events';
import { flattenPage, recordPerformanceMark, readPerformanceMarks, __resetPerformanceMarksForTesting } from '../renderer';

beforeEach(() => {
  useRuntimeStore.getState().reset();
  __resetPerformanceMarksForTesting();
});

describe('runtime store', () => {
  it('apply page patches + read', () => {
    useRuntimeStore.getState().applyPatches([{ scope: 'page', path: 'page.formValues.name', op: 'set', value: 'a' }]);
    expect(useRuntimeStore.getState().read('page.formValues.name')).toBe('a');
  });

  it('apply component patches by id', () => {
    useRuntimeStore.getState().applyPatches([{ scope: 'component', componentId: 'btn-1', path: 'component.btn-1.loading', op: 'set', value: true }]);
    expect(useRuntimeStore.getState().read('component.btn-1.loading')).toBe(true);
  });

  it('merge / unset 对应 op', () => {
    useRuntimeStore.getState().applyPatches([
      { scope: 'app', path: 'app.user', op: 'set', value: { name: 'a' } },
      { scope: 'app', path: 'app.user', op: 'merge', value: { email: 'x@y' } }
    ]);
    expect(useRuntimeStore.getState().read('app.user')).toEqual({ name: 'a', email: 'x@y' });
    useRuntimeStore.getState().applyPatches([{ scope: 'app', path: 'app.user', op: 'unset' }]);
    expect(useRuntimeStore.getState().read('app.user')).toBeUndefined();
  });
});

describe('events / dispatch', () => {
  it('dispatchEvent 经 MockDispatchClient 提交并应用 patches', async () => {
    const action: ActionSchema = { kind: 'set_variable', targetPath: 'page.x', scopeRoot: 'page', value: { sourceType: 'static', valueType: 'number', value: 1 } };
    const evt: EventSchema = { name: 'onClick', actions: [action] };
    const client = new MockDispatchClient(async () => ({
      traceId: 't1',
      statePatches: [{ scope: 'page', path: 'page.x', op: 'set', value: 1 }]
    }));
    const ctx = createRuntimeContext({ appId: 'a', dispatchClient: client });
    await dispatchEvent(evt, ctx, { componentId: 'btn-1' });
    expect(useRuntimeStore.getState().read('page.x')).toBe(1);
  });
});

describe('renderer', () => {
  it('flattenPage 递归展开', () => {
    const page: PageSchema = {
      id: 'p1', code: 'p1', displayName: '', path: '/p1', targetType: 'web', layout: 'free',
      root: {
        id: 'root', type: 'Container',
        children: [{ id: 'btn', type: 'Button' }],
        slots: { footer: [{ id: 'tip', type: 'Text' }] }
      } as ComponentSchema
    };
    const list = flattenPage(page);
    const ids = list.map((d) => d.schema.id);
    expect(ids).toEqual(['root', 'btn', 'tip']);
  });

  it('performance marks 记录 + 读取', () => {
    recordPerformanceMark({ componentId: 'a', phase: 'render', durationMs: 10, startedAt: 1 });
    expect(readPerformanceMarks().length).toBe(1);
  });
});
