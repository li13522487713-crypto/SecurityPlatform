import { describe, it, expect } from 'vitest';
import { renderTemplate, TemplateSyntaxError, registerFilter } from '../template';

describe('renderTemplate', () => {
  it('插值', async () => {
    expect(await renderTemplate('hi {{ name }}', { name: 'world' })).toBe('hi world');
  });

  it('if/else', async () => {
    const tpl = '{% if x > 0 %}pos{% else %}neg{% endif %}';
    expect(await renderTemplate(tpl, { x: 1 })).toBe('pos');
    expect(await renderTemplate(tpl, { x: -1 })).toBe('neg');
  });

  it('for', async () => {
    const tpl = '{% for u in users %}{{ u.name }};{% endfor %}';
    const out = await renderTemplate(tpl, { users: [{ name: 'a' }, { name: 'b' }] });
    expect(out).toBe('a;b;');
  });

  it('未闭合 if 抛错', async () => {
    await expect(renderTemplate('{% if x %}x', { x: 1 })).rejects.toBeInstanceOf(TemplateSyntaxError);
  });

  it('for 缺少 in 抛错', async () => {
    expect(() => {
      // 同步 tokenize 阶段抛错
      // 但 renderTemplate 本身 await tokenize，于是变成 reject
      // 直接断言其错误类型
    }).not.toThrow();
    await expect(renderTemplate('{% for x %}body{% endfor %}', {})).rejects.toBeInstanceOf(TemplateSyntaxError);
  });

  it('未知指令抛错', async () => {
    await expect(renderTemplate('{% foo %}body{% endfoo %}', {})).rejects.toBeInstanceOf(TemplateSyntaxError);
  });

  it('内置 filter：upper / lower / capitalize', async () => {
    expect(await renderTemplate('{{ name | upper }}', { name: 'foo' })).toBe('FOO');
    expect(await renderTemplate('{{ name | lower }}', { name: 'BAR' })).toBe('bar');
    expect(await renderTemplate('{{ name | capitalize }}', { name: 'baz' })).toBe('Baz');
  });

  it('filter 链：trim → upper', async () => {
    expect(await renderTemplate('{{ s | trim | upper }}', { s: '  hello  ' })).toBe('HELLO');
  });

  it('filter default(value)', async () => {
    expect(await renderTemplate("{{ x | default('fallback') }}", { x: null })).toBe('fallback');
    expect(await renderTemplate("{{ x | default('fallback') }}", { x: 'real' })).toBe('real');
  });

  it('filter join(sep)', async () => {
    expect(await renderTemplate("{{ tags | join('|') }}", { tags: ['a', 'b', 'c'] })).toBe('a|b|c');
  });

  it('filter truncate(n, suffix)', async () => {
    expect(await renderTemplate("{{ s | truncate(3, '...') }}", { s: 'abcdefg' })).toBe('abc...');
  });

  it('filter json', async () => {
    expect(await renderTemplate('{{ obj | json }}', { obj: { a: 1 } })).toBe('{"a":1}');
  });

  it('未知 filter 抛错', async () => {
    await expect(renderTemplate('{{ s | unknown }}', { s: 'x' })).rejects.toBeInstanceOf(TemplateSyntaxError);
  });

  it('registerFilter 自定义', async () => {
    registerFilter('reverse', (v) => String(v ?? '').split('').reverse().join(''));
    expect(await renderTemplate('{{ s | reverse }}', { s: 'abc' })).toBe('cba');
  });

  it('for + break', async () => {
    const tpl = '{% for n in nums %}{% if n > 2 %}{% break %}{% endif %}{{ n }};{% endfor %}';
    const out = await renderTemplate(tpl, { nums: [1, 2, 3, 4, 5] });
    expect(out).toBe('1;2;');
  });

  it('for + continue', async () => {
    const tpl = '{% for n in nums %}{% if n > 2 %}{% continue %}{% endif %}{{ n }};{% endfor %}';
    const out = await renderTemplate(tpl, { nums: [1, 2, 3, 4, 5] });
    expect(out).toBe('1;2;');
  });
});
