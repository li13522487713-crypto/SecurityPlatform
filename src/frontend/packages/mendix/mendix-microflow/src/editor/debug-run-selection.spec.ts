import { describe, expect, it } from "vitest";

import type { MicroflowRunSession } from "../debug";
import { resolveDebugRunSelectionOutcome } from "./debug-run-selection";

function createRunSession(overrides?: Partial<MicroflowRunSession>): MicroflowRunSession {
  return {
    id: "run-1",
    schemaId: "schema-1",
    resourceId: "mf-target",
    startedAt: "2026-05-12T12:00:00.000Z",
    status: "success",
    input: {},
    output: {},
    logs: [],
    variables: [],
    trace: [
      {
        id: "frame-1",
        runId: "run-1",
        objectId: "node-1",
        status: "success",
        startedAt: "2026-05-12T12:00:00.000Z",
      },
    ],
    ...overrides,
  };
}

describe("resolveDebugRunSelectionOutcome", () => {
  it("优先使用 run detail 自带的资源归属和首个 trace frame 焦点", () => {
    const outcome = resolveDebugRunSelectionOutcome("mf-source", createRunSession());

    expect(outcome.targetMicroflowId).toBe("mf-target");
    expect(outcome.focusIntent).toEqual({
      runId: "run-1",
      frameId: "frame-1",
      objectId: "node-1",
    });
  });

  it("目标资源缺失时回退到来源微流，且无 trace 时不生成焦点", () => {
    const outcome = resolveDebugRunSelectionOutcome("mf-source", createRunSession({
      resourceId: " ",
      trace: [],
    }));

    expect(outcome.targetMicroflowId).toBe("mf-source");
    expect(outcome.focusIntent).toBeUndefined();
  });
});
