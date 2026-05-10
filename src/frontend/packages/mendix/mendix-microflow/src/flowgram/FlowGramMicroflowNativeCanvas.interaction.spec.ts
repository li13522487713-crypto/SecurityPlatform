import { describe, expect, it } from "vitest";

import {
  shouldViewportPanFromPointerDown,
  zoomViewportForPanToolWheel,
  zoomViewportAtCanvasCenter,
} from "./flowgram-canvas-interactions";

describe("FlowGramMicroflowNativeCanvas hand-tool interactions", () => {
  const makeTarget = (matcher: (selector: string) => boolean): HTMLElement => ({
    closest: (selector: string) => (matcher(selector) ? ({ nodeName: "DIV" } as Element) : null),
  } as unknown as HTMLElement);

  it("keeps node drag priority in pan tool mode", () => {
    const target = makeTarget(selector => selector.includes("[data-microflow-object-id]"));

    const shouldPan = shouldViewportPanFromPointerDown({
      target,
      button: 0,
      panToolActive: true,
      spacePressed: false,
      draggingNode: false,
    });

    expect(shouldPan).toBe(false);
  });

  it("pans viewport on blank canvas in pan tool mode", () => {
    const blank = makeTarget(() => false);

    const shouldPan = shouldViewportPanFromPointerDown({
      target: blank,
      button: 0,
      panToolActive: true,
      spacePressed: false,
      draggingNode: false,
    });

    expect(shouldPan).toBe(true);
  });

  it("locks viewport pan while node drag is active", () => {
    const blank = makeTarget(() => false);

    const shouldPan = shouldViewportPanFromPointerDown({
      target: blank,
      button: 0,
      panToolActive: true,
      spacePressed: false,
      draggingNode: true,
    });

    expect(shouldPan).toBe(false);
  });

  it("zooms around cursor position instead of canvas center in pan tool mode", () => {
    const viewport = { x: 120, y: 80, zoom: 1 };
    const deltaY = -120;
    const nextZoom = viewport.zoom * Math.exp(-deltaY * 0.002);

    const cursorAnchored = zoomViewportForPanToolWheel(viewport, 450, 120, deltaY);
    const centerAnchored = zoomViewportAtCanvasCenter(viewport, 1000, 800, nextZoom);

    expect(cursorAnchored.zoom).toBeCloseTo(centerAnchored.zoom, 6);
    expect(cursorAnchored.x).not.toBeCloseTo(centerAnchored.x, 6);
    expect(cursorAnchored.y).not.toBeCloseTo(centerAnchored.y, 6);
  });

  it("keeps the world point under the cursor stable while zooming scroll viewport", () => {
    const viewport = { x: 120, y: 80, zoom: 1 };
    const localX = 450;
    const localY = 120;
    const before = {
      x: (localX + viewport.x) / viewport.zoom,
      y: (localY + viewport.y) / viewport.zoom,
    };

    const next = zoomViewportForPanToolWheel(viewport, localX, localY, -120);
    const after = {
      x: (localX + next.x) / next.zoom,
      y: (localY + next.y) / next.zoom,
    };

    expect(after.x).toBeCloseTo(before.x, 6);
    expect(after.y).toBeCloseTo(before.y, 6);
  });
});
