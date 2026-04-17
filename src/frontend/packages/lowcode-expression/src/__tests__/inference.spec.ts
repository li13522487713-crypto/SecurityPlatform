import { describe, it, expect } from 'vitest';
import { lintExpression, locatePosition, buildCompletionList, inferLiteralType, type ExpressionIndex } from '../inference';

describe('lintExpression', () => {
  it('合法表达式返回 null', () => {
    expect(lintExpression('a + b')).toBeNull();
  });

  it('非法表达式返回错误', () => {
    const err = lintExpression('a + (');
    expect(err).not.toBeNull();
    expect(err?.message).toMatch(/.+/);
    expect(err?.line).toBeGreaterThanOrEqual(1);
  });
});

describe('locatePosition', () => {
  it('行列定位准确', () => {
    expect(locatePosition('abc\ndef', 0)).toEqual({ line: 1, column: 1 });
    expect(locatePosition('abc\ndef', 3)).toEqual({ line: 1, column: 4 });
    expect(locatePosition('abc\ndef', 4)).toEqual({ line: 2, column: 1 });
  });
});

describe('buildCompletionList', () => {
  it('合并 4 类候选', () => {
    const idx: ExpressionIndex = {
      variables: [{ scope: 'app', code: 'foo', valueType: 'string' }],
      workflows: [{ id: 'wf1', outputPaths: [{ path: 'users', valueType: 'array' }] }],
      chatflows: [{ id: 'cf1', outputPaths: [{ path: 'tokens', valueType: 'number' }] }],
      components: [{ id: 'btn', valueType: 'string' }]
    };
    const list = buildCompletionList(idx);
    const labels = list.map((c) => c.label);
    expect(labels).toContain('app.foo');
    expect(labels).toContain('workflow.outputs.wf1.users');
    expect(labels).toContain('chatflow.outputs.cf1.tokens');
    expect(labels).toContain('component.btn.value');
  });
});

describe('inferLiteralType', () => {
  it('推断 9 类', () => {
    expect(inferLiteralType('hi')).toBe('string');
    expect(inferLiteralType(1)).toBe('number');
    expect(inferLiteralType(true)).toBe('boolean');
    expect(inferLiteralType(new Date())).toBe('date');
    expect(inferLiteralType([])).toBe('array');
    expect(inferLiteralType({})).toBe('object');
    expect(inferLiteralType(null)).toBe('any');
    expect(inferLiteralType(undefined)).toBe('any');
  });
});
