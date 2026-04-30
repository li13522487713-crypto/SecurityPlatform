import { describe, expect, it, vi } from "vitest";
import type { MicroflowRunSession } from "@atlas/microflow";

import { createHttpMicroflowRuntimeAdapter } from "./http-runtime-adapter";

function createRunSession(id: string, overrides: Partial<MicroflowRunSession> = {}): MicroflowRunSession {
  return {
    id,
    schemaId: "schema-1",
    resourceId: "mf-1",
    version: "1.0.0",
    startedAt: "2026-04-30T08:00:00.000Z",
    endedAt: "2026-04-30T08:00:01.000Z",
    status: "success",
    input: {},
    trace: [],
    logs: [],
    variables: [],
    callStack: [],
    childRuns: [],
    childRunIds: [],
    ...overrides,
  };
}

describe("createHttpMicroflowRuntimeAdapter", () => {
  it("hydrates run session, trace and debug session into a unified response", async () => {
    const apiClient = {
      post: vi.fn(async () => ({
        session: createRunSession("run-1", { trace: [{ id: "bootstrap-frame", runId: "run-1", objectId: "start", status: "success", startedAt: "2026-04-30T08:00:00.000Z", durationMs: 1 }] as MicroflowRunSession["trace"] })
      })),
      get: vi.fn(async (path: string) => {
        switch (path) {
          case "/microflows/runs/run-1":
            return createRunSession("run-1", { persistedAt: "2026-04-30T08:00:01.500Z", finalized: true, traceFrameCount: 1, hasHydratedTrace: true });
          case "/microflows/runs/run-1/trace":
            return { trace: [{ id: "hydrated-frame", runId: "run-1", objectId: "end", status: "success", startedAt: "2026-04-30T08:00:00.000Z", durationMs: 3 }] };
          case "/microflows/debug-sessions/debug-1":
            return { id: "debug-1", microflowId: "mf-1", status: "paused", state: "paused", lastUpdatedAt: "2026-04-30T08:00:01.600Z", availableCommands: ["continue"] };
          case "/microflows/debug-sessions/debug-1/variables":
            return [{ name: "$amount", type: "decimal", valuePreview: "120" }];
          case "/microflows/debug-sessions/debug-1/trace":
            return [{ id: "evt-1", kind: "pause", message: "paused", createdAt: "2026-04-30T08:00:01.600Z" }];
          default:
            throw new Error(`unexpected path ${path}`);
        }
      }),
      delete: vi.fn(),
    } as any;

    const adapter = createHttpMicroflowRuntimeAdapter({
      apiBaseUrl: "/api/v1",
      apiClient,
    });

    const response = await adapter.testRunMicroflow({
      microflowId: "mf-1",
      input: {},
      debugSessionId: "debug-1",
    });

    expect(response.session.persistedAt).toBe("2026-04-30T08:00:01.500Z");
    expect(response.session.trace[0]?.id).toBe("hydrated-frame");
    expect(response.hydration).toMatchObject({
      sessionHydrated: true,
      traceHydrated: true,
      debugSessionHydrated: true,
      degraded: false,
    });
    expect(response.debugSession).toMatchObject({ id: "debug-1", state: "paused" });
    expect(response.debugVariables?.[0]).toMatchObject({ name: "$amount" });
    expect(response.debugTrace?.[0]).toMatchObject({ id: "evt-1" });
  });

  it("extracts runtimeCommands from hydrated trace frames", async () => {
    const apiClient = {
      post: vi.fn(async () => ({
        session: createRunSession("run-3")
      })),
      get: vi.fn(async (path: string) => {
        switch (path) {
          case "/microflows/runs/run-3":
            return createRunSession("run-3", { persistedAt: "2026-04-30T08:00:01.500Z", finalized: true, traceFrameCount: 1, hasHydratedTrace: true });
          case "/microflows/runs/run-3/trace":
            return {
              trace: [{
                id: "frame-runtime-command",
                runId: "run-3",
                objectId: "show",
                actionId: "show-action",
                status: "success",
                startedAt: "2026-04-30T08:00:00.000Z",
                durationMs: 2,
                output: {
                  runtimeCommands: [
                    {
                      commandKind: "showMessage",
                      payloadJson: "{\"message\":\"hello\"}",
                      requiresClientHandling: true,
                      status: "pending",
                    }
                  ]
                }
              }]
            };
          default:
            throw new Error(`unexpected path ${path}`);
        }
      }),
      delete: vi.fn(),
    } as any;

    const adapter = createHttpMicroflowRuntimeAdapter({
      apiBaseUrl: "/api/v1",
      apiClient,
    });

    const response = await adapter.testRunMicroflow({
      microflowId: "mf-1",
      input: {},
    });

    expect(response.runtimeCommands).toEqual([
      expect.objectContaining({
        commandKind: "showMessage",
        sourceObjectId: "show",
        sourceActionId: "show-action",
      })
    ]);
  });

  it("keeps bootstrap session and marks hydration degraded when trace reload fails", async () => {
    const apiClient = {
      post: vi.fn(async () => ({
        session: createRunSession("run-2", {
          trace: [{ id: "bootstrap-frame", runId: "run-2", objectId: "start", status: "success", startedAt: "2026-04-30T08:00:00.000Z", durationMs: 1 }],
        }),
      })),
      get: vi.fn(async (path: string) => {
        switch (path) {
          case "/microflows/runs/run-2":
            return createRunSession("run-2", { persistedAt: "2026-04-30T08:00:01.500Z", finalized: true, traceFrameCount: 1 });
          case "/microflows/runs/run-2/trace":
            throw new Error("trace unavailable");
          default:
            throw new Error(`unexpected path ${path}`);
        }
      }),
      delete: vi.fn(),
    } as any;

    const adapter = createHttpMicroflowRuntimeAdapter({
      apiBaseUrl: "/api/v1",
      apiClient,
    });

    const response = await adapter.testRunMicroflow({
      microflowId: "mf-1",
      input: {},
    });

    expect(response.session.trace[0]?.id).toBe("bootstrap-frame");
    expect(response.hydration).toMatchObject({
      sessionHydrated: true,
      traceHydrated: false,
      degraded: true,
    });
    expect(response.hydration?.warning).toBeTruthy();
  });
});
