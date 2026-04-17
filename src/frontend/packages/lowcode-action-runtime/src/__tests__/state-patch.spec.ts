import { describe, it, expect } from 'vitest';
import type { RuntimeStatePatch } from '@atlas/lowcode-schema';
import { commitPatches, parsePath, readPath } from '../state-patch';

describe('state-patch', () => {
  it('set 嵌套路径', () => {
    const r = commitPatches({}, [{ scope: 'page', path: 'page.formValues.name', op: 'set', value: 'a' }]);
    expect(readPath(r.next, 'page.formValues.name')).toBe('a');
  });

  it('merge 对象', () => {
    const init = { app: { user: { name: 'a' } } };
    const r = commitPatches(init as never, [{ scope: 'app', path: 'app.user', op: 'merge', value: { email: 'x@y' } }]);
    const u = readPath(r.next, 'app.user');
    expect(u).toEqual({ name: 'a', email: 'x@y' });
  });

  it('unset 删除', () => {
    const init = { app: { foo: 1, bar: 2 } };
    const r = commitPatches(init as never, [{ scope: 'app', path: 'app.foo', op: 'unset' }]);
    const v = readPath(r.next, 'app.foo');
    expect(v).toBeUndefined();
    expect(readPath(r.next, 'app.bar')).toBe(2);
  });

  it('合并多补丁', () => {
    const patches: RuntimeStatePatch[] = [
      { scope: 'page', path: 'page.x', op: 'set', value: 1 },
      { scope: 'page', path: 'page.y', op: 'set', value: 2 }
    ];
    const r = commitPatches({}, patches);
    expect(r.applied).toBe(2);
    expect(readPath(r.next, 'page.x')).toBe(1);
    expect(readPath(r.next, 'page.y')).toBe(2);
  });

  it('parsePath 处理边缘', () => {
    expect(parsePath('a.b.c')).toEqual(['a', 'b', 'c']);
    expect(parsePath('')).toEqual([]);
  });
});
