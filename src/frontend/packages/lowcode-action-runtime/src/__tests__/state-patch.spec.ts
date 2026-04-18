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

  it('支持 [index] 数组路径 set', () => {
    const init = { page: { list: [{ title: 'a' }, { title: 'b' }] } };
    const r = commitPatches(init as never, [{ scope: 'page', path: 'page.list[1].title', op: 'set', value: 'B' }]);
    expect(readPath(r.next, 'page.list[1].title')).toBe('B');
    expect(readPath(r.next, 'page.list[0].title')).toBe('a');
  });

  it('支持 page.list.0.title 数字段语法', () => {
    const init = { page: { list: [{ title: 'a' }, { title: 'b' }] } };
    const r = commitPatches(init as never, [{ scope: 'page', path: 'page.list.0.title', op: 'set', value: 'A' }]);
    expect(readPath(r.next, 'page.list[0].title')).toBe('A');
  });

  it('在数组上 unset 等价于 splice', () => {
    const init = { page: { list: ['a', 'b', 'c'] } };
    const r = commitPatches(init as never, [{ scope: 'page', path: 'page.list[1]', op: 'unset' }]);
    expect(readPath(r.next, 'page.list')).toEqual(['a', 'c']);
  });

  it('数组 merge：非对象时退化为 set', () => {
    const init = { page: { tags: ['x', 'y'] } };
    const r = commitPatches(init as never, [{ scope: 'page', path: 'page.tags[0]', op: 'merge', value: 'X' }]);
    expect(readPath(r.next, 'page.tags[0]')).toBe('X');
  });

  it('数组中间路径自动建对象', () => {
    const init = { page: { list: [] as Array<{ deep?: { ok?: boolean } }> } };
    const r = commitPatches(init as never, [{ scope: 'page', path: 'page.list[0].deep.ok', op: 'set', value: true }]);
    expect(readPath(r.next, 'page.list[0].deep.ok')).toBe(true);
  });
});
