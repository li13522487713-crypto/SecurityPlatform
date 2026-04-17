import { describe, it, expect } from 'vitest';
import { compile, evaluate, __clearCompileCacheForTesting } from '../jsonata';

describe('jsonata wrapper', () => {
  it('编译并求值', async () => {
    const r = await evaluate('a + b', { a: 1, b: 2 });
    expect(r).toBe(3);
  });

  it('compile 缓存命中', () => {
    __clearCompileCacheForTesting();
    const c1 = compile('1 + 2');
    const c2 = compile('1 + 2');
    expect(c1).toBe(c2);
  });

  it('支持复杂查询表达式', async () => {
    const r = await evaluate('users[role="admin"].name', {
      users: [
        { name: 'a', role: 'admin' },
        { name: 'b', role: 'user' }
      ]
    });
    expect(r).toBe('a');
  });
});
