import type { ReactNode } from "react";
import { create } from "zustand";

interface NodeSideSheetStore {
  isVisible: boolean;
  currentNodeKey: string | null;
  mainPanelWidth: number;
  fullscreenPanel: ReactNode | null;
  openSideSheet: (nodeKey: string, width?: number) => void;
  closeSideSheet: () => void;
  setFullscreenPanel: (panel: ReactNode | null) => void;
  setMainPanelWidth: (width: number) => void;
}

export const useNodeSideSheetStore = create<NodeSideSheetStore>((set) => ({
  isVisible: false,
  currentNodeKey: null,
  mainPanelWidth: 420,
  fullscreenPanel: null,
  openSideSheet: (nodeKey, width) =>
    set((state) => ({
      isVisible: true,
      currentNodeKey: nodeKey,
      mainPanelWidth: width ?? state.mainPanelWidth
    })),
  closeSideSheet: () =>
    set({
      isVisible: false,
      currentNodeKey: null,
      fullscreenPanel: null
    }),
  setFullscreenPanel: (fullscreenPanel) => set({ fullscreenPanel }),
  setMainPanelWidth: (mainPanelWidth) => set({ mainPanelWidth })
}));
