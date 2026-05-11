import { describe, expect, it } from "vitest";
import type { MicroflowTraceFrame } from "../debug/trace-types";
import { composeTraceFramesForRuntimePreview } from "./ws-runtime-trace";

function frame(patch: Partial<MicroflowTraceFrame>): MicroflowTraceFrame {
  return {
    id: "frame-1",
    runId: "run-1",
    objectId: "node-1",
    status: "success",
    startedAt: "2026-01-01T00:00:00.000Z",
    durationMs: 1,
    ...patch,
  };
}

describe("composeTraceFramesForRuntimePreview", () => {
  it("returns base frames when websocket current node is absent", () => {
    const base = [frame({ objectId: "node-a", status: "success" })];
    expect(composeTraceFramesForRuntimePreview({ baseFrames: base, wsCurrentNodeId: "" })).toBe(base);
  });

  it("appends synthetic running frame for websocket current node", () => {
    const result = composeTraceFramesForRuntimePreview({
      baseFrames: [frame({ objectId: "node-a", status: "success" })],
      wsCurrentNodeId: "node-live",
      sessionId: "session-1",
    });
    expect(result).toHaveLength(2);
    expect(result[1]?.id).toBe("ws-live-node-live");
    expect(result[1]?.status).toBe("running");
    expect(result[1]?.runId).toBe("session-1");
  });

  it("does not duplicate running frame for same current node", () => {
    const base = [frame({ objectId: "node-live", status: "running" })];
    const result = composeTraceFramesForRuntimePreview({
      baseFrames: base,
      wsCurrentNodeId: "node-live",
      sessionId: "session-1",
    });
    expect(result).toBe(base);
  });

  it("injects selected branchTrace for realtime edge highlight", () => {
    const result = composeTraceFramesForRuntimePreview({
      baseFrames: [frame({ objectId: "node-a", status: "success" })],
      wsCurrentNodeId: "node-live",
      wsCurrentBranchId: "branch-live",
      sessionId: "session-2",
    });
    expect(result[1]?.output?.branchTrace?.[0]).toEqual({
      flowId: "branch-live",
      branchId: "branch-live",
      selected: true,
      status: "completed",
    });
  });
});
