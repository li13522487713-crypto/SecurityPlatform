import { describe, expect, it, vi } from "vitest";

import { MicroflowStepDebugApiClient } from "./step-debug-api";

describe("MicroflowStepDebugApiClient", () => {
  it("creates a debug session through the v1 endpoint", async () => {
    const fetcher = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        success: true,
        data: { id: "session-1", microflowId: "mf-1", status: "created" },
      }),
    });
    const client = new MicroflowStepDebugApiClient({ fetcher: fetcher as unknown as typeof fetch });

    const session = await client.createSession("mf-1");

    expect(session.id).toBe("session-1");
    expect(fetcher).toHaveBeenCalledWith("/api/v1/microflows/mf-1/debug-sessions", expect.objectContaining({ method: "POST" }));
  });

  it("sends step command payloads", async () => {
    const fetcher = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        success: true,
        data: { id: "session-1", microflowId: "mf-1", status: "stepping" },
      }),
    });
    const client = new MicroflowStepDebugApiClient({ fetcher: fetcher as unknown as typeof fetch });

    await client.sendCommand("session-1", "runToNode", { nodeObjectId: "node-b" });

    const [, init] = fetcher.mock.calls[0];
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body)).toEqual({ command: "runToNode", targetNodeObjectId: "node-b" });
  });

  it("throws envelope errors without hiding backend code", async () => {
    const fetcher = vi.fn().mockResolvedValue({
      ok: false,
      status: 403,
      statusText: "Forbidden",
      json: async () => ({
        success: false,
        error: { code: "MICROFLOW_DEBUG_SESSION_FORBIDDEN", message: "denied" },
      }),
    });
    const client = new MicroflowStepDebugApiClient({ fetcher: fetcher as unknown as typeof fetch });

    await expect(client.getSession("session-1")).rejects.toThrow("MICROFLOW_DEBUG_SESSION_FORBIDDEN: denied");
  });
});
