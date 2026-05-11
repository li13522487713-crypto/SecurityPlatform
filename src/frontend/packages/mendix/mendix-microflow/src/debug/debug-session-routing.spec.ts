import { describe, expect, it } from "vitest";

import type { MicroflowDebugSessionDto } from "./step-debug-api";
import { collectDebugSessionMicroflowIds, resolveDeepestDebugMicroflowId } from "./debug-session-routing";

function createSession(overrides: Partial<MicroflowDebugSessionDto> = {}): MicroflowDebugSessionDto {
  return {
    id: "session-1",
    microflowId: "mf-parent",
    status: "paused",
    callStack: [],
    ...overrides,
  };
}

describe("debug-session-routing", () => {
  it("collects fallback, session and call-stack microflow ids without duplicates", () => {
    const session = createSession({
      callStack: [
        { id: "frame-1", microflowId: "mf-parent", runId: "run-parent", depth: 0, status: "paused" },
        { id: "frame-2", microflowId: "mf-child", runId: "run-child", depth: 1, status: "paused" },
        { id: "frame-3", microflowId: "mf-parent", runId: "run-parent-2", depth: 2, status: "paused" },
      ],
    });

    expect(collectDebugSessionMicroflowIds(session, "mf-fallback")).toEqual([
      "mf-fallback",
      "mf-parent",
      "mf-child",
    ]);
  });

  it("prefers the deepest call-stack frame when resolving route target", () => {
    const session = createSession({
      callStack: [
        { id: "frame-1", microflowId: "mf-parent", runId: "run-parent", depth: 0, status: "paused" },
        { id: "frame-2", microflowId: "mf-child", runId: "run-child", depth: 1, status: "paused" },
      ],
    });

    expect(resolveDeepestDebugMicroflowId(session, "mf-fallback")).toBe("mf-child");
  });

  it("falls back to session microflow id and provided fallback when the call stack is empty", () => {
    expect(resolveDeepestDebugMicroflowId(createSession({ callStack: [] }), "mf-fallback")).toBe("mf-parent");
    expect(resolveDeepestDebugMicroflowId(createSession({ microflowId: "", callStack: [] }), "mf-fallback")).toBe("mf-fallback");
  });
});
