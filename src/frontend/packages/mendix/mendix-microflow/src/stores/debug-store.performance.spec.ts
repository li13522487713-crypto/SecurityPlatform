import { describe, expect, it } from "vitest";
import { createDebugStore, DEBUG_WS_EVENTS } from "./debug-store";

describe("DebugStore performance smoke checks", () => {
  it("processes node-enter event within 50ms", () => {
    const store = createDebugStore();

    const started = performance.now();
    store.handleEvent({
      type: DEBUG_WS_EVENTS.NODE_ENTER,
      nodeId: "node-perf-1",
      flowId: "flow-perf-1",
      branchId: "branch-perf-1",
      safePoint: "before-node-perf-1",
    });
    const elapsed = performance.now() - started;

    expect(elapsed).toBeLessThan(50);
    expect(store.getSnapshot().nodeState.currentNodeId).toBe("node-perf-1");
  });

  it("processes paused variable refresh within 100ms", () => {
    const store = createDebugStore();
    const variables = Array.from({ length: 200 }, (_, index) => ({
      name: `$v${index}`,
      value: `${index}`,
      type: "String",
    }));

    const started = performance.now();
    store.handleEvent({
      type: DEBUG_WS_EVENTS.PAUSED,
      nodeId: "node-perf-2",
      safePoint: "paused-node-perf-2",
      variables,
      callStack: [
        { runId: "run-perf", microflowId: "mf-perf", depth: 0 },
      ],
    });
    const elapsed = performance.now() - started;

    expect(elapsed).toBeLessThan(100);
    const snapshot = store.getSnapshot();
    expect(snapshot.variables).toHaveLength(200);
    expect(snapshot.callStack).toHaveLength(1);
    expect(snapshot.paused).toBe(true);
  });
});
