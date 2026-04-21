/**
 * Studio 选择上下文 store（M07 C07-2 / C07-8）。
 *
 * - selectedComponentId：当前选中组件实例 ID（与 ComponentSchema.id 对齐）
 * - currentPageCode：当前编辑的页面 code（PageSchema.code）
 *
 * 与 lowcode-runtime-web 的运行时 store 严格隔离：本 store 仅存设计期 UI 状态，
 * 不持有任何 runtime variable / dispatch / SSE 状态。
 */
import { create } from 'zustand';

export interface StudioSelectionState {
  selectedComponentId: string | null;
  currentPageCode: string | null;
  setSelectedComponentId: (id: string | null) => void;
  setCurrentPageCode: (code: string | null) => void;
}

export const useStudioSelection = create<StudioSelectionState>((set) => ({
  selectedComponentId: null,
  currentPageCode: null,
  setSelectedComponentId: (id) => set({ selectedComponentId: id }),
  setCurrentPageCode: (code) => set({ currentPageCode: code })
}));
