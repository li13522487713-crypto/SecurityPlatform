import { describe, it, expect } from 'vitest';
import type { ComponentSchema } from '@atlas/lowcode-schema';
import { buildOutline, setVisibility, setLocked, renameComponent, deleteComponent, searchOutline } from '..';

const tree: ComponentSchema = {
  id: 'root',
  type: 'Container',
  metadata: { displayName: '根容器' },
  children: [
    { id: 'btn-1', type: 'Button', metadata: { displayName: '主按钮' } },
    {
      id: 'sub',
      type: 'Container',
      children: [{ id: 'img-1', type: 'Image' }]
    }
  ]
};

describe('outline', () => {
  it('buildOutline 构造视图模型', () => {
    const o = buildOutline(tree);
    expect(o.label).toBe('根容器');
    expect(o.children[0].label).toBe('主按钮');
    expect(o.children[1].children[0].type).toBe('Image');
  });

  it('setVisibility / setLocked', () => {
    const t1 = setVisibility(tree, 'btn-1', false);
    expect(buildOutline(t1).children[0].visible).toBe(false);
    const t2 = setLocked(t1, 'btn-1', true);
    expect(buildOutline(t2).children[0].locked).toBe(true);
  });

  it('renameComponent 更新 displayName', () => {
    const t = renameComponent(tree, 'btn-1', '改名按钮');
    expect(buildOutline(t).children[0].label).toBe('改名按钮');
  });

  it('deleteComponent 移除节点', () => {
    const t = deleteComponent(tree, 'btn-1');
    expect(buildOutline(t).children.length).toBe(1);
  });

  it('searchOutline 命中含祖先链', () => {
    const o = buildOutline(tree);
    const hits = searchOutline(o, 'Image');
    expect(hits.has('img-1')).toBe(true);
    expect(hits.has('sub')).toBe(true); // 祖先
    expect(hits.has('root')).toBe(true);
    expect(hits.has('btn-1')).toBe(false);
  });
});
