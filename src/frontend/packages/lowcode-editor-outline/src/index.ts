/**
 * @atlas/lowcode-editor-outline — 结构树（M05 C05-1）。
 *
 * 设计要点：
 * - 暴露纯函数 + 数据结构；React Tree 渲染器（基于 Semi `Tree`）由 M07 lowcode-studio-web 装配。
 * - 拖拽改父子复用 @atlas/lowcode-editor-canvas/dnd 中 reorderInParent / moveAcrossParent / canDrop。
 * - 显隐 / 锁定基于 ComponentSchema.visible / locked 字段（M01 已支持）。
 */

import { produce } from 'immer';
import type { ComponentSchema } from '@atlas/lowcode-schema';

/** 树节点视图模型（用于 Semi Tree 渲染）。*/
export interface OutlineNode {
  id: string;
  type: string;
  label: string;
  visible: boolean;
  locked: boolean;
  children: OutlineNode[];
}

export function buildOutline(root: ComponentSchema): OutlineNode {
  const label = root.metadata?.['displayName'] && typeof root.metadata['displayName'] === 'string' ? (root.metadata['displayName'] as string) : root.type;
  return {
    id: root.id,
    type: root.type,
    label,
    visible: root.visible !== false,
    locked: root.locked === true,
    children: (root.children ?? []).map(buildOutline)
  };
}

export function setVisibility(root: ComponentSchema, id: string, visible: boolean): ComponentSchema {
  return produce(root, (draft) => {
    visit(draft, id, (node) => {
      node.visible = visible;
    });
  });
}

export function setLocked(root: ComponentSchema, id: string, locked: boolean): ComponentSchema {
  return produce(root, (draft) => {
    visit(draft, id, (node) => {
      node.locked = locked;
    });
  });
}

export function renameComponent(root: ComponentSchema, id: string, displayName: string): ComponentSchema {
  return produce(root, (draft) => {
    visit(draft, id, (node) => {
      const meta = (node.metadata ?? {}) as Record<string, unknown>;
      meta.displayName = displayName;
      node.metadata = meta as ComponentSchema['metadata'];
    });
  });
}

export function deleteComponent(root: ComponentSchema, id: string): ComponentSchema {
  return produce(root, (draft) => {
    deleteVisit(draft, id);
  });
}

function visit(node: ComponentSchema, id: string, fn: (n: ComponentSchema) => void): boolean {
  if (node.id === id) {
    fn(node);
    return true;
  }
  if (!node.children) return false;
  for (const c of node.children) {
    if (visit(c, id, fn)) return true;
  }
  return false;
}

function deleteVisit(node: ComponentSchema, id: string): boolean {
  if (!node.children) return false;
  const idx = node.children.findIndex((c) => c.id === id);
  if (idx >= 0) {
    node.children.splice(idx, 1);
    return true;
  }
  for (const c of node.children) {
    if (deleteVisit(c, id)) return true;
  }
  return false;
}

/** 搜索过滤：返回匹配节点 id 集合（含其祖先链，便于 Tree 自动展开）。*/
export function searchOutline(root: OutlineNode, keyword: string): Set<string> {
  const out = new Set<string>();
  if (!keyword) return out;
  const lower = keyword.toLowerCase();
  function walk(n: OutlineNode, ancestors: string[]): void {
    const hit = n.label.toLowerCase().includes(lower) || n.type.toLowerCase().includes(lower);
    if (hit) {
      for (const a of ancestors) out.add(a);
      out.add(n.id);
    }
    for (const c of n.children) {
      walk(c, [...ancestors, n.id]);
    }
  }
  walk(root, []);
  return out;
}

export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-editor-outline' as const;
