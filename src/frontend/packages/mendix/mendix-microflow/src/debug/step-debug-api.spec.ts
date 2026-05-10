import { describe, expect, it, vi } from "vitest";

import { MicroflowStepDebugApiClient } from "./step-debug-api";

describe("MicroflowStepDebugApiClient", () => {
  it("creates a debug session through the v1 endpoint", async () => {
    const fetcher = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        success: true,
        data: {
          id: "session-1",
          microflowId: "mf-1",
          status: "created",
          currentSafePoint: {
            nodeObjectId: "rest",
            nodeKind: "actionActivity",
            phase: "beforeRestRequest",
            callDepth: 0,
            semanticKind: "rest",
            arrivedAt: "2026-04-29T00:00:00Z",
          },
        },
      }),
    });
    const client = new MicroflowStepDebugApiClient({ fetcher: fetcher as unknown as typeof fetch });

    const session = await client.createSession("mf-1");

    expect(session.id).toBe("session-1");
    expect(session.currentSafePoint?.phase).toBe("beforeRestRequest");
    expect(session.currentSafePoint?.callDepth).toBe(0);
    expect(session.currentSafePoint?.semanticKind).toBe("rest");
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

  it("upserts node breakpoint payloads through dedicated endpoint", async () => {
    const fetcher = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        success: true,
        data: { id: "session-1", microflowId: "mf-1", status: "paused", breakpoints: [{ id: "bp-1", microflowObjectId: "node-a", scope: 0, stale: false }] },
      }),
    });
    const client = new MicroflowStepDebugApiClient({ fetcher: fetcher as unknown as typeof fetch });

    await client.upsertBreakpoint("session-1", {
      id: "bp-1",
      microflowObjectId: "node-a",
      scope: 0,
      stale: false,
      enabled: true,
      suspendPolicy: 0,
    });

    const [url, init] = fetcher.mock.calls[0];
    expect(url).toBe("/api/v1/microflows/debug-sessions/session-1/breakpoints");
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body)).toEqual({
      id: "bp-1",
      microflowObjectId: "node-a",
      scope: 0,
      stale: false,
      enabled: true,
      suspendPolicy: 0,
    });
  });

  it("removes breakpoint through dedicated endpoint", async () => {
    const fetcher = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        success: true,
        data: { id: "session-1", microflowId: "mf-1", status: "paused", breakpoints: [] },
      }),
    });
    const client = new MicroflowStepDebugApiClient({ fetcher: fetcher as unknown as typeof fetch });

    await client.removeBreakpoint("session-1", "bp-1");

    expect(fetcher).toHaveBeenCalledWith(
      "/api/v1/microflows/debug-sessions/session-1/breakpoints/bp-1",
      expect.objectContaining({ method: "DELETE" }),
    );
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

  it("updates suspend policy via dedicated endpoint", async () => {
    const fetcher = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        success: true,
        data: { sessionId: "session-1", policy: "branchOnly" },
      }),
    });
    const client = new MicroflowStepDebugApiClient({ fetcher: fetcher as unknown as typeof fetch });

    const result = await client.updateSuspendPolicy("session-1", "branchOnly");

    expect(result.policy).toBe("branchOnly");
    expect(fetcher).toHaveBeenCalledWith(
      "/api/v1/microflows/debug-sessions/session-1/suspend-policy",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("mutates debug variable through mutate endpoint", async () => {
    const fetcher = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        success: true,
        data: { sessionId: "session-1", name: "$flag", valuePreview: "true", mutated: true },
      }),
    });
    const client = new MicroflowStepDebugApiClient({ fetcher: fetcher as unknown as typeof fetch });

    const result = await client.mutateVariable("session-1", { name: "$flag", value: true });

    expect(result.mutated).toBe(true);
    const [, init] = fetcher.mock.calls[0];
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body)).toEqual({ name: "$flag", value: true });
  });
});
