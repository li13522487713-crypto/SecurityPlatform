import { describe, expect, it } from "vitest";
import type { MicroflowDebugSessionDto } from "../debug/step-debug-api";
import { buildSessionDebugRouteIntent, buildWsDebugRouteIntent } from "./debug-routing";

function createSession(overrides: Partial<MicroflowDebugSessionDto> = {}): MicroflowDebugSessionDto {
  return {
    id: "session-1",
    microflowId: "mf-parent",
    status: "paused",
    callStack: [],
    ...overrides,
  };
}

describe("buildWsDebugRouteIntent", () => {
  it("routes to deepest ws call-stack microflow", () => {
    const intent = buildWsDebugRouteIntent({
      debugSessionId: "session-ws",
      activeMicroflowId: "mf-parent",
      callStack: [
        { runId: "run-1", microflowId: "mf-parent", depth: 0 },
        { runId: "run-2", microflowId: "mf-child", depth: 1 },
      ],
      nodeState: {
        currentNodeId: "node-a",
        currentSafePoint: "before-rest",
      },
    });
    expect(intent).toEqual({
      targetMicroflowId: "mf-child",
      routeKey: "ws|session-ws|mf-parent|mf-child|node-a|before-rest|2",
    });
  });

  it("returns undefined when deepest ws stack equals active microflow", () => {
    const intent = buildWsDebugRouteIntent({
      debugSessionId: "session-ws",
      activeMicroflowId: "mf-parent",
      callStack: [{ runId: "run-1", microflowId: "mf-parent", depth: 0 }],
      nodeState: {},
    });
    expect(intent).toBeUndefined();
  });
});

describe("buildSessionDebugRouteIntent", () => {
  it("routes to child microflow by debug session stack (step into)", () => {
    const intent = buildSessionDebugRouteIntent({
      sourceMicroflowId: "mf-parent",
      session: createSession({
        id: "session-debug",
        callStack: [
          { id: "frame-1", microflowId: "mf-parent", runId: "run-parent", depth: 0, status: "paused" },
          { id: "frame-2", microflowId: "mf-child", runId: "run-child", depth: 1, status: "paused" },
        ],
        currentSafePoint: {
          nodeObjectId: "node-step-into",
          phase: "before",
        },
        lastUpdatedAt: "2026-05-11T10:00:00.000Z",
      }),
    });
    expect(intent).toEqual({
      targetMicroflowId: "mf-child",
      routeKey: "session-debug|mf-parent|mf-child|node-step-into|before|2026-05-11T10:00:00.000Z",
    });
  });

  it("routes back to parent microflow when child frame is popped (step out)", () => {
    const childIntent = buildSessionDebugRouteIntent({
      sourceMicroflowId: "mf-parent",
      session: createSession({
        id: "session-debug",
        callStack: [
          { id: "frame-1", microflowId: "mf-parent", runId: "run-parent", depth: 0, status: "paused" },
          { id: "frame-2", microflowId: "mf-child", runId: "run-child", depth: 1, status: "paused" },
        ],
      }),
    });
    const parentIntent = buildSessionDebugRouteIntent({
      sourceMicroflowId: "mf-child",
      session: createSession({
        id: "session-debug",
        callStack: [{ id: "frame-1", microflowId: "mf-parent", runId: "run-parent", depth: 0, status: "paused" }],
      }),
    });
    expect(childIntent?.targetMicroflowId).toBe("mf-child");
    expect(parentIntent?.targetMicroflowId).toBe("mf-parent");
  });

  it("returns undefined when target equals current microflow or session is missing", () => {
    expect(buildSessionDebugRouteIntent({
      sourceMicroflowId: "mf-parent",
      session: createSession({ callStack: [] }),
    })).toBeUndefined();
    expect(buildSessionDebugRouteIntent({
      sourceMicroflowId: "mf-parent",
      session: undefined,
    })).toBeUndefined();
  });
});
