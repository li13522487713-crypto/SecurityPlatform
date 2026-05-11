import { describe, expect, it } from "vitest";

import type { MicroflowTraceFrame } from "../debug/trace-types";
import { deriveEdgeRuntimeStateByFlowId } from "./runtime-edge-state";

function frame(patch: Partial<MicroflowTraceFrame>): MicroflowTraceFrame {
  return {
    id: "f-1",
    runId: "run-1",
    objectId: "node-1",
    status: "success",
    startedAt: "2026-05-05T00:00:00.000Z",
    durationMs: 1,
    ...patch,
  };
}

describe("deriveEdgeRuntimeStateByFlowId", () => {
  it("marks selected gateway branch as selectedCase", () => {
    const states = deriveEdgeRuntimeStateByFlowId([
      frame({
        output: {
          branchTrace: [
            { flowId: "flow-true", branchId: "branch-true", selected: true, status: "completed" },
            { flowId: "flow-false", branchId: "branch-false", selected: false, status: "skipped" },
          ],
        },
      }),
    ]);
    expect(states.get("flow-true")).toBe("selectedCase");
    expect(states.get("branch-true")).toBe("selectedCase");
    expect(states.get("flow-false")).toBe("skipped");
  });

  it("keeps higher-priority selectedCase when base status also writes the same edge", () => {
    const states = deriveEdgeRuntimeStateByFlowId([
      frame({
        outgoingFlowId: "flow-a",
        status: "success",
      }),
      frame({
        output: {
          branchTrace: [{ flowId: "flow-a", branchId: "flow-a", selected: true, status: "executed" }],
        },
      }),
      frame({
        outgoingFlowId: "flow-a",
        status: "skipped",
      }),
    ]);
    expect(states.get("flow-a")).toBe("selectedCase");
  });

  it("maps failed branch trace to failed runtime state", () => {
    const states = deriveEdgeRuntimeStateByFlowId([
      frame({
        output: {
          branchTrace: [{ flowId: "flow-failed", branchId: "branch-failed", selected: false, status: "failed" }],
        },
      }),
    ]);
    expect(states.get("flow-failed")).toBe("failed");
    expect(states.get("branch-failed")).toBe("failed");
  });

  it("promotes running frame branchTrace selection to selectedCase", () => {
    const states = deriveEdgeRuntimeStateByFlowId([
      frame({
        status: "running",
        output: {
          branchTrace: [{ flowId: "branch-live", branchId: "branch-live", selected: true, status: "completed" }],
        },
      }),
    ]);
    expect(states.get("branch-live")).toBe("selectedCase");
  });

  it("only projects runtime states from latest runId", () => {
    const states = deriveEdgeRuntimeStateByFlowId([
      frame({
        runId: "run-old",
        outgoingFlowId: "flow-a",
        status: "failed",
      }),
      frame({
        runId: "run-new",
        outgoingFlowId: "flow-a",
        status: "success",
      }),
    ]);
    expect(states.get("flow-a")).toBe("visited");
  });
});
