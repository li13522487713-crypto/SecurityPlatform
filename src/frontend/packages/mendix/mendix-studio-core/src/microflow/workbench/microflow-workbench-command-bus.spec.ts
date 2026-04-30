import { describe, expect, it, vi } from "vitest";

import { MicroflowWorkbenchCommandBus } from "./microflow-workbench-command-bus";

function createHandle() {
  return {
    save: vi.fn(async () => undefined),
    validate: vi.fn(async () => undefined),
    runTest: vi.fn(async () => undefined),
    runDebug: vi.fn(async () => undefined),
    publish: vi.fn(async () => undefined),
    undo: vi.fn(),
    redo: vi.fn(),
    autoLayout: vi.fn(),
    fitView: vi.fn(),
    zoomIn: vi.fn(),
    zoomOut: vi.fn(),
    setZoom: vi.fn(),
    toggleFullscreen: vi.fn(),
    toggleFocusMode: vi.fn(),
    toggleMinimap: vi.fn(),
    resetLayout: vi.fn(),
    openBottomTab: vi.fn(),
    setBottomDockMode: vi.fn(),
    getLayoutState: vi.fn(),
    getStatus: vi.fn(),
  };
}

describe("MicroflowWorkbenchCommandBus", () => {
  it("executes editor-backed commands and records success state", async () => {
    const handle = createHandle();
    const bus = new MicroflowWorkbenchCommandBus();
    bus.bindContext({
      microflowId: "mf-1",
      tabId: "microflow:mf-1",
      getEditorHandle: () => handle as any,
    });

    await bus.execute("microflow.save");
    await bus.execute("microflow.debugRun");

    expect(handle.save).toHaveBeenCalledTimes(1);
    expect(handle.runDebug).toHaveBeenCalledTimes(1);
    expect(bus.getSnapshot().latestExecutionByCommand["microflow.debugRun"]).toMatchObject({
      microflowId: "mf-1",
      tabId: "microflow:mf-1",
      state: "success",
    });
  });

  it("routes references panel commands to the host callback", async () => {
    const handle = createHandle();
    const openReferencesPanel = vi.fn();
    const bus = new MicroflowWorkbenchCommandBus();
    bus.bindContext({
      microflowId: "mf-1",
      tabId: "microflow:mf-1",
      getEditorHandle: () => handle as any,
      openReferencesPanel,
    });

    await bus.execute("microflow.openPanel", { panel: "references" });

    expect(openReferencesPanel).toHaveBeenCalledWith("mf-1");
    expect(handle.openBottomTab).not.toHaveBeenCalled();
  });

  it("records failed state when the underlying command throws", async () => {
    const handle = createHandle();
    handle.runTest.mockRejectedValueOnce(new Error("run failed"));
    const bus = new MicroflowWorkbenchCommandBus();
    bus.bindContext({
      microflowId: "mf-1",
      tabId: "microflow:mf-1",
      getEditorHandle: () => handle as any,
    });

    await expect(bus.execute("microflow.run")).rejects.toThrow("run failed");
    expect(bus.getSnapshot().latestExecutionByCommand["microflow.run"]).toMatchObject({
      state: "failed",
      errorMessage: "run failed",
    });
  });
});
