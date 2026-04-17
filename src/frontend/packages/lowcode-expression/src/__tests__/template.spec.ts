import { describe, it, expect } from 'vitest';
import { renderTemplate, TemplateSyntaxError } from '../template';

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
});
