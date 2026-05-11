import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { MicroflowStepDebugApiClient } from "./step-debug-api";

class FakeWebSocket {
  static instances: FakeWebSocket[] = [];
  static OPEN = 1;
  static CONNECTING = 0;
  static CLOSED = 3;
  readyState = FakeWebSocket.CONNECTING;
  sent: string[] = [];
  url: string;
  onopen: (() => void) | null = null;
  onclose: (() => void) | null = null;
  onerror: (() => void) | null = null;
  onmessage: ((event: { data: string }) => void) | null = null;

  constructor(url: string) {
    this.url = url;
    FakeWebSocket.instances.push(this);
    setTimeout(() => {
      this.readyState = FakeWebSocket.OPEN;
      this.onopen?.();
    }, 0);
  }

  send(payload: string) {
    this.sent.push(payload);
  }

  close() {
    this.readyState = FakeWebSocket.CLOSED;
    this.onclose?.();
  }
}

describe("MicroflowStepDebugApiClient", () => {
  const originalWebSocket = globalThis.WebSocket;

  beforeEach(() => {
    FakeWebSocket.instances = [];
    (globalThis as typeof globalThis & { WebSocket: typeof WebSocket }).WebSocket = FakeWebSocket as unknown as typeof WebSocket;
  });

  afterEach(() => {
    (globalThis as typeof globalThis & { WebSocket: typeof WebSocket }).WebSocket = originalWebSocket;
  });

  it("creates session through websocket and sends hello", async () => {
    const client = new MicroflowStepDebugApiClient({ baseUrl: "ws://localhost:5002" });
    const session = await client.createSession("mf-1");
    const socket = FakeWebSocket.instances[0];

    expect(socket.url).toContain("/api/debug/microflow/mf-1?sessionId=");
    expect(session.microflowId).toBe("mf-1");
    expect(socket.sent.some(message => message.includes("\"type\":\"hello\""))).toBe(true);
  });

  it("sends breakpoint command over websocket", async () => {
    const client = new MicroflowStepDebugApiClient({ baseUrl: "ws://localhost:5002" });
    const session = await client.createSession("mf-1");
    const socket = FakeWebSocket.instances[0];

    await client.upsertBreakpoint(session.id, {
      id: "bp-1",
      microflowObjectId: "node-a",
      scope: 0,
      stale: false,
      enabled: true,
      suspendPolicy: 0,
    });

    expect(socket.sent.some(message => message.includes("\"type\":\"set-breakpoint\""))).toBe(true);
  });

  it("sends variable mutation over websocket", async () => {
    const client = new MicroflowStepDebugApiClient({ baseUrl: "ws://localhost:5002" });
    const session = await client.createSession("mf-1");
    const socket = FakeWebSocket.instances[0];

    const result = await client.mutateVariable(session.id, { name: "$flag", value: true });

    expect(result.mutated).toBe(true);
    expect(socket.sent.some(message => message.includes("\"type\":\"set-variable\""))).toBe(true);
  });
});
