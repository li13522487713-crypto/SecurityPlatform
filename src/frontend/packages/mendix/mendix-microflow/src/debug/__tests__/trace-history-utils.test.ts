import { describe, expect, it } from "vitest";

import type { MicroflowRunSession } from "../trace-types";
import {
  buildExecutionPath,
  buildRunHistoryItemFromSession,
  filterNodeResultsByMicroflowId,
  normalizeRunHistoryStatus,
} from "../trace-history-utils";

function createSession(): MicroflowRunSession {
  return {
    id: "run-parent",
    schemaId: "schema-parent",
    resourceId: "MF_Parent",
    version: "1.0.0",
    status: "failed",
    startedAt: "2026-04-28T01:00:00.000Z",
    endedAt: "2026-04-28T01:00:01.000Z",
    input: {},
    output: undefined,
    error: { code: "RUNTIME_UNSUPPORTED_ACTION", message: "unsupported", objectId: "node-2" },
    trace: [
      { id: "f1", runId: "run-parent", microflowId: "MF_Parent", objectId: "node-1", status: "success", startedAt: "2026-04-28T01:00:00.000Z", durationMs: 10 },
      { id: "f2", runId: "run-parent", microflowId: "MF_Parent", objectId: "node-2", status: "failed", startedAt: "2026-04-28T01:00:00.010Z", durationMs: 12, error: { code: "RUNTIME_UNSUPPORTED_ACTION", message: "unsupported" } },
    ],
    logs: [],
    variables: [],
    childRuns: [
      {
        id: "run-child",
        schemaId: "schema-child",
        resourceId: "MF_Child",
        status: "success",
        startedAt: "2026-04-28T01:00:00.020Z",
        endedAt: "2026-04-28T01:00:00.030Z",
        input: {},
        output: undefined,
        trace: [
          { id: "c1", runId: "run-child", microflowId: "MF_Child", objectId: "child-node-1", status: "success", startedAt: "2026-04-28T01:00:00.020Z", durationMs: 6, callDepth: 1 },
        ],
        logs: [],
        variables: [],
        childRuns: [],
        childRunIds: [],
      },
    ],
    childRunIds: ["run-child"],
  };
}

describe("trace history utils", () => {
  it("normalizes unsupported status by error code", () => {
    expect(normalizeRunHistoryStatus("failed", "RUNTIME_UNSUPPORTED_ACTION")).toBe("unsupported");
    expect(normalizeRunHistoryStatus("success")).toBe("success");
  });

  it("builds run history item from session", () => {
    const item = buildRunHistoryItemFromSession("MF_Parent", createSession());
    expect(item.status).toBe("unsupported");
    expect(item.durationMs).toBe(1000);
  });

  it("builds execution path with child run depth", () => {
    const path = buildExecutionPath(createSession());
    expect(path.map(item => item.frame.id)).toEqual(["f1", "f2", "c1"]);
    expect(path.at(-1)?.callDepth).toBe(1);
  });

  it("filters node results by microflow id", () => {
    const frames = filterNodeResultsByMicroflowId(createSession(), "MF_Parent");
    expect(frames.map(frame => frame.id)).toEqual(["f1", "f2"]);
  });
});
