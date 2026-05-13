// @vitest-environment jsdom

import { useEffect } from "react";
import { act, cleanup, render } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useRuntimeWebSocket, type UseRuntimeWebSocketOptions } from "./use-runtime-ws";

class FakeWebSocket {
  static instances: FakeWebSocket[] = [];
  static OPEN = 1;
  static CONNECTING = 0;
  static CLOSING = 2;
  static CLOSED = 3;

  readonly url: string;
  readyState = FakeWebSocket.CONNECTING;
  onopen: (() => void) | null = null;
  onclose: (() => void) | null = null;
  onerror: (() => void) | null = null;
  onmessage: ((event: { data: string }) => void) | null = null;
  sent: string[] = [];

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
}

function HookHarness(props: {
  options: UseRuntimeWebSocketOptions;
  onReady: (api: ReturnType<typeof useRuntimeWebSocket>) => void;
}) {
  const api = useRuntimeWebSocket(props.options);
  useEffect(() => {
    props.onReady(api);
  }, [api, props]);
  return null;
}

async function flushPromises(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
}

describe("useRuntimeWebSocket", () => {
  const originalWebSocket = globalThis.WebSocket;
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    FakeWebSocket.instances = [];
    (globalThis as typeof globalThis & { WebSocket: typeof WebSocket }).WebSocket = FakeWebSocket as unknown as typeof WebSocket;
  });

  afterEach(() => {
    cleanup();
    (globalThis as typeof globalThis & { WebSocket: typeof WebSocket }).WebSocket = originalWebSocket;
    (globalThis as typeof globalThis & { fetch: typeof fetch }).fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("sends runtime request headers and reports 401 snapshot as transport error", async () => {
    const onTransportError = vi.fn();
    const onSnapshot = vi.fn();
    const fetchMock = vi.fn(async () => ({
      ok: false,
      status: 401,
      json: async () => ({}),
    })) as unknown as typeof fetch;
    (globalThis as typeof globalThis & { fetch: typeof fetch }).fetch = fetchMock;

    let api: ReturnType<typeof useRuntimeWebSocket> | undefined;
    render(
      <HookHarness
        options={{
          runId: "run-401",
          requestHeaders: () => ({ Authorization: "Bearer test-token" }),
          onTransportError,
          onSnapshot,
        }}
        onReady={next => { api = next; }}
      />,
    );

    await act(async () => {
      await api?.refreshSnapshot();
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const requestInit = fetchMock.mock.calls[0]?.[1] as RequestInit | undefined;
    expect(requestInit?.headers).toMatchObject({
      Accept: "application/json",
      Authorization: "Bearer test-token",
    });
    expect(onSnapshot).not.toHaveBeenCalled();
    expect(onTransportError).toHaveBeenCalledWith(expect.objectContaining({
      source: "snapshot",
      status: 401,
    }));
  });

  it("does not open websocket when snapshot returns 401", async () => {
    const fetchMock = vi.fn(async () => ({
      ok: false,
      status: 401,
      json: async () => ({}),
    })) as unknown as typeof fetch;
    (globalThis as typeof globalThis & { fetch: typeof fetch }).fetch = fetchMock;

    let api: ReturnType<typeof useRuntimeWebSocket> | undefined;
    render(
      <HookHarness
        options={{ runId: "run-no-ws-on-401" }}
        onReady={next => { api = next; }}
      />,
    );

    await act(async () => {
      api?.connect();
      await flushPromises();
    });

    expect(FakeWebSocket.instances).toHaveLength(0);
  });

  it("opens websocket after successful snapshot refresh", async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      status: 200,
      json: async () => ({
        success: true,
        data: {
          runId: "run-ok",
          status: "running",
          currentObjectId: "node-1",
          lastSequence: 0,
          nodeOverlays: {},
          flowOverlays: {},
          events: [],
        },
      }),
    })) as unknown as typeof fetch;
    (globalThis as typeof globalThis & { fetch: typeof fetch }).fetch = fetchMock;

    let api: ReturnType<typeof useRuntimeWebSocket> | undefined;
    render(
      <HookHarness
        options={{ runId: "run-ok" }}
        onReady={next => { api = next; }}
      />,
    );

    await act(async () => {
      api?.connect();
      await flushPromises();
    });

    expect(FakeWebSocket.instances).toHaveLength(1);
    expect(FakeWebSocket.instances[0]?.url).toContain("/api/v1/microflows/runs/run-ok/runtime/events/ws");
  });
});

