import { describe, expect, it } from "vitest";

import { buildRuntimeHighlightState } from "../trace-highlighting";
import type { MicroflowRunSession } from "../trace-types";

function session(trace: MicroflowRunSession["trace"]): MicroflowRunSession {
  return {
    id: "run-1",
    schemaId: "schema-1",
    startedAt: "2026-05-02T00:00:00Z",
    status: "success",
    input: {},
    trace,
    logs: [],
    variables: [],
  };
}

describe("buildRuntimeHighlightState", () => {
  it("marks gateway branch trace as visited or skipped", () => {
    const state = buildRuntimeHighlightState(session([
      {
        id: "frame-gateway",
        runId: "run-1",
        objectId: "inclusive-fork",
        status: "success",
        startedAt: "2026-05-02T00:00:00Z",
        durationMs: 1,
        input: { gatewayKind: "inclusive" },
        output: {
          branchTrace: [
            { flowId: "flow-a", branchId: "flow-a", targetObjectId: "a", selected: true, status: "completed" },
            { flowId: "flow-b", branchId: "flow-b", targetObjectId: "b", selected: false, status: "skipped" },
          ],
        },
      },
    ]));

    expect(state.flows["flow-a"]).toMatchObject({ visited: true, selectedCase: true });
    expect(state.flows["flow-b"]).toMatchObject({ visited: false, skipped: true });
  });
});
