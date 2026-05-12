import { describe, expect, it } from "vitest";

import {
  MICROFLOW_PANEL_WIDTH_PX,
  normalizePanelOpenState,
  reducePanelOpenState,
  resolveRightColumnWidth,
} from "./panel-layout-state";

describe("panel layout state", () => {
  it("keeps node and property panels mutually exclusive", () => {
    expect(normalizePanelOpenState({ leftOpen: true, rightOpen: true })).toEqual({ leftOpen: true, rightOpen: false });
    expect(normalizePanelOpenState({ leftOpen: false, rightOpen: true })).toEqual({ leftOpen: false, rightOpen: true });
  });

  it("reduces open/close/toggle actions as expected", () => {
    expect(reducePanelOpenState({ leftOpen: false, rightOpen: false }, "openNodePanel")).toEqual({ leftOpen: true, rightOpen: false });
    expect(reducePanelOpenState({ leftOpen: true, rightOpen: false }, "openPropertiesPanel")).toEqual({ leftOpen: false, rightOpen: true });
    expect(reducePanelOpenState({ leftOpen: true, rightOpen: false }, "closeNodePanel")).toEqual({ leftOpen: false, rightOpen: false });
    expect(reducePanelOpenState({ leftOpen: false, rightOpen: true }, "closePropertiesPanel")).toEqual({ leftOpen: false, rightOpen: false });
    expect(reducePanelOpenState({ leftOpen: false, rightOpen: false }, "toggleNodePanel")).toEqual({ leftOpen: true, rightOpen: false });
    expect(reducePanelOpenState({ leftOpen: true, rightOpen: false }, "toggleNodePanel")).toEqual({ leftOpen: false, rightOpen: false });
    expect(reducePanelOpenState({ leftOpen: false, rightOpen: false }, "togglePropertiesPanel")).toEqual({ leftOpen: false, rightOpen: true });
    expect(reducePanelOpenState({ leftOpen: false, rightOpen: true }, "togglePropertiesPanel")).toEqual({ leftOpen: false, rightOpen: false });
  });

  it("computes right column width by focus mode and panel state", () => {
    expect(resolveRightColumnWidth({
      focusMode: true,
      auxiliaryPanelsEnabled: true,
      leftOpen: true,
      rightOpen: false,
    })).toBe(0);
    expect(resolveRightColumnWidth({
      focusMode: false,
      auxiliaryPanelsEnabled: false,
      leftOpen: false,
      rightOpen: true,
    })).toBe(0);
    expect(resolveRightColumnWidth({
      focusMode: false,
      auxiliaryPanelsEnabled: true,
      leftOpen: true,
      rightOpen: false,
    })).toBe(MICROFLOW_PANEL_WIDTH_PX);
    expect(resolveRightColumnWidth({
      focusMode: false,
      auxiliaryPanelsEnabled: true,
      leftOpen: false,
      rightOpen: true,
    })).toBe(MICROFLOW_PANEL_WIDTH_PX);
    expect(resolveRightColumnWidth({
      focusMode: false,
      auxiliaryPanelsEnabled: true,
      leftOpen: false,
      rightOpen: false,
    })).toBe(0);
  });
});
