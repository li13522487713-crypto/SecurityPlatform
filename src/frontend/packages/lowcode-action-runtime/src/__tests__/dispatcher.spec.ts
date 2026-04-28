import { describe, it, expect, beforeEach } from 'vitest';
import type { ActionSchema, BindingSchema } from '@atlas/lowcode-schema';
import { installBuiltInActions, __resetInstalledForTesting } from '../dispatcher';
import { __resetActionRegistryForTesting, getActionHandler, listRegisteredKinds } from '../extend';
import { executeChain } from '../chain';
import { commitPatches } from '../state-patch';

beforeEach(() => {
  __resetActionRegistryForTesting();
  __resetInstalledForTesting();
  installBuiltInActions();
});

const staticBinding = (value: unknown): BindingSchema => ({
  sourceType: 'static',
  valueType: typeof value === 'string' ? 'string' : 'any',
  value: value as never
});

describe('dispatcher 7 内置动作', () => {
  it('listRegisteredKinds 含 7 种', () => {
    const kinds = listRegisteredKinds().sort();
    expect(kinds).toEqual(['call_chatflow', 'call_workflow', 'navigate', 'open_external_link', 'set_variable', 'show_toast', 'update_component']);
  });

  it('set_variable 写入 page', async () => {
    const handler = getActionHandler('set_variable')!;
    const r = await handler(
      {
        kind: 'set_variable',
        targetPath: 'page.foo',
        scopeRoot: 'page',
        value: staticBinding('hi')
      },
      { state: {} }
    );
    const next = commitPatches({}, r.patches!).next;
    expect((next as Record<string, Record<string, unknown>>).page.foo).toBe('hi');
  });

  it('set_variable 拒绝写入 system', async () => {
    const handler = getActionHandler('set_variable')!;
    await expect(
      handler(
        {
          kind: 'set_variable',
          targetPath: 'system.foo',
          // @ts-expect-error 设计上禁止 system 写入，强行测试触发 scope-guard
          scopeRoot: 'system',
          value: staticBinding('x')
        },
        { state: {} }
      )
    ).rejects.toThrow();
  });

  it('update_component 更新 props', async () => {
    const handler = getActionHandler('update_component')!;
    const r = await handler(
      {
        kind: 'update_component',
        componentId: 'btn-1',
        patchProps: { disabled: staticBinding(true) }
      },
      { state: {} }
    );
    const next = commitPatches({}, r.patches!).next;
    expect((next as Record<string, Record<string, Record<string, unknown>>>).component['btn-1'].disabled).toBe(true);
  });

  it('navigate 输出 navigate 指令', async () => {
    const handler = getActionHandler('navigate')!;
    const r = await handler({ kind: 'navigate', to: '/home' }, { state: {} });
    expect(r.outputs?.navigate).toEqual({ to: '/home', params: {}, replace: false });
  });

  it('show_toast 解析 binding 文本', async () => {
    const handler = getActionHandler('show_toast')!;
    const r = await handler(
      { kind: 'show_toast', message: staticBinding('hi'), toastType: 'success' },
      { state: {} }
    );
    expect(r.messages).toEqual([{ kind: 'success', text: 'hi' }]);
  });

  it('call_workflow 委托 invokeDispatch + loading patches', async () => {
    const handler = getActionHandler('call_workflow')!;
    const action: ActionSchema = {
      kind: 'call_workflow',
      workflowId: 'wf1',
      loadingTargets: ['list-1'],
      errorTargets: ['list-1']
    };
    const r = await handler(action, {
      state: {},
      invokeDispatch: async () => ({
        patches: [{ scope: 'page', path: 'page.result', op: 'set', value: 1 }]
      })
    });
    // loading on + loading off + error null + 业务 patch
    const ops = r.patches!.map((p) => p.op);
    expect(ops.length).toBeGreaterThanOrEqual(4);
  });

  it('call_workflow 失败时通过 applySideEffectPatches 挂 error patches 后重新抛错（P4-4）', async () => {
    const handler = getActionHandler('call_workflow')!;
    const sideEffectPatches: { path: string; op: string }[] = [];
    let thrown: Error | null = null;
    try {
      await handler(
        { kind: 'call_workflow', workflowId: 'wf1', loadingTargets: ['l1'], errorTargets: ['l1'] },
        {
          state: {},
          invokeDispatch: async () => {
            throw new Error('boom');
          },
          applySideEffectPatches: (patches) => {
            for (const p of patches) sideEffectPatches.push({ path: p.path, op: p.op });
          }
        }
      );
    } catch (e) {
      thrown = e as Error;
    }
    // P4-4：必须重新抛出，让 chain 的 onError 可以捕获
    expect(thrown).not.toBeNull();
    expect(thrown!.message).toBe('boom');
    // 同时验证 side effect patches 已经走 applySideEffectPatches 提交（loading off + error）
    expect(sideEffectPatches.some((p) => p.path === 'component.l1.error')).toBe(true);
    expect(sideEffectPatches.some((p) => p.path === 'component.l1.loading' && p.op === 'set')).toBe(true);
  });
});

describe('chain 编排', () => {
  it('顺序执行', async () => {
    const actions: ActionSchema[] = [
      { kind: 'set_variable', targetPath: 'page.a', scopeRoot: 'page', value: staticBinding(1) },
      { kind: 'set_variable', targetPath: 'page.b', scopeRoot: 'page', value: staticBinding(2) }
    ];
    const r = await executeChain(actions, { state: {} });
    expect(r.patches.length).toBe(2);
  });

  it('when 条件跳过', async () => {
    const actions: ActionSchema[] = [
      {
        kind: 'set_variable',
        when: 'false',
        targetPath: 'page.a',
        scopeRoot: 'page',
        value: staticBinding(1)
      }
    ];
    const r = await executeChain(actions, { state: {} });
    expect(r.patches.length).toBe(0);
  });

  it('parallel 并行批', async () => {
    const actions: ActionSchema[] = [
      { kind: 'navigate', to: '/a', parallel: true },
      { kind: 'navigate', to: '/b', parallel: true }
    ];
    const r = await executeChain(actions, { state: {} });
    expect(r.errors.length).toBe(0);
  });

  it('onError 子链兜底（P4-4 修复后：call_workflow 抛错可被 onError 捕获）', async () => {
    const actions: ActionSchema[] = [
      {
        kind: 'call_workflow',
        workflowId: 'wf-fail',
        onError: [{ kind: 'show_toast', message: staticBinding('caught') }]
      }
    ];
    const r = await executeChain(actions, {
      state: {},
      invokeDispatch: async () => {
        throw new Error('boom');
      }
    });
    // P4-4 修复后：call_workflow 失败时重新抛出异常，外层 chain 的 onError 子链能捕获并执行替代动作。
    // 期望：onError 中的 show_toast('caught') 被执行，'caught' 出现在 messages 中。
    expect(r.messages?.some((m) => m.text === 'caught')).toBe(true);
    // 同时验证：当一个非 call_* 的 action 直接 throw 时，onError 子链同样能捕获（已有行为）
    const actions2: ActionSchema[] = [
      {
        kind: 'set_variable',
        targetPath: 'system.foo',
        scopeRoot: 'page', // 故意标 page 但路径是 system → scope-guard 会抛
        value: staticBinding(1),
        onError: [{ kind: 'show_toast', message: staticBinding('caught2') }]
      }
    ];
    const r2 = await executeChain(actions2, { state: {} });
    expect(r2.messages?.some((m) => m.text === 'caught2')).toBe(true);
  });
});
