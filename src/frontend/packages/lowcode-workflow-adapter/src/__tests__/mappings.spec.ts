import { describe, it, expect } from 'vitest';
import type { BindingSchema } from '@atlas/lowcode-schema';
import { applyOutputMapping, buildInputs, resolveBinding } from '../mappings';

describe('resolveBinding', () => {
  it('static / variable / expression', async () => {
    const scope = { app: { foo: 'bar' }, page: { count: 3 } };
    const s: BindingSchema = { sourceType: 'static', valueType: 'string', value: 'hi' };
    const v: BindingSchema = { sourceType: 'variable', valueType: 'string', path: 'app.foo', scopeRoot: 'app' };
    const e: BindingSchema = { sourceType: 'expression', valueType: 'number', expression: 'page.count * 2' };
    expect(await resolveBinding(s, scope as never)).toBe('hi');
    expect(await resolveBinding(v, scope as never)).toBe('bar');
    expect(await resolveBinding(e, scope as never)).toBe(6);
  });

  it('workflow_output 走 fallback', async () => {
    const b: BindingSchema = { sourceType: 'workflow_output', valueType: 'array', workflowId: 'wf1', fallback: [] };
    expect(await resolveBinding(b, {} as never)).toEqual([]);
  });
});

describe('buildInputs', () => {
  it('解析 inputMapping → 完整 inputs', async () => {
    const inputs = await buildInputs(
      {
        keyword: { sourceType: 'variable', valueType: 'string', path: 'page.kw', scopeRoot: 'page' },
        page: { sourceType: 'static', valueType: 'number', value: 1 }
      },
      { page: { kw: 'hello' } } as never
    );
    expect(inputs.keyword).toBe('hello');
    expect(inputs.page).toBe(1);
  });

  it('空 inputMapping → 空对象', async () => {
    expect(await buildInputs(undefined, {} as never)).toEqual({});
  });
});

describe('applyOutputMapping', () => {
  it('模式 A 黄金样本：outputs → page/app/component patches', async () => {
    const outputs = { users: [{ name: 'a' }], count: 1, html: '<p>hi</p>' };
    const patches = await applyOutputMapping(outputs as never, {
      'users': 'page.tableData',
      'count': 'app.userCount',
      'html': 'component.markdown-1.content'
    });
    expect(patches.length).toBe(3);
    const byPath = Object.fromEntries(patches.map((p) => [p.path, p]));
    expect(byPath['page.tableData'].op).toBe('set');
    expect(byPath['page.tableData'].scope).toBe('page');
    expect(byPath['app.userCount'].scope).toBe('app');
    expect(byPath['component.markdown-1.content'].scope).toBe('component');
    expect(byPath['component.markdown-1.content'].componentId).toBe('markdown-1');
  });

  it('忽略只读作用域（system / event / workflow.outputs / chatflow.outputs）', async () => {
    const patches = await applyOutputMapping({ a: 1 } as never, {
      'a': 'system.x',
      'a ': 'workflow.outputs.x',
      ' a': 'chatflow.outputs.x'
    });
    expect(patches).toEqual([]);
  });

  it('空 outputs / 空 mapping 返回空数组', async () => {
    expect(await applyOutputMapping(undefined, { 'a': 'page.x' })).toEqual([]);
    expect(await applyOutputMapping({ a: 1 } as never, undefined)).toEqual([]);
  });
});
