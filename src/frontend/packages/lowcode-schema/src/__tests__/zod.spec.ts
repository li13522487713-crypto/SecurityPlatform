import { describe, expect, it } from 'vitest';
import {
  AppSchemaZod,
  ActionSchemaZod,
  BindingSchemaZod,
  ContentParamSchemaZod,
  ComponentSchemaZod,
  EventSchemaZod,
  PageSchemaZod,
  RuntimeStatePatchZod,
  RuntimeTraceZod,
  VariableSchemaZod
} from '../zod';
import {
  isCallWorkflowAction,
  isExpressionBinding,
  isImageContentParam,
  isReadonlyScope,
  isWritableScope,
  inferScopeRoot,
  ScopeViolationError,
  assertWritable
} from '../guards';
import { CURRENT_SCHEMA_VERSION, upgradeSchema, registerMigrationStep, __resetMigrationStepsForTesting } from '../migrate';

describe('BindingSchemaZod', () => {
  it('应接受 5 种 sourceType', () => {
    const cases = [
      { sourceType: 'static', valueType: 'string', value: 'hi' },
      { sourceType: 'variable', valueType: 'string', path: 'page.foo', scopeRoot: 'page' },
      { sourceType: 'expression', valueType: 'number', expression: '1+1' },
      { sourceType: 'workflow_output', valueType: 'array', workflowId: 'wf1' },
      { sourceType: 'chatflow_output', valueType: 'string', chatflowId: 'cf1' }
    ];
    for (const c of cases) {
      expect(BindingSchemaZod.safeParse(c).success).toBe(true);
    }
  });

  it('未知 sourceType 应失败并返回精确路径', () => {
    const r = BindingSchemaZod.safeParse({ sourceType: 'evil', valueType: 'string' });
    expect(r.success).toBe(false);
  });

  it('expression 缺失 expression 字段应失败', () => {
    const r = BindingSchemaZod.safeParse({ sourceType: 'expression', valueType: 'string' });
    expect(r.success).toBe(false);
  });
});

describe('ContentParamSchemaZod', () => {
  it('6 类内容参数全部接受', () => {
    expect(ContentParamSchemaZod.safeParse({ kind: 'text', code: 't1', mode: 'static', source: 'hello' }).success).toBe(true);
    expect(ContentParamSchemaZod.safeParse({ kind: 'image', code: 'i1', mode: 'url', source: 'https://x' }).success).toBe(true);
    expect(
      ContentParamSchemaZod.safeParse({
        kind: 'data',
        code: 'd1',
        source: { sourceType: 'static', valueType: 'array', value: [] }
      }).success
    ).toBe(true);
    expect(ContentParamSchemaZod.safeParse({ kind: 'link', code: 'l1', linkType: 'internal', href: '/home' }).success).toBe(true);
    expect(ContentParamSchemaZod.safeParse({ kind: 'media', code: 'm1', mediaType: 'video', url: 'https://x.mp4' }).success).toBe(true);
    expect(ContentParamSchemaZod.safeParse({ kind: 'ai', code: 'a1', mode: 'chatflow_stream', chatflowId: 'cf1' }).success).toBe(true);
  });
});

describe('ActionSchemaZod', () => {
  it('7 类内置动作均通过', () => {
    const make = <T extends Record<string, unknown>>(extra: T) => ({ ...extra });
    const cases = [
      make({
        kind: 'set_variable',
        targetPath: 'page.x',
        scopeRoot: 'page',
        value: { sourceType: 'static', valueType: 'string', value: 'a' }
      }),
      make({ kind: 'call_workflow', workflowId: 'wf1' }),
      make({ kind: 'call_chatflow', chatflowId: 'cf1', streamTarget: 'aichat-1' }),
      make({ kind: 'navigate', to: '/home' }),
      make({ kind: 'open_external_link', url: 'https://example.com' }),
      make({
        kind: 'show_toast',
        message: { sourceType: 'static', valueType: 'string', value: 'hi' }
      }),
      make({
        kind: 'update_component',
        componentId: 'btn-1',
        patchProps: { disabled: { sourceType: 'static', valueType: 'boolean', value: true } }
      })
    ];
    for (const c of cases) {
      const r = ActionSchemaZod.safeParse(c);
      if (!r.success) {
        // eslint-disable-next-line no-console
        console.error(JSON.stringify(r.error.format(), null, 2), c);
      }
      expect(r.success).toBe(true);
    }
  });

  it('set_variable 写入只读 scopeRoot 应失败', () => {
    const r = ActionSchemaZod.safeParse({
      kind: 'set_variable',
      targetPath: 'system.x',
      scopeRoot: 'system',
      value: { sourceType: 'static', valueType: 'string', value: 'a' }
    });
    expect(r.success).toBe(false);
  });

  it('open_external_link 非法 URL 应失败', () => {
    const r = ActionSchemaZod.safeParse({ kind: 'open_external_link', url: 'not-a-url' });
    expect(r.success).toBe(false);
  });
});

describe('EventSchemaZod & ComponentSchemaZod', () => {
  it('事件 → 动作链可嵌套 onError', () => {
    const r = EventSchemaZod.safeParse({
      name: 'onClick',
      actions: [
        {
          kind: 'call_workflow',
          workflowId: 'wf1',
          onError: [{ kind: 'show_toast', message: { sourceType: 'static', valueType: 'string', value: 'failed' } }]
        }
      ]
    });
    expect(r.success).toBe(true);
  });

  it('组件 schema 支持递归 children + slots', () => {
    const r = ComponentSchemaZod.safeParse({
      id: 'root',
      type: 'Container',
      children: [{ id: 'btn', type: 'Button' }],
      slots: { footer: [{ id: 'tip', type: 'Text' }] }
    });
    expect(r.success).toBe(true);
  });
});

describe('PageSchemaZod / AppSchemaZod', () => {
  it('完整 AppSchema 解析通过', () => {
    const r = AppSchemaZod.safeParse({
      schemaVersion: 'v1',
      appId: 'demo',
      code: 'demo',
      displayName: 'Demo',
      targetTypes: ['web'],
      defaultLocale: 'zh-CN',
      pages: [
        {
          id: 'home',
          code: 'home',
          displayName: '主页',
          path: '/home',
          targetType: 'web',
          layout: 'free',
          root: { id: 'root', type: 'Container' }
        }
      ]
    });
    if (!r.success) {
      // eslint-disable-next-line no-console
      console.error(JSON.stringify(r.error.format(), null, 2));
    }
    expect(r.success).toBe(true);
  });

  it('页面 path 不以 / 开头应失败', () => {
    const r = PageSchemaZod.safeParse({
      id: 'p1',
      code: 'p1',
      displayName: 'p1',
      path: 'home',
      targetType: 'web',
      layout: 'free',
      root: { id: 'r', type: 'C' }
    });
    expect(r.success).toBe(false);
  });

  it('AppSchema targetTypes 必须含至少 1 项', () => {
    const r = AppSchemaZod.safeParse({
      schemaVersion: 'v1',
      appId: 'd',
      code: 'd',
      displayName: 'd',
      targetTypes: [],
      defaultLocale: 'zh-CN',
      pages: []
    });
    expect(r.success).toBe(false);
  });
});

describe('VariableSchemaZod', () => {
  it('9 种 valueType 均通过', () => {
    const types = ['string', 'number', 'boolean', 'date', 'array', 'object', 'file', 'image', 'any'] as const;
    for (const t of types) {
      const r = VariableSchemaZod.safeParse({ code: 'v', displayName: 'v', scope: 'app', valueType: t });
      expect(r.success).toBe(true);
    }
  });
});

describe('RuntimeStatePatchZod / RuntimeTraceZod', () => {
  it('补丁 op 限定 set/merge/unset', () => {
    expect(RuntimeStatePatchZod.safeParse({ scope: 'page', path: 'page.x', op: 'set', value: 1 }).success).toBe(true);
    expect(RuntimeStatePatchZod.safeParse({ scope: 'page', path: 'page.x', op: 'unknown' }).success).toBe(false);
  });

  it('trace span 接受最小集', () => {
    const r = RuntimeTraceZod.safeParse({
      traceId: 't1',
      appId: 'a1',
      spans: [{ spanId: 's1', name: 'dispatcher.start', status: 'ok', startedAt: '2026-04-17T00:00:00Z' }],
      startedAt: '2026-04-17T00:00:00Z',
      status: 'success'
    });
    expect(r.success).toBe(true);
  });
});

describe('Guards', () => {
  it('Binding/ContentParam/Action 守卫缩窄类型', () => {
    expect(isExpressionBinding({ sourceType: 'expression', valueType: 'string', expression: '1' })).toBe(true);
    expect(isImageContentParam({ kind: 'image', code: 'i', mode: 'url', source: 'x' })).toBe(true);
    expect(isCallWorkflowAction({ kind: 'call_workflow', workflowId: 'w' })).toBe(true);
  });

  it('作用域守卫识别可写/只读', () => {
    expect(isWritableScope('page')).toBe(true);
    expect(isWritableScope('app')).toBe(true);
    expect(isWritableScope('system')).toBe(false);
    expect(isReadonlyScope('component')).toBe(true);
    expect(isReadonlyScope('event')).toBe(true);
    expect(isReadonlyScope('workflow.outputs')).toBe(true);
    expect(isReadonlyScope('chatflow.outputs')).toBe(true);
  });

  it('inferScopeRoot 识别完整作用域', () => {
    expect(inferScopeRoot('page.foo')).toBe('page');
    expect(inferScopeRoot('app.x')).toBe('app');
    expect(inferScopeRoot('system.tenantId')).toBe('system');
    expect(inferScopeRoot('component.btn-1.value')).toBe('component');
    expect(inferScopeRoot('event.payload')).toBe('event');
    expect(inferScopeRoot('workflow.outputs.x')).toBe('workflow.outputs');
    expect(inferScopeRoot('chatflow.outputs.y')).toBe('chatflow.outputs');
    expect(inferScopeRoot('unknown.x')).toBeUndefined();
  });

  it('assertWritable 拒绝只读作用域写入', () => {
    expect(() => assertWritable('page.foo')).not.toThrow();
    expect(() => assertWritable('app.bar')).not.toThrow();
    expect(() => assertWritable('system.x')).toThrow(ScopeViolationError);
    expect(() => assertWritable('component.btn-1.x')).toThrow(ScopeViolationError);
    expect(() => assertWritable('workflow.outputs.x')).toThrow(ScopeViolationError);
    expect(() => assertWritable('chatflow.outputs.x')).toThrow(ScopeViolationError);
    expect(() => assertWritable('unknown.x')).toThrow(ScopeViolationError);
  });
});

describe('Migrate', () => {
  it('当前版本无需迁移直接返回', () => {
    const out = upgradeSchema({ schemaVersion: CURRENT_SCHEMA_VERSION, foo: 1 });
    expect(out.schemaVersion).toBe(CURRENT_SCHEMA_VERSION);
    expect(out.foo).toBe(1);
  });

  it('注册迁移步骤后可升级', () => {
    __resetMigrationStepsForTesting();
    registerMigrationStep({
      fromVersion: 'v0',
      toVersion: CURRENT_SCHEMA_VERSION,
      upgrade: (input) => ({ ...input, migratedFrom: 'v0' })
    });
    const out = upgradeSchema({ schemaVersion: 'v0', x: 1 });
    expect(out.schemaVersion).toBe(CURRENT_SCHEMA_VERSION);
    expect(out.migratedFrom).toBe('v0');
    expect(out.x).toBe(1);
    __resetMigrationStepsForTesting();
  });

  it('禁止重复注册同 from→to', () => {
    __resetMigrationStepsForTesting();
    const step = { fromVersion: 'v0', toVersion: 'v1', upgrade: (i: Record<string, unknown>) => i };
    registerMigrationStep(step as never);
    expect(() => registerMigrationStep(step as never)).toThrow();
    __resetMigrationStepsForTesting();
  });
});
