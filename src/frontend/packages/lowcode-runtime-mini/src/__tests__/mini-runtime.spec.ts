import { describe, it, expect } from 'vitest';
import type { ComponentSchema } from '@atlas/lowcode-schema';
import { findUnsupportedComponents, pickRendererForMini } from '..';

const tree: ComponentSchema = {
  id: 'root',
  type: 'Container',
  children: [
    { id: 'btn', type: 'Button' },
    { id: 'editor', type: 'CodeEditor' },
    {
      id: 'sub',
      type: 'Container',
      children: [{ id: 'chart', type: 'Chart' }]
    }
  ]
};

describe('mini runtime', () => {
  it('CodeEditor 在 mini-wx 不支持', () => {
    const r = pickRendererForMini({ id: 'e', type: 'CodeEditor' }, 'mini-wx');
    expect(r.supported).toBe(false);
    expect(r.fallbackText).toContain('CodeEditor');
  });

  it('Chart 仅在 h5 支持', () => {
    expect(pickRendererForMini({ id: 'c', type: 'Chart' }, 'h5').supported).toBe(true);
    expect(pickRendererForMini({ id: 'c', type: 'Chart' }, 'mini-wx').supported).toBe(false);
  });

  it('findUnsupportedComponents 报告全树不支持节点', () => {
    const list = findUnsupportedComponents(tree, 'mini-wx');
    expect(list).toContain('editor');
    expect(list).toContain('chart');
    expect(list).not.toContain('btn');
  });

  it('h5 端 chart 支持', () => {
    expect(findUnsupportedComponents(tree, 'h5')).toEqual(['editor']);
  });
});
