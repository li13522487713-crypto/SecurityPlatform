// @vitest-environment jsdom

import { useEffect } from "react";
import { act, cleanup, render } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createDebugStore, DEBUG_WS_EVENTS } from "../stores/debug-store";
import { useDebugWebSocket, type UseDebugWebSocketOptions } from "./use-debug-ws";

interface FakeSocketMessage {
  type: string;
  [key: string]: unknown;
}

class FakeWebSocket {
  static instances: FakeWebSocket[] = [];
  static OPEN = 1;
  static CONNECTING = 0;
  static CLOSING = 2;
  static CLOSED = 3;

  readonly url: string;
  readyState = FakeWebSocket.CONNECTING;
  sent: string[] = [];
  onopen: (() => void) | null = null;
  onclose: (() => void) | null = null;
  onerror: (() => void) | null = null;
  onmessage: ((event: { data: string }) => void) | null = null;

  constructor(url: string) {
    this.url = url;
    FakeWebSocket.instances.push(this);
  }

  send(payload: string) {
    this.sent.push(payload);
  }

  close() {
    this.readyState = FakeWebSocket.CLOSED;
    this.onclose?.();
  }

  open() {
    this.readyState = FakeWebSocket.OPEN;
    this.onopen?.();
  }
}

function decodeMessages(socket: FakeWebSocket): FakeSocketMessage[] {
  return socket.sent.map(item => JSON.parse(item) as FakeSocketMessage);
}

function HookHarness(props: {
  microflowId: string;
  sessionId?: string;
  store: ReturnType<typeof createDebugStore>;
  options?: Partial<UseDebugWebSocketOptions>;
  onReady: (api: ReturnType<typeof useDebugWebSocket>) => void;
}) {
  const api = useDebugWebSocket(props.microflowId, {
    store: props.store,
    sessionId: props.sessionId,
    pingIntervalMs: 60_000,
    ...props.options,
  });
  useEffect(() => {
    props.onReady(api);
  }, [api, props]);
  return null;
}

function MultiHookHarness(props: {
  left: {
    microflowId: string;
    sessionId?: string;
    store: ReturnType<typeof createDebugStore>;
    options?: Partial<UseDebugWebSocketOptions>;
    onReady: (api: ReturnType<typeof useDebugWebSocket>) => void;
  };
  right: {
    microflowId: string;
    sessionId?: string;
    store: ReturnType<typeof createDebugStore>;
    options?: Partial<UseDebugWebSocketOptions>;
    onReady: (api: ReturnType<typeof useDebugWebSocket>) => void;
  };
}) {
  const leftApi = useDebugWebSocket(props.left.microflowId, {
    store: props.left.store,
    sessionId: props.left.sessionId,
    pingIntervalMs: 60_000,
    ...props.left.options,
  });
  const rightApi = useDebugWebSocket(props.right.microflowId, {
    store: props.right.store,
    sessionId: props.right.sessionId,
    pingIntervalMs: 60_000,
    ...props.right.options,
  });
  useEffect(() => {
    props.left.onReady(leftApi);
  }, [leftApi, props]);
  useEffect(() => {
    props.right.onReady(rightApi);
  }, [rightApi, props]);
  return null;
}

describe("useDebugWebSocket", () => {
  const originalWebSocket = globalThis.WebSocket;

  beforeEach(() => {
    FakeWebSocket.instances = [];
    vi.useFakeTimers();
    (globalThis as typeof globalThis & { WebSocket: typeof WebSocket }).WebSocket = FakeWebSocket as unknown as typeof WebSocket;
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
    (globalThis as typeof globalThis & { WebSocket: typeof WebSocket }).WebSocket = originalWebSocket;
  });

  it("registers existing breakpoints and sends hello payload on open", () => {
    const store = createDebugStore();
    store.upsertBreakpoint({ id: "bp-1", nodeId: "node-1", scope: "node", enabled: true });
    let api: ReturnType<typeof useDebugWebSocket> | undefined;

    render(<HookHarness microflowId="mf-1" sessionId="session-1" store={store} onReady={next => { api = next; }} />);

    act(() => {
      api?.connect();
    });
    const socket = FakeWebSocket.instances[0];
    if (!socket) {
      throw new Error("Expected a websocket instance.");
    }
    act(() => {
      socket.open();
    });

    const messages = decodeMessages(socket);
    expect(messages).toEqual(expect.arrayContaining([
      expect.objectContaining({ type: "set-breakpoint", data: expect.objectContaining({ nodeId: "node-1", enabled: true }) }),
      expect.objectContaining({ type: "hello", sessionId: "session-1" }),
    ]));
  });

  it("queues command before open and flushes after connection is ready", () => {
    const store = createDebugStore();
    let api: ReturnType<typeof useDebugWebSocket> | undefined;
    render(<HookHarness microflowId="mf-queue" sessionId="session-2" store={store} onReady={next => { api = next; }} />);

    act(() => {
      api?.send("step-over");
      api?.connect();
    });
    const socket = FakeWebSocket.instances[0];
    if (!socket) {
      throw new Error("Expected a websocket instance.");
    }
    expect(socket.sent).toHaveLength(0);

    act(() => {
      socket.open();
    });

    const messages = decodeMessages(socket);
    expect(messages).toEqual(expect.arrayContaining([
      expect.objectContaining({ type: "step-over" }),
    ]));
  });

  it("flushes queued command payload after socket reconnects", () => {
    const store = createDebugStore();
    let api: ReturnType<typeof useDebugWebSocket> | undefined;
    render(<HookHarness microflowId="mf-queue-payload" sessionId="session-queue-payload" store={store} onReady={next => { api = next; }} />);

    act(() => {
      api?.send("run-to-node", { nodeId: "node-88", targetId: "node-88" });
      api?.connect();
    });
    const socket = FakeWebSocket.instances[0];
    if (!socket) {
      throw new Error("Expected a websocket instance.");
    }
    act(() => {
      socket.open();
    });

    const messages = decodeMessages(socket);
    expect(messages).toEqual(expect.arrayContaining([
      expect.objectContaining({ type: "run-to-node", nodeId: "node-88", targetId: "node-88" }),
    ]));
  });

  it("re-registers breakpoints after reconnect", () => {
    const store = createDebugStore();
    store.upsertBreakpoint({ id: "bp-reconnect", nodeId: "node-reconnect", scope: "node", enabled: true });
    let api: ReturnType<typeof useDebugWebSocket> | undefined;
    render(
      <HookHarness
        microflowId="mf-breakpoint-reconnect"
        sessionId="session-breakpoint-reconnect"
        store={store}
        options={{ reconnectDelayMs: 50 }}
        onReady={next => { api = next; }}
      />,
    );

    act(() => {
      api?.connect();
    });
    const first = FakeWebSocket.instances[0];
    if (!first) {
      throw new Error("Expected first websocket instance.");
    }
    act(() => {
      first.open();
      first.close();
    });
    act(() => {
      vi.advanceTimersByTime(50);
    });
    const second = FakeWebSocket.instances[1];
    if (!second) {
      throw new Error("Expected second websocket instance.");
    }
    act(() => {
      second.open();
    });

    const secondMessages = decodeMessages(second);
    expect(secondMessages).toEqual(expect.arrayContaining([
      expect.objectContaining({
        type: "set-breakpoint",
        data: expect.objectContaining({ nodeId: "node-reconnect", enabled: true }),
      }),
    ]));
  });

  it("reconnects with exponential backoff and stops after max attempts", () => {
    const store = createDebugStore();
    const statuses: string[] = [];
    let api: ReturnType<typeof useDebugWebSocket> | undefined;
    render(
      <HookHarness
        microflowId="mf-reconnect"
        sessionId="session-reconnect"
        store={store}
        options={{
          autoReconnect: 2,
          reconnectDelayMs: 100,
          onStatusChange: status => statuses.push(status),
        }}
        onReady={next => { api = next; }}
      />,
    );

    act(() => {
      api?.connect();
    });
    expect(FakeWebSocket.instances).toHaveLength(1);
    const socket1 = FakeWebSocket.instances[0];
    if (!socket1) {
      throw new Error("Expected first websocket instance.");
    }
    act(() => {
      socket1.close();
    });
    act(() => {
      vi.advanceTimersByTime(99);
    });
    expect(FakeWebSocket.instances).toHaveLength(1);
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(FakeWebSocket.instances).toHaveLength(2);

    const socket2 = FakeWebSocket.instances[1];
    if (!socket2) {
      throw new Error("Expected second websocket instance.");
    }
    act(() => {
      socket2.close();
    });
    act(() => {
      vi.advanceTimersByTime(199);
    });
    expect(FakeWebSocket.instances).toHaveLength(2);
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(FakeWebSocket.instances).toHaveLength(3);

    const socket3 = FakeWebSocket.instances[2];
    if (!socket3) {
      throw new Error("Expected third websocket instance.");
    }
    act(() => {
      socket3.close();
    });
    act(() => {
      vi.runOnlyPendingTimers();
    });

    expect(FakeWebSocket.instances).toHaveLength(3);
    expect(statuses).toContain("error");
  });

  it("sends heartbeat ping at configured interval after open", () => {
    const store = createDebugStore();
    let api: ReturnType<typeof useDebugWebSocket> | undefined;
    render(
      <HookHarness
        microflowId="mf-ping"
        sessionId="session-ping"
        store={store}
        options={{ pingIntervalMs: 500 }}
        onReady={next => { api = next; }}
      />,
    );

    act(() => {
      api?.connect();
    });
    const socket = FakeWebSocket.instances[0];
    if (!socket) {
      throw new Error("Expected websocket instance.");
    }
    act(() => {
      socket.open();
    });
    const before = socket.sent.length;

    act(() => {
      vi.advanceTimersByTime(500);
    });

    const messages = decodeMessages(socket).slice(before);
    expect(messages).toEqual(expect.arrayContaining([
      expect.objectContaining({ type: "ping" }),
    ]));
  });

  it("emits connecting/connected/disconnected status changes", () => {
    const store = createDebugStore();
    const statuses: string[] = [];
    let api: ReturnType<typeof useDebugWebSocket> | undefined;
    render(
      <HookHarness
        microflowId="mf-status"
        sessionId="session-status"
        store={store}
        options={{ onStatusChange: status => statuses.push(status) }}
        onReady={next => { api = next; }}
      />,
    );

    act(() => {
      api?.connect();
    });
    const socket = FakeWebSocket.instances[0];
    if (!socket) {
      throw new Error("Expected websocket instance.");
    }
    act(() => {
      socket.open();
      socket.close();
    });

    expect(statuses).toEqual(expect.arrayContaining(["connecting", "connected", "disconnected"]));
  });

  it("keeps debug runtime snapshot after disconnect", () => {
    const store = createDebugStore();
    let api: ReturnType<typeof useDebugWebSocket> | undefined;
    render(
      <HookHarness
        microflowId="mf-preserve-runtime"
        sessionId="session-preserve-runtime"
        store={store}
        onReady={next => { api = next; }}
      />,
    );

    act(() => {
      api?.connect();
    });
    const socket = FakeWebSocket.instances[0];
    if (!socket) {
      throw new Error("Expected websocket instance.");
    }
    act(() => {
      socket.open();
      socket.onmessage?.({
        data: JSON.stringify({
          type: DEBUG_WS_EVENTS.NODE_ENTER,
          nodeId: "node-before-disconnect",
          flowId: "flow-before-disconnect",
        }),
      });
      socket.close();
    });

    const snapshot = store.getSnapshot();
    expect(snapshot.nodeState.currentNodeId).toBe("node-before-disconnect");
    expect(snapshot.nodeState.currentFlowId).toBe("flow-before-disconnect");
    expect(snapshot.status).toBe("disconnected");
  });

  it("isolates parallel sessions so different users do not interfere", () => {
    const leftStore = createDebugStore();
    const rightStore = createDebugStore();
    let leftApi: ReturnType<typeof useDebugWebSocket> | undefined;
    let rightApi: ReturnType<typeof useDebugWebSocket> | undefined;

    render(
      <MultiHookHarness
        left={{
          microflowId: "mf-shared",
          sessionId: "session-user-A",
          store: leftStore,
          onReady: api => { leftApi = api; },
        }}
        right={{
          microflowId: "mf-shared",
          sessionId: "session-user-B",
          store: rightStore,
          onReady: api => { rightApi = api; },
        }}
      />,
    );

    act(() => {
      leftApi?.send("step-over");
      rightApi?.send("step-into");
      leftApi?.connect();
      rightApi?.connect();
    });

    const leftSocket = FakeWebSocket.instances[0];
    const rightSocket = FakeWebSocket.instances[1];
    if (!leftSocket || !rightSocket) {
      throw new Error("Expected two websocket instances for parallel sessions.");
    }
    act(() => {
      leftSocket.open();
      rightSocket.open();
    });

    const leftMessages = decodeMessages(leftSocket);
    const rightMessages = decodeMessages(rightSocket);
    expect(leftMessages).toEqual(expect.arrayContaining([
      expect.objectContaining({ type: "hello", sessionId: "session-user-A" }),
      expect.objectContaining({ type: "step-over" }),
    ]));
    expect(rightMessages).toEqual(expect.arrayContaining([
      expect.objectContaining({ type: "hello", sessionId: "session-user-B" }),
      expect.objectContaining({ type: "step-into" }),
    ]));
    expect(leftMessages.some(message => message.sessionId === "session-user-B")).toBe(false);
    expect(rightMessages.some(message => message.sessionId === "session-user-A")).toBe(false);
    expect(leftStore.getSnapshot().sessionId).toBe("session-user-A");
    expect(rightStore.getSnapshot().sessionId).toBe("session-user-B");
  });

  it("sends step command immediately after connection opens", () => {
    const store = createDebugStore();
    let api: ReturnType<typeof useDebugWebSocket> | undefined;
    render(
      <HookHarness
        microflowId="mf-step-response"
        sessionId="session-step-response"
        store={store}
        onReady={next => { api = next; }}
      />,
    );

    act(() => {
      api?.connect();
    });
    const socket = FakeWebSocket.instances[0];
    if (!socket) {
      throw new Error("Expected websocket instance.");
    }
    act(() => {
      socket.open();
      api?.send("step-over");
    });

    const messages = decodeMessages(socket);
    expect(messages).toEqual(expect.arrayContaining([
      expect.objectContaining({ type: "step-over" }),
    ]));
  });

  it("deduplicates repeated server messages by id", () => {
    const store = createDebugStore();
    let api: ReturnType<typeof useDebugWebSocket> | undefined;
    render(
      <HookHarness
        microflowId="mf-dedup"
        sessionId="session-dedup"
        store={store}
        onReady={next => { api = next; }}
      />,
    );

    act(() => {
      api?.connect();
    });
    const socket = FakeWebSocket.instances[0];
    if (!socket) {
      throw new Error("Expected websocket instance.");
    }
    act(() => {
      socket.open();
      socket.onmessage?.({
        data: JSON.stringify({
          id: "evt-1",
          type: DEBUG_WS_EVENTS.NODE_ENTER,
          nodeId: "node-a",
          flowId: "flow-a",
        }),
      });
      socket.onmessage?.({
        data: JSON.stringify({
          id: "evt-1",
          type: DEBUG_WS_EVENTS.NODE_ENTER,
          nodeId: "node-b",
          flowId: "flow-b",
        }),
      });
    });

    const snapshot = store.getSnapshot();
    expect(snapshot.nodeState.currentNodeId).toBe("node-a");
    expect(snapshot.nodeState.currentFlowId).toBe("flow-a");
  });

  it("restores runtime state from state-sync payload", () => {
    const store = createDebugStore();
    let api: ReturnType<typeof useDebugWebSocket> | undefined;
    render(
      <HookHarness
        microflowId="mf-state-sync"
        sessionId="session-state-sync"
        store={store}
        onReady={next => { api = next; }}
      />,
    );

    act(() => {
      api?.connect();
    });
    const socket = FakeWebSocket.instances[0];
    if (!socket) {
      throw new Error("Expected websocket instance.");
    }
    act(() => {
      socket.open();
      socket.onmessage?.({
        data: JSON.stringify({
          type: DEBUG_WS_EVENTS.STATE_SYNC,
          id: "evt-state-sync",
          data: {
            nodeStatuses: { "node-sync": "running" },
            executedEdgeIds: ["edge-sync"],
            variables: [{ name: "v1", valuePreview: "42", type: "integer" }],
            breakpoints: [{ nodeId: "node-sync", enabled: true }],
            callStack: [{ runId: "run-sync", microflowId: "mf-state-sync", depth: 0, callerObjectId: "call-order-submit", status: "paused" }],
          },
        }),
      });
    });

    const snapshot = store.getSnapshot();
    expect(snapshot.nodeStatuses["node-sync"]).toBe("running");
    expect(snapshot.executedEdgeIds).toContain("edge-sync");
    expect(snapshot.variables).toEqual(expect.arrayContaining([expect.objectContaining({ name: "v1", value: "42" })]));
    expect(snapshot.breakpoints).toEqual(expect.arrayContaining([expect.objectContaining({ nodeId: "node-sync", enabled: true })]));
    expect(snapshot.callStack).toEqual(expect.arrayContaining([expect.objectContaining({ runId: "run-sync", microflowId: "mf-state-sync", callerNodeId: "call-order-submit", status: "paused" })]));
  });
});
