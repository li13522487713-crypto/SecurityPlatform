import { describe, expect, it } from "vitest";

import { resolveMicroflowActivation, resolveWorkbenchResourceActivation } from "./microflow-activation-guard";

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

  it("requires confirmation when a dirty microflow switches to a page resource", () => {
    expect(resolveWorkbenchResourceActivation({
      target: { kind: "page", resourceId: "page-order" },
      activeWorkbenchTabId: "microflow:mf-a",
      workbenchTabs: [
        { id: "microflow:mf-a", kind: "microflow", title: "A", microflowId: "mf-a", openedAt: "2026-01-01T00:00:00.000Z" },
        { id: "page:page-order", kind: "page", title: "Order", resourceId: "page-order", openedAt: "2026-01-01T00:00:00.000Z" },
      ],
      dirtyByWorkbenchTabId: { "microflow:mf-a": true },
    })).toEqual({ kind: "confirm-dirty", activeTabId: "microflow:mf-a" });
  });

  it("activates existing workflow/domain/security targets without confirmation when they are already active", () => {
    for (const target of [
      { kind: "workflow" as const, resourceId: "wf-a" },
      { kind: "domainModel" as const, resourceId: "mod-a" },
      { kind: "security" as const, resourceId: "mod-a" },
    ]) {
      expect(resolveWorkbenchResourceActivation({
        target,
        activeWorkbenchTabId: `${target.kind}:${target.resourceId}`,
        workbenchTabs: [
          { id: `${target.kind}:${target.resourceId}`, kind: target.kind, title: target.kind, resourceId: target.resourceId, openedAt: "2026-01-01T00:00:00.000Z" },
        ],
        dirtyByWorkbenchTabId: { [`${target.kind}:${target.resourceId}`]: true },
      })).toEqual({ kind: "activate" });
    }
  });
});
