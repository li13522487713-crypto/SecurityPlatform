import type { MicroflowEditorState } from "../types";

export function createDefaultEditorState(): MicroflowEditorState {
  return {
    viewport: { x: 0, y: 0, zoom: 1 },
    zoom: 1,
    activeBottomPanel: "problems",
    leftPanelCollapsed: false,
    rightPanelCollapsed: false,
    bottomPanelCollapsed: false,
    showMiniMap: true,
    gridEnabled: true,
    selection: {},
    layoutMode: "freeform"
  };
}
