import { describe, expect, it } from "vitest";
import { createDebugStore, DEBUG_WS_EVENTS } from "./debug-store";

describe("DebugStore websocket event handling", () => {
  it("tracks node/branch progress and breakpoint pause state", () => {
    const store = createDebugStore();

    store.handleEvent({
      type: DEBUG_WS_EVENTS.NODE_ENTER,
      nodeId: "node-a",
      flowId: "flow-a",
      branchId: "branch-a",
      safePoint: "before-node-a",
    });
    store.handleEvent({
      type: DEBUG_WS_EVENTS.EDGE_TAKEN,
      branchId: "branch-b",
    });
    store.handleEvent({
      type: DEBUG_WS_EVENTS.BREAKPOINT_HIT,
      nodeId: "node-b",
      safePoint: "breakpoint-hit",
    });

    const snapshot = store.getSnapshot();
    expect(snapshot.nodeState.currentNodeId).toBe("node-b");
    expect(snapshot.nodeState.currentFlowId).toBe("flow-a");
    expect(snapshot.nodeState.currentBranchId).toBe("branch-b");
    expect(snapshot.nodeState.currentSafePoint).toBe("breakpoint-hit");
    expect(snapshot.paused).toBe(true);
  });

  it("tracks loop iteration, call stack and variable snapshots", () => {
    const store = createDebugStore();
    store.handleEvent({
      type: DEBUG_WS_EVENTS.LOOP_ITER,
      iteration: {
        nodeId: "loop-node",
        iterationIndex: 2,
        totalIterations: 10,
      },
    });
    store.handleEvent({
      type: DEBUG_WS_EVENTS.STACK_TOP,
      callStack: [
        { id: "frame-child", runId: "run-child", microflowId: "mf-child", depth: 1, status: "paused", callerObjectId: "call-node-1" },
        { id: "frame-root", runId: "run-root", microflowId: "mf-root", depth: 0, status: "running" },
      ],
    });
    store.handleEvent({
      type: DEBUG_WS_EVENTS.VAR_SNAPSHOT,
      variables: [
        { name: "$orderId", value: "SO-1001", type: "String" },
        { name: "$approved", value: "true", type: "Boolean" },
      ],
    });

    const snapshot = store.getSnapshot();
    expect(snapshot.loopIteration?.nodeId).toBe("loop-node");
    expect(snapshot.loopIteration?.iterationIndex).toBe(2);
    expect(snapshot.callStack).toHaveLength(2);
    expect(snapshot.callStack[0]).toEqual(expect.objectContaining({ id: "frame-child", status: "paused", callerNodeId: "call-node-1" }));
    expect(snapshot.callStack[1]).toEqual(expect.objectContaining({ id: "frame-root", status: "running" }));
    expect(snapshot.variables.map(item => item.name)).toEqual(["$orderId", "$approved"]);
  });

  it("updates call stack from push/pop events", () => {
    const store = createDebugStore();
    store.handleEvent({
      type: DEBUG_WS_EVENTS.STACK_PUSH,
      callStack: [
        { runId: "run-root", microflowId: "mf-root", depth: 0 },
        { runId: "run-child", microflowId: "mf-child", depth: 1 },
      ],
    });
    let snapshot = store.getSnapshot();
    expect(snapshot.callStack.map(frame => frame.microflowId)).toEqual(["mf-root", "mf-child"]);

    store.handleEvent({
      type: DEBUG_WS_EVENTS.STACK_POP,
      callStack: [
        { runId: "run-root", microflowId: "mf-root", depth: 0 },
      ],
    });
    snapshot = store.getSnapshot();
    expect(snapshot.callStack.map(frame => frame.microflowId)).toEqual(["mf-root"]);
  });

  it("updates active session id from session events", () => {
    const store = createDebugStore();
    store.handleEvent({
      type: DEBUG_WS_EVENTS.SESSION,
      sessionId: "session-new",
    });
    expect(store.getSnapshot().sessionId).toBe("session-new");
  });

  it("pauses on breakpoint and refreshes variables from snapshot events", () => {
    const store = createDebugStore();
    store.handleEvent({
      type: DEBUG_WS_EVENTS.BREAKPOINT_HIT,
      nodeId: "decision-1",
      safePoint: "before-decision",
    });
    store.handleEvent({
      type: DEBUG_WS_EVENTS.VAR_SNAPSHOT,
      variables: [
        { name: "$orderId", value: "SO-1002", type: "String" },
        { name: "$riskLevel", value: "high", type: "String" },
      ],
    });

    const snapshot = store.getSnapshot();
    expect(snapshot.paused).toBe(true);
    expect(snapshot.nodeState.currentNodeId).toBe("decision-1");
    expect(snapshot.variables).toEqual([
      { name: "$orderId", value: "SO-1002", type: "String" },
      { name: "$riskLevel", value: "high", type: "String" },
    ]);
  });

  it("keeps queued command payload for later websocket flush", () => {
    const store = createDebugStore();
    store.queueCommand("run-to-node", { nodeId: "node-42", reason: "test" });

    const queued = store.popCommands();
    expect(queued).toEqual([
      {
        command: "run-to-node",
        payload: { nodeId: "node-42", reason: "test" },
      },
    ]);
  });

  it("records error state and clears pause/loop marker on complete", () => {
    const store = createDebugStore();
    store.handleEvent({
      type: DEBUG_WS_EVENTS.NODE_ENTER,
      nodeId: "rest-call",
      flowId: "flow-rest",
      branchId: "branch-rest",
      safePoint: "before-rest-call",
    });
    store.handleEvent({
      type: DEBUG_WS_EVENTS.EDGE_TAKEN,
      branchId: "branch-rest-next",
    });
    store.handleEvent({
      type: DEBUG_WS_EVENTS.LOOP_ITER,
      iteration: {
        nodeId: "loop-node",
        iterationIndex: 4,
        totalIterations: 8,
      },
    });
    store.handleEvent({
      type: DEBUG_WS_EVENTS.ERROR,
      nodeId: "rest-call",
      safePoint: "after-rest-call",
      error: {
        message: "Runtime exploded",
        stack: "Error: Runtime exploded\n  at rest-call\n  at flow-exit",
      },
    });
    let snapshot = store.getSnapshot();
    expect(snapshot.activeError).toBe("Runtime exploded");
    expect(snapshot.activeErrorStack).toContain("rest-call");
    expect(snapshot.lastError).toBe("Runtime exploded");
    expect(snapshot.nodeState.currentNodeId).toBe("rest-call");
    expect(snapshot.nodeState.currentSafePoint).toBe("after-rest-call");
    expect(snapshot.paused).toBe(true);

    store.handleEvent({
      type: DEBUG_WS_EVENTS.COMPLETE,
    });
    snapshot = store.getSnapshot();
    expect(snapshot.loopIteration).toBeUndefined();
    expect(snapshot.nodeState.currentNodeId).toBeUndefined();
    expect(snapshot.nodeState.currentFlowId).toBeUndefined();
    expect(snapshot.nodeState.currentBranchId).toBeUndefined();
    expect(snapshot.nodeState.currentSafePoint).toBeUndefined();
    expect(snapshot.paused).toBe(false);
  });

  it("clears error stack when clearError is invoked", () => {
    const store = createDebugStore();
    store.handleEvent({
      type: DEBUG_WS_EVENTS.ERROR,
      error: {
        message: "Failed",
        stack: "Error: Failed\n at unit-test",
      },
    });
    expect(store.getSnapshot().activeErrorStack).toContain("unit-test");
    store.clearError();
    const snapshot = store.getSnapshot();
    expect(snapshot.activeError).toBeUndefined();
    expect(snapshot.activeErrorStack).toBeUndefined();
  });
});
