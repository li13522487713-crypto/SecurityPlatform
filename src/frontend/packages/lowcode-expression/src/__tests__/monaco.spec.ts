import { describe, it, expect } from 'vitest';
import { computeDiagnostics, computeCompletionItems, computeHover, computeWriteWarnings } from '../monaco';
import type { ExpressionIndex } from '../inference';

const idx: ExpressionIndex = {
  variables: [{ scope: 'app', code: 'foo', valueType: 'string', description: '描述 foo' }],
  workflows: [{ id: 'wf1', outputPaths: [{ path: 'users', valueType: 'array' }] }],
  chatflows: [{ id: 'cf1', outputPaths: [{ path: 'tokens', valueType: 'number' }] }],
  components: [{ id: 'btn', valueType: 'string' }]
};

describe('Monaco LSP adapter', () => {
  it('合法表达式无 diagnostics', () => {
    expect(computeDiagnostics('app.foo')).toEqual([]);
  });

  it('非法表达式产生 marker', () => {
    const m = computeDiagnostics('a + (');
    expect(m.length).toBe(1);
    expect(m[0].severity).toBe('error');
  });

  it('completion items 含全部分类', () => {
    const items = computeCompletionItems(idx);
    expect(items.find((i) => i.label === 'app.foo')).toBeDefined();
    expect(items.find((i) => i.label.startsWith('workflow.outputs.wf1'))).toBeDefined();
    expect(items.find((i) => i.label.startsWith('chatflow.outputs.cf1'))).toBeDefined();
    expect(items.find((i) => i.label.startsWith('component.btn'))).toBeDefined();
  });

  it('hover 命中变量 / 工作流 / 对话流 / 组件', () => {
    expect(computeHover('app.foo', idx)?.contents.some((s) => s.includes('foo'))).toBe(true);
    expect(computeHover('workflow.outputs.wf1.users', idx)).not.toBeNull();
    expect(computeHover('chatflow.outputs.cf1.tokens', idx)).not.toBeNull();
    expect(computeHover('component.btn.value', idx)).not.toBeNull();
    expect(computeHover('unknown.x', idx)).toBeNull();
  });

  it('疑似写入语法 + 只读作用域 → warning', () => {
    const w = computeWriteWarnings('system.x := 1');
    expect(w.length).toBe(1);
    expect(w[0].severity).toBe('warning');
  });

  it('合法表达式无 write warnings', () => {
    expect(computeWriteWarnings('app.foo + 1')).toEqual([]);
  });
});
