import { describe, it, expect } from 'vitest';
import { ScopeViolationError, classifyScope, ensureReadOnly, ensureWritablePath } from '../scope';

describe('scope guards', () => {
  it('classifyScope 识别 7 作用域', () => {
    expect(classifyScope('page.x')).toBe('page');
    expect(classifyScope('app.x')).toBe('app');
    expect(classifyScope('system.x')).toBe('system');
    expect(classifyScope('component.btn.value')).toBe('component');
    expect(classifyScope('event.payload')).toBe('event');
    expect(classifyScope('workflow.outputs.x')).toBe('workflow.outputs');
    expect(classifyScope('chatflow.outputs.x')).toBe('chatflow.outputs');
    expect(classifyScope('unknown.x')).toBeUndefined();
  });

  it('ensureWritablePath 拒绝只读作用域', () => {
    expect(() => ensureWritablePath('page.foo')).not.toThrow();
    expect(() => ensureWritablePath('app.foo')).not.toThrow();
    expect(() => ensureWritablePath('system.foo')).toThrow(ScopeViolationError);
    expect(() => ensureWritablePath('component.btn.value')).toThrow(ScopeViolationError);
    expect(() => ensureWritablePath('event.x')).toThrow(ScopeViolationError);
    expect(() => ensureWritablePath('workflow.outputs.x')).toThrow(ScopeViolationError);
    expect(() => ensureWritablePath('chatflow.outputs.x')).toThrow(ScopeViolationError);
    expect(() => ensureWritablePath('unknown.x')).toThrow(ScopeViolationError);
  });

  it('ensureReadOnly 不抛错（占位）', () => {
    expect(() => ensureReadOnly('app.x')).not.toThrow();
  });

  it('30 个跨作用域违规用例', () => {
    const violations = [
      'system.foo',
      'system.x.y',
      'component.btn',
      'component.btn.value',
      'component.list.0.value',
      'event.payload',
      'event.target',
      'workflow.outputs.wf1',
      'workflow.outputs.wf1.users',
      'workflow.outputs.wf1.users.0.name',
      'chatflow.outputs.cf1',
      'chatflow.outputs.cf1.tokens',
      'chatflow.outputs.cf1.tokens.length',
      'unknown.x',
      'foo.bar',
      'window.location',
      'document.body',
      'globalThis.x',
      'undefined.x',
      'system.user',
      'system.tenantId',
      'component.aiChat.message',
      'component.upload.file',
      'event.value',
      'event.checked',
      'event.target.value',
      'workflow.outputs.deepNested.array.0.field',
      'chatflow.outputs.toolCall.args',
      'chatflow.outputs.toolCall.result',
      'component.x.children.0.id'
    ];
    for (const p of violations) {
      expect(() => ensureWritablePath(p)).toThrow(ScopeViolationError);
    }
  });
});
