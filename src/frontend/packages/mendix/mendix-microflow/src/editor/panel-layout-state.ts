export interface MicroflowPanelOpenState {
  leftOpen: boolean;
  rightOpen: boolean;
}

export type MicroflowPanelAction =
  | "openNodePanel"
  | "openPropertiesPanel"
  | "closeNodePanel"
  | "closePropertiesPanel"
  | "toggleNodePanel"
  | "togglePropertiesPanel";

export const MICROFLOW_PANEL_WIDTH_PX = 380;

export function reducePanelOpenState(
  state: MicroflowPanelOpenState,
  action: MicroflowPanelAction,
): MicroflowPanelOpenState {
  switch (action) {
    case "openNodePanel":
      return { leftOpen: true, rightOpen: false };
    case "openPropertiesPanel":
      return { leftOpen: false, rightOpen: true };
    case "closeNodePanel":
      return { ...state, leftOpen: false };
    case "closePropertiesPanel":
      return { ...state, rightOpen: false };
    case "toggleNodePanel":
      return state.leftOpen
        ? { ...state, leftOpen: false }
        : { leftOpen: true, rightOpen: false };
    case "togglePropertiesPanel":
      return state.rightOpen
        ? { ...state, rightOpen: false }
        : { leftOpen: false, rightOpen: true };
    default:
      return state;
  }
}

export function normalizePanelOpenState(state: MicroflowPanelOpenState): MicroflowPanelOpenState {
  if (state.leftOpen && state.rightOpen) {
    return { leftOpen: true, rightOpen: false };
  }
  return state;
}

export function resolveRightColumnWidth(options: {
  focusMode: boolean;
  auxiliaryPanelsEnabled: boolean;
  leftOpen: boolean;
  rightOpen: boolean;
  panelWidthPx?: number;
}): number {
  const width = options.panelWidthPx ?? MICROFLOW_PANEL_WIDTH_PX;
  if (options.focusMode || !options.auxiliaryPanelsEnabled) {
    return 0;
  }
  if (options.leftOpen) {
    return width;
  }
  if (options.rightOpen) {
    return width;
  }
  return 0;
}
