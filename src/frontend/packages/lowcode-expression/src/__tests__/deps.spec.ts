import { describe, it, expect } from 'vitest';
import { extractDeps, ReverseDependencyIndex } from '../deps';

describe('extractDeps', () => {
  it('识别 7 作用域路径', () => {
    const expr =
      'page.formValues.name & " " & app.currentUser.email & " " & system.tenantId & " " & component.btn.value & " " & event.payload.id & " " & workflow.outputs.wf1.users[0].name & " " & chatflow.outputs.cf1.tokens.length';
    const deps = extractDeps(expr);
    const scopes = new Set(deps.map((d) => d.scope));
    expect(scopes.has('page')).toBe(true);
    expect(scopes.has('app')).toBe(true);
    expect(scopes.has('system')).toBe(true);
    expect(scopes.has('component')).toBe(true);
    expect(scopes.has('event')).toBe(true);
    expect(scopes.has('workflow.outputs')).toBe(true);
    expect(scopes.has('chatflow.outputs')).toBe(true);
  });

  it('去重相同 path', () => {
    const deps = extractDeps('app.x + app.x + page.y');
    expect(deps.length).toBe(2);
  });
});

describe('ReverseDependencyIndex', () => {
  it('upsert / reverseLookup', () => {
    const idx = new ReverseDependencyIndex();
    idx.upsertBinding('b1', extractDeps('app.foo + page.bar'));
    idx.upsertBinding('b2', extractDeps('app.foo'));
    expect(idx.reverseLookup('app.foo').sort()).toEqual(['b1', 'b2']);
    expect(idx.reverseLookup('page.bar')).toEqual(['b1']);
    expect(idx.reverseLookup('not.exists')).toEqual([]);
  });

  it('removeBinding 清理反向桶', () => {
    const idx = new ReverseDependencyIndex();
    idx.upsertBinding('b1', extractDeps('app.foo'));
    idx.removeBinding('b1');
    expect(idx.reverseLookup('app.foo')).toEqual([]);
    expect(idx.size()).toBe(0);
  });

  it('upsert 替换旧依赖（无残留）', () => {
    const idx = new ReverseDependencyIndex();
    idx.upsertBinding('b1', extractDeps('app.foo'));
    idx.upsertBinding('b1', extractDeps('app.bar'));
    expect(idx.reverseLookup('app.foo')).toEqual([]);
    expect(idx.reverseLookup('app.bar')).toEqual(['b1']);
  });

  it('reverseLookupByPrefix 命中前缀', () => {
    const idx = new ReverseDependencyIndex();
    idx.upsertBinding('b1', extractDeps('page.user.name'));
    idx.upsertBinding('b2', extractDeps('page.user.email'));
    idx.upsertBinding('b3', extractDeps('app.unrelated'));
    const r = idx.reverseLookupByPrefix('page.user').sort();
    expect(r).toEqual(['b1', 'b2']);
  });
});
