/**
 * 选择模型（M04 C04-4）。
 *
 * - 单选 / 多选 / 框选 / Ctrl+Shift 加选 / 跨层级选择
 * - 输出选中的 ComponentSchema.id 集合（不直接耦合 React 状态）
 */

export interface SelectionState {
  ids: ReadonlySet<string>;
}

export const EMPTY_SELECTION: SelectionState = { ids: new Set() };

export function selectOnly(id: string): SelectionState {
  return { ids: new Set([id]) };
}

export function clearSelection(): SelectionState {
  return EMPTY_SELECTION;
}

export function toggleSelect(state: SelectionState, id: string): SelectionState {
  const next = new Set(state.ids);
  if (next.has(id)) {
    next.delete(id);
  } else {
    next.add(id);
  }
  return { ids: next };
}

export function addToSelection(state: SelectionState, ids: ReadonlyArray<string>): SelectionState {
  const next = new Set(state.ids);
  for (const id of ids) next.add(id);
  return { ids: next };
}

export function removeFromSelection(state: SelectionState, ids: ReadonlyArray<string>): SelectionState {
  const next = new Set(state.ids);
  for (const id of ids) next.delete(id);
  return { ids: next };
}

export interface BoxSelectArea {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface ComponentBound {
  id: string;
  x: number;
  y: number;
  width: number;
  height: number;
}

/** 框选：返回与 area 相交的全部组件 id。*/
export function boxSelect(area: BoxSelectArea, bounds: ReadonlyArray<ComponentBound>): string[] {
  const out: string[] = [];
  const ax2 = area.x + area.width;
  const ay2 = area.y + area.height;
  for (const b of bounds) {
    const bx2 = b.x + b.width;
    const by2 = b.y + b.height;
    const intersect = !(b.x > ax2 || bx2 < area.x || b.y > ay2 || by2 < area.y);
    if (intersect) out.push(b.id);
  }
  return out;
}
