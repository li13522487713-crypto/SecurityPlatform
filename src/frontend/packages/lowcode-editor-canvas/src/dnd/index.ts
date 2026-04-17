/**
 * dnd-kit 集成抽象（M04 C04-1）。
 *
 * 仅暴露"无 React 副作用"的工具函数 + 类型；React Provider/Hooks 由 M05 / M07 接入。
 *
 * 包含：
 *  - DnDDescriptor：单个可拖拽节点的描述
 *  - canDrop：父子约束检查
 *  - reorderInParent / moveAcrossParent：组件树操作（基于 immer）
 *
 * 实际 useDraggable / useDroppable / DragOverlay 由 lowcode-studio-web (M07) 在 React 层装配。
 */

import { produce } from 'immer';
import type { ComponentMeta, ComponentSchema } from '@atlas/lowcode-schema';

export interface DnDDescriptor {
  /** 组件实例 ID（树中唯一）。*/
  id: string;
  /** 父组件实例 ID（顶层为 null）。*/
  parentId: string | null;
  /** 在父组件 children 中的索引位置。*/
  index: number;
  /** 组件类型。*/
  type: string;
}

/** 检查目标父组件是否允许接受某子类型（按 ComponentMeta.childPolicy）。*/
export function canDrop(parentMeta: ComponentMeta | undefined, childType: string): boolean {
  if (!parentMeta) return false;
  if (parentMeta.childPolicy.arity === 'none') return false;
  if (parentMeta.childPolicy.allowTypes && !parentMeta.childPolicy.allowTypes.includes(childType)) return false;
  return true;
}

/** 从组件树中按 id 查找节点 + 父引用。*/
export function findNode(root: ComponentSchema, id: string): { node: ComponentSchema; parent: ComponentSchema | null; index: number } | null {
  if (root.id === id) return { node: root, parent: null, index: -1 };
  const stack: Array<{ parent: ComponentSchema; node: ComponentSchema; index: number }> = [];
  if (root.children) {
    for (let i = 0; i < root.children.length; i++) {
      stack.push({ parent: root, node: root.children[i], index: i });
    }
  }
  while (stack.length > 0) {
    const cur = stack.pop()!;
    if (cur.node.id === id) return { node: cur.node, parent: cur.parent, index: cur.index };
    if (cur.node.children) {
      for (let i = 0; i < cur.node.children.length; i++) {
        stack.push({ parent: cur.node, node: cur.node.children[i], index: i });
      }
    }
  }
  return null;
}

/** 在同一父组件内重新排序。*/
export function reorderInParent(root: ComponentSchema, parentId: string, fromIndex: number, toIndex: number): ComponentSchema {
  return produce(root, (draft) => {
    const target = parentId === draft.id ? draft : findInDraft(draft, parentId);
    if (!target || !target.children) return;
    const [moved] = target.children.splice(fromIndex, 1);
    if (moved) target.children.splice(toIndex, 0, moved);
  });
}

/** 跨父组件移动节点。*/
export function moveAcrossParent(root: ComponentSchema, fromParentId: string, fromIndex: number, toParentId: string, toIndex: number): ComponentSchema {
  return produce(root, (draft) => {
    const fromParent = fromParentId === draft.id ? draft : findInDraft(draft, fromParentId);
    const toParent = toParentId === draft.id ? draft : findInDraft(draft, toParentId);
    if (!fromParent?.children || !toParent) return;
    const [moved] = fromParent.children.splice(fromIndex, 1);
    if (!moved) return;
    if (!toParent.children) toParent.children = [];
    toParent.children.splice(toIndex, 0, moved);
  });
}

function findInDraft(node: ComponentSchema, id: string): ComponentSchema | null {
  if (node.id === id) return node;
  if (!node.children) return null;
  for (const c of node.children) {
    const r = findInDraft(c, id);
    if (r) return r;
  }
  return null;
}
