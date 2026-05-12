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
  it("hydrates run session and trace into a unified response", async () => {
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

    expect(response.session.persistedAt).toBe("2026-04-30T08:00:01.500Z");
    expect(response.session.trace[0]?.id).toBe("hydrated-frame");
    expect(response.hydration).toMatchObject({
      sessionHydrated: true,
      traceHydrated: true,
      debugSessionHydrated: false,
      degraded: false,
    });
    expect(response.debugSession).toBeUndefined();
    expect(response.debugVariables).toBeUndefined();
    expect(response.debugTrace).toBeUndefined();
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

  it("maps enqueue/status/retry/retention endpoints", async () => {
    const apiClient = {
      post: vi.fn(async (path: string, payload?: Record<string, unknown>) => {
        if (path === "/microflows/runs:enqueue") {
          expect(payload).toMatchObject({
            resourceId: "mf-1",
            request: {
              input: { amount: 1 },
              inputs: { amount: 1 },
              debug: false,
            },
          });
          return { runId: "run-10", status: "queued" };
        }
        if (path === "/microflows/runs/run-10:retry") {
          return { newRunId: "run-11", status: "queued" };
        }
        if (path === "/microflows/runtime/retention:run") {
          expect(payload).toMatchObject({ retentionDays: 30, dryRun: true });
          return {
            cutoffAt: "2026-03-31T00:00:00.000Z",
            dryRun: true,
            candidateRunCount: 3,
            deletedRunCount: 0,
            deletedTraceCount: 0,
            deletedLogCount: 0,
            sampleRunIds: ["run-1", "run-2"],
          };
        }
        throw new Error(`unexpected post path ${path}`);
      }),
      get: vi.fn(async (path: string) => {
        if (path === "/microflows/runs/run-10/status") {
          return {
            runId: "run-10",
            status: "running",
            startedAt: "2026-04-30T08:00:00.000Z",
            endedAt: "2026-04-30T08:01:00.000Z",
          };
        }
        throw new Error(`unexpected get path ${path}`);
      }),
      delete: vi.fn(),
      put: vi.fn(),
    } as any;

    const adapter = createHttpMicroflowRuntimeAdapter({
      apiBaseUrl: "/api/v1",
      apiClient,
    });

    const queued = await adapter.enqueueMicroflowRun?.({ microflowId: "mf-1", input: { amount: 1 } });
    const status = await adapter.getMicroflowRunStatus?.("run-10");
    const retried = await adapter.retryMicroflowRun?.("run-10");
    const retention = await adapter.runRetention?.({ dryRun: true, retainDays: 30 });

    expect(queued).toMatchObject({ runId: "run-10", status: "queued" });
    expect(status).toMatchObject({ runId: "run-10", status: "running", completedAt: "2026-04-30T08:01:00.000Z" });
    expect(retried).toMatchObject({ runId: "run-11", status: "queued" });
    expect(retention).toMatchObject({
      cutoffAt: "2026-03-31T00:00:00.000Z",
      dryRun: true,
      candidateRuns: 3,
      deletedRuns: 0,
      deletedTraceFrames: 0,
      deletedLogs: 0,
      sampleRunIds: ["run-1", "run-2"],
    });
  });

  it("preserves real history metadata from list runs", async () => {
    const apiClient = {
      get: vi.fn(async (path: string) => {
        if (path === "/microflows/mf-1/runs") {
          return {
            items: [
              {
                runId: "run-history-1",
                microflowId: "mf-1",
                schemaId: "schema-parent",
                status: "failed",
                errorCode: "RuntimeCallMicroflowFailed",
                durationMs: 123,
                startedAt: "2026-04-30T08:00:00.000Z",
                completedAt: "2026-04-30T08:00:01.000Z",
                finalized: true,
                parentRunId: "run-root",
                rootRunId: "run-root",
                callFrameId: "frame-call-1",
                callDepth: 1,
                correlationId: "corr-run-history",
                traceFrameCount: 5,
                logCount: 2,
                childRunIds: ["run-child-1"],
                callStack: ["Sales.Parent", "Sales.Child"],
                callStackFrames: [
                  {
                    id: "frame-call-1",
                    runId: "run-history-1",
                    microflowId: "mf-child",
                    callerObjectId: "call-child",
                    qualifiedName: "Sales.Child",
                    depth: 1,
                    status: "failed"
                  }
                ],
                errorMessage: "child failed",
                summary: "Run failed"
              }
            ],
            total: 1,
          };
        }
        throw new Error(`unexpected get path ${path}`);
      }),
      post: vi.fn(),
      delete: vi.fn(),
      put: vi.fn(),
    } as any;

    const adapter = createHttpMicroflowRuntimeAdapter({
      apiBaseUrl: "/api/v1",
      apiClient,
    });

    const response = await adapter.listMicroflowRuns("mf-1");

    expect(response.total).toBe(1);
    expect(response.items[0]).toEqual(expect.objectContaining({
      schemaId: "schema-parent",
      errorCode: "RuntimeCallMicroflowFailed",
      callFrameId: "frame-call-1",
      traceFrameCount: 5,
      childRunIds: ["run-child-1"],
      callStack: ["Sales.Parent", "Sales.Child"],
    }));
    expect(response.items[0]?.callStackFrames?.[0]).toEqual(expect.objectContaining({
      callerObjectId: "call-child",
      qualifiedName: "Sales.Child",
    }));
  });

});
