// @vitest-environment jsdom

import { describe, expect, it, vi } from "vitest";

import { MicroflowWorkbenchErrorBus } from "./microflow-workbench-error-bus";

describe("MicroflowWorkbenchErrorBus", () => {
  it("opens problems and surfaces validation errors", () => {
    const onUnauthorizedRedirect = vi.fn();
    const onOpenProblems = vi.fn();
    const bus = new MicroflowWorkbenchErrorBus({
      onUnauthorizedRedirect,
      onOpenProblems,
    });
    bus.attach();

    window.dispatchEvent(new CustomEvent("atlas:microflow-api-error", {
      detail: {
        code: "MICROFLOW_VALIDATION_FAILED",
        category: "validation",
        message: "validation failed",
        httpStatus: 422,
        validationIssues: [{ id: "issue-1" }],
      },
    }));

    expect(onOpenProblems).toHaveBeenCalledTimes(1);
    expect(bus.getSnapshot().activeError).toMatchObject({
      code: "MICROFLOW_VALIDATION_FAILED",
      category: "validation",
    });
  });

  it("locks the workbench into readonly mode on forbidden", () => {
    const bus = new MicroflowWorkbenchErrorBus({
      onUnauthorizedRedirect: vi.fn(),
      onOpenProblems: vi.fn(),
    });
    bus.attach();

    window.dispatchEvent(new CustomEvent("atlas:microflow-forbidden"));

    expect(bus.getSnapshot().readonlyReason).toMatchObject({
      code: "MICROFLOW_PERMISSION_DENIED",
      category: "permission",
      httpStatus: 403,
    });
  });

  it("redirects on unauthorized", () => {
    const onUnauthorizedRedirect = vi.fn();
    const bus = new MicroflowWorkbenchErrorBus({
      onUnauthorizedRedirect,
      onOpenProblems: vi.fn(),
    });
    bus.attach();

    window.dispatchEvent(new CustomEvent("atlas:microflow-unauthorized"));

    expect(onUnauthorizedRedirect).toHaveBeenCalledTimes(1);
  });
});
