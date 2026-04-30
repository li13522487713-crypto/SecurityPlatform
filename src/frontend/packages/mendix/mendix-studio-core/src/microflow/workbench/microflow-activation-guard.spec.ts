import { describe, expect, it } from "vitest";

import { resolveMicroflowActivation } from "./microflow-activation-guard";

describe("resolveMicroflowActivation", () => {
  it("activates immediately when the target tab is already active", () => {
    expect(resolveMicroflowActivation({
      targetMicroflowId: "mf-a",
      activeWorkbenchTabId: "microflow:mf-a",
      workbenchTabs: [
        { id: "microflow:mf-a", kind: "microflow", title: "A", microflowId: "mf-a", openedAt: "2026-01-01T00:00:00.000Z" },
      ],
      dirtyByWorkbenchTabId: { "microflow:mf-a": true },
    })).toEqual({ kind: "activate" });
  });

  it("requires confirmation when switching away from a dirty tab", () => {
    expect(resolveMicroflowActivation({
      targetMicroflowId: "mf-b",
      activeWorkbenchTabId: "microflow:mf-a",
      workbenchTabs: [
        { id: "microflow:mf-a", kind: "microflow", title: "A", microflowId: "mf-a", openedAt: "2026-01-01T00:00:00.000Z" },
        { id: "microflow:mf-b", kind: "microflow", title: "B", microflowId: "mf-b", openedAt: "2026-01-01T00:00:00.000Z" },
      ],
      dirtyByWorkbenchTabId: { "microflow:mf-a": true },
    })).toEqual({ kind: "confirm-dirty", activeTabId: "microflow:mf-a" });
  });

  it("activates immediately when current tab is clean", () => {
    expect(resolveMicroflowActivation({
      targetMicroflowId: "mf-b",
      activeWorkbenchTabId: "microflow:mf-a",
      workbenchTabs: [
        { id: "microflow:mf-a", kind: "microflow", title: "A", microflowId: "mf-a", openedAt: "2026-01-01T00:00:00.000Z" },
      ],
      dirtyByWorkbenchTabId: {},
    })).toEqual({ kind: "activate" });
  });
});
