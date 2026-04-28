import { mockTestRunMicroflow, type MicroflowRunSession, type MicroflowRuntimeLog } from "@atlas/microflow";
import { HttpResponse, type HttpHandler } from "msw";

import type { TestRunMicroflowApiRequest } from "../api/microflow-runtime-api-contract";
import { createMockRunSession, getMockResource, saveMockResource } from "./mock-api-store";
import type { MicroflowContractMockStore } from "./mock-api-types";
import { notFound, ok, runFailed } from "./mock-api-response";
import { mockGet, mockPost } from "./mock-handler-utils";

function cancelSession(session: MicroflowRunSession): MicroflowRunSession {
  return {
    ...session,
    status: "cancelled",
    endedAt: session.endedAt ?? new Date().toISOString(),
    logs: [
      ...session.logs,
      {
        id: `${session.id}-cancel-log`,
        timestamp: new Date().toISOString(),
        level: "warning",
        message: "Mock run session was cancelled.",
      } satisfies MicroflowRuntimeLog,
    ],
  };
}

export function createMicroflowRuntimeMockHandlers(store: MicroflowContractMockStore): HttpHandler[] {
  return [
    ...mockPost("/api/microflows/:id/test-run", async ({ request, params }) => {
      const resource = getMockResource(store, params.id);
      if (!resource) {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      const body = await request.json() as TestRunMicroflowApiRequest;
      const schema = body.schema ?? resource.schema;
      if (new URL(request.url).searchParams.get("runFailed") === "true") {
        return HttpResponse.json(runFailed("Mocked runtime run failed."), { status: 500 });
      }
      const session = await mockTestRunMicroflow({
        schema,
        metadata: store.metadataCatalog,
        variableIndex: schema.variables,
        parameters: body.input,
        options: body.options,
      });
      createMockRunSession(store, session);
      saveMockResource(store, {
        ...resource,
        lastRunStatus: session.status === "success" ? "success" : "failed",
        lastRunAt: session.endedAt ?? session.startedAt,
      });
      return HttpResponse.json(ok({ session }));
    }),
    ...mockPost("/api/microflows/runs/:runId/cancel", ({ params }) => {
      const session = store.runSessions.get(params.runId);
      if (!session) {
        return HttpResponse.json(notFound("Microflow run session was not found."), { status: 404 });
      }
      const cancelled = cancelSession(session);
      store.runSessions.set(cancelled.id, cancelled);
      return HttpResponse.json(ok({ runId: cancelled.id, status: "cancelled" as const }));
    }),
    ...mockGet("/api/microflows/runs/:runId", ({ params }) => {
      const session = store.runSessions.get(params.runId);
      return session ? HttpResponse.json(ok(session)) : HttpResponse.json(notFound("Microflow run session was not found."), { status: 404 });
    }),
    ...mockGet("/api/microflows/runs/:runId/trace", ({ params }) => {
      const session = store.runSessions.get(params.runId);
      return session
        ? HttpResponse.json(ok({ runId: session.id, trace: session.trace, logs: session.logs }))
        : HttpResponse.json(notFound("Microflow run session was not found."), { status: 404 });
    }),
  ];
}
