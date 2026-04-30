/**
 * Integration/production adapter.
 * Uses backend API contracts and MicroflowApiResponse envelope.
 */
import type {
  CreateMicroflowInput as RuntimeCreateMicroflowInput,
  ListMicroflowRunsResponse,
  MicroflowApiClient as RuntimeMicroflowApiClient,
  MicroflowRunHistoryItem,
  MicroflowRunHistoryQuery,
  MicroflowReference as RuntimeMicroflowReference,
  MicroflowResource as RuntimeMicroflowResource,
  MicroflowDesignSchema,
  PublishMicroflowPayload,
  PublishMicroflowResponse,
  SaveMicroflowRequest,
  SaveMicroflowResponse,
  TestRunMicroflowResponse,
  ValidateMicroflowResponse,
} from "@atlas/microflow";
import type { MicroflowDebugAdapter, MicroflowRunSession, MicroflowTraceFrame } from "@atlas/microflow";
import type { MicroflowDebugSessionDto, MicroflowDebugTraceEventDto, MicroflowDebugVariableSnapshotDto, RunSessionViewModel } from "@atlas/microflow";

import type { MicroflowResource } from "../../resource/resource-types";
import type { GetMicroflowSchemaResponse, SaveMicroflowSchemaResponse } from "../../contracts/api/microflow-schema-api-contract";
import { MicroflowApiClient, type MicroflowApiClientOptions } from "./microflow-api-client";

export interface HttpMicroflowRuntimeAdapterOptions extends MicroflowApiClientOptions {
  apiClient?: MicroflowApiClient;
}

function normalizeRunHistoryItem(dto: MicroflowRunHistoryItem): MicroflowRunHistoryItem {
  return {
    runId: dto.runId,
    microflowId: dto.microflowId,
    status: dto.status,
    durationMs: dto.durationMs ?? 0,
    startedAt: dto.startedAt,
    completedAt: dto.completedAt,
    errorMessage: dto.errorMessage,
    summary: dto.summary,
  };
}

function normalizeRunDetail(dto: MicroflowRunSession): MicroflowRunSession {
  return {
    ...dto,
    trace: [...(dto.trace ?? [])],
    logs: [...(dto.logs ?? [])],
    childRuns: dto.childRuns?.map(child => normalizeRunDetail(child)) ?? [],
    childRunIds: [...(dto.childRunIds ?? [])],
    callStack: [...(dto.callStack ?? [])],
  };
}

function createHydrationSummary(input: {
  sessionHydrated: boolean;
  traceHydrated: boolean;
  debugSessionHydrated: boolean;
}): RunSessionViewModel["hydration"] {
  const degraded = !input.sessionHydrated || !input.traceHydrated;
  const warning = degraded
    ? "运行会话已启动，但持久化会话或 trace 回读未完全成功。"
    : undefined;
  return {
    ...input,
    degraded,
    warning,
  };
}

function extractRuntimeCommands(session: MicroflowRunSession): TestRunMicroflowResponse["runtimeCommands"] {
  return session.trace.flatMap(frame => {
    const output = frame.output;
    if (!output || typeof output !== "object" || !("runtimeCommands" in output) || !Array.isArray((output as { runtimeCommands?: unknown[] }).runtimeCommands)) {
      return [];
    }

    return ((output as { runtimeCommands?: Array<Record<string, unknown>> }).runtimeCommands ?? []).flatMap(command => {
      const commandKind = typeof command.commandKind === "string" ? command.commandKind : undefined;
      if (!commandKind) {
        return [];
      }

      return [{
        commandKind,
        sourceObjectId: typeof command.sourceObjectId === "string" ? command.sourceObjectId : frame.objectId,
        sourceActionId: typeof command.sourceActionId === "string" ? command.sourceActionId : frame.actionId,
        payloadJson: typeof command.payloadJson === "string" ? command.payloadJson : undefined,
        status: typeof command.status === "string" ? command.status : undefined,
        requiresClientHandling: typeof command.requiresClientHandling === "boolean" ? command.requiresClientHandling : undefined,
      }];
    });
  });
}

async function runWithSessionHydration(
  client: MicroflowApiClient,
  request: Parameters<RuntimeMicroflowApiClient["testRunMicroflow"]>[0],
  debugAdapter: MicroflowDebugAdapter | undefined,
): Promise<TestRunMicroflowResponse> {
  const id = request.microflowId ?? request.schema?.id;
  if (!id) {
    throw new Error("Microflow test-run requires microflowId.");
  }

  const response = await client.post<{ session: MicroflowRunSession }>(`/microflows/${encodeURIComponent(id)}/test-run`, {
    schema: request.schema,
    input: request.input,
    inputs: request.input,
    schemaId: request.schemaId,
    version: request.version,
    debug: request.debug ?? true,
    debugSessionId: request.debugSessionId,
    correlationId: request.correlationId,
    options: request.options,
  });

  const bootstrapSession = normalizeRunDetail(response.session);
  const runId = bootstrapSession.id;
  const [hydratedSessionResult, hydratedTraceResult, hydratedDebugSessionResult, hydratedVariablesResult, hydratedDebugTraceResult] = await Promise.allSettled([
    client.get<MicroflowRunSession>(`/microflows/runs/${encodeURIComponent(runId)}`),
    client.get<{ trace: MicroflowTraceFrame[] }>(`/microflows/runs/${encodeURIComponent(runId)}/trace`),
    request.debugSessionId && debugAdapter ? debugAdapter.getSession(request.debugSessionId) : Promise.resolve(undefined as MicroflowDebugSessionDto | undefined),
    request.debugSessionId && debugAdapter ? debugAdapter.listVariables(request.debugSessionId) : Promise.resolve(undefined as MicroflowDebugVariableSnapshotDto[] | undefined),
    request.debugSessionId && debugAdapter ? debugAdapter.trace(request.debugSessionId) : Promise.resolve(undefined as MicroflowDebugTraceEventDto[] | undefined),
  ]);

  const hydratedSession = hydratedSessionResult.status === "fulfilled" && hydratedSessionResult.value
    ? normalizeRunDetail(hydratedSessionResult.value)
    : undefined;
  const hydratedTrace = hydratedTraceResult.status === "fulfilled"
    ? hydratedTraceResult.value.trace
    : undefined;
  const session = {
    ...(hydratedSession ?? bootstrapSession),
    trace: hydratedTrace ?? (hydratedSession?.trace?.length ? hydratedSession.trace : bootstrapSession.trace),
    persistedAt: hydratedSession?.persistedAt ?? bootstrapSession.persistedAt ?? hydratedSession?.endedAt ?? bootstrapSession.endedAt,
    finalized: hydratedSession?.finalized ?? Boolean(hydratedSession?.endedAt ?? bootstrapSession.endedAt),
    traceFrameCount: hydratedTrace?.length ?? hydratedSession?.traceFrameCount ?? hydratedSession?.trace.length ?? bootstrapSession.trace.length,
    hasHydratedTrace: Boolean(hydratedTrace ?? hydratedSession?.hasHydratedTrace),
  } satisfies MicroflowRunSession;
  const errorCode = session.error?.code ?? session.trace?.find(frame => frame.error)?.error?.code;
  const status = errorCode?.toUpperCase().includes("UNSUPPORTED")
    ? "unsupported"
    : session.status === "success"
      ? "succeeded"
      : session.status === "failed"
        ? "failed"
        : "cancelled";
  const hydration = createHydrationSummary({
    sessionHydrated: Boolean(hydratedSession),
    traceHydrated: Boolean(hydratedTrace),
    debugSessionHydrated: hydratedDebugSessionResult.status === "fulfilled" && Boolean(hydratedDebugSessionResult.value),
  });
  const runtimeCommands = extractRuntimeCommands(session);

  return {
    runId: session.id,
    status,
    startedAt: session.startedAt,
    durationMs: session.endedAt ? Math.max(0, new Date(session.endedAt).getTime() - new Date(session.startedAt).getTime()) : 0,
    frames: session.trace,
    error: session.error,
    session,
    runtimeCommands,
    hydration,
    debugSession: hydratedDebugSessionResult.status === "fulfilled" ? hydratedDebugSessionResult.value : undefined,
    debugVariables: hydratedVariablesResult.status === "fulfilled" ? hydratedVariablesResult.value : undefined,
    debugTrace: hydratedDebugTraceResult.status === "fulfilled" ? hydratedDebugTraceResult.value : undefined,
  };
}

function toRuntimeResource(resource: MicroflowResource): RuntimeMicroflowResource {
  return {
    id: resource.id,
    name: resource.name,
    description: resource.description ?? "",
    moduleId: resource.moduleId,
    moduleName: resource.moduleName,
    ownerName: resource.ownerName ?? resource.createdBy ?? "Current User",
    tags: [...resource.tags],
    version: resource.version,
    status: resource.status,
    favorite: resource.favorite,
    createdAt: resource.createdAt,
    updatedAt: resource.updatedAt,
    lastModifiedBy: resource.updatedBy,
    schema: resource.schema,
  };
}

function toRuntimeReference(reference: import("../../references/microflow-reference-types").MicroflowReference): RuntimeMicroflowReference {
  return {
    id: reference.id,
    sourceType: reference.sourceType === "microflow" || reference.sourceType === "workflow" ? "workflow" : reference.sourceType === "page" || reference.sourceType === "button" || reference.sourceType === "form" ? "lowcode-app" : "agent",
    sourceName: reference.sourceName,
    sourceId: reference.sourceId ?? reference.sourcePath ?? reference.id,
    updatedAt: new Date().toISOString(),
  };
}

export function createHttpMicroflowRuntimeAdapter(options: HttpMicroflowRuntimeAdapterOptions): RuntimeMicroflowApiClient {
  const client = options.apiClient ?? new MicroflowApiClient(options);
  const debugAdapter: MicroflowDebugAdapter = {
    createSession(microflowId) {
      return client.post(`/microflows/${encodeURIComponent(microflowId)}/debug-sessions`, {});
    },
    getSession(sessionId) {
      return client.get(`/microflows/debug-sessions/${encodeURIComponent(sessionId)}`);
    },
    sendCommand(sessionId, command, target) {
      return client.post(`/microflows/debug-sessions/${encodeURIComponent(sessionId)}/commands`, {
        command,
        targetNodeObjectId: target?.nodeObjectId,
        targetFlowId: target?.flowId,
      });
    },
    listVariables(sessionId) {
      return client.get(`/microflows/debug-sessions/${encodeURIComponent(sessionId)}/variables`);
    },
    evaluate(sessionId, expression) {
      return client.post(`/microflows/debug-sessions/${encodeURIComponent(sessionId)}/evaluate`, { expression });
    },
    trace(sessionId) {
      return client.get(`/microflows/debug-sessions/${encodeURIComponent(sessionId)}/trace`);
    },
    deleteSession(sessionId) {
      return client.delete(`/microflows/debug-sessions/${encodeURIComponent(sessionId)}`);
    },
  };

  return {
    debugAdapter,
    async listMicroflows(query) {
      const result = await client.get<{ items: MicroflowResource[] }>("/microflows", query);
      return result.items.map(toRuntimeResource);
    },
    async createMicroflow(input: RuntimeCreateMicroflowInput) {
      const resource = await client.post<MicroflowResource>("/microflows", {
        workspaceId: options.workspaceId,
        input: {
          name: input.name,
          displayName: input.name,
          description: input.description,
          moduleId: input.moduleId,
          moduleName: input.moduleName,
          tags: input.tags,
          parameters: [],
          returnType: { kind: "void" },
          template: "blank",
        },
      });
      return toRuntimeResource(resource);
    },
    async getMicroflow(id: string) {
      return toRuntimeResource(await client.get<MicroflowResource>(`/microflows/${encodeURIComponent(id)}`));
    },
    async saveMicroflow(request: SaveMicroflowRequest): Promise<SaveMicroflowResponse> {
      const schema = request.schema as MicroflowDesignSchema;
      const saved = await client.put<SaveMicroflowSchemaResponse>(`/microflows/${encodeURIComponent(schema.id)}/schema`, {
        schema,
        saveReason: request.comment,
      });
      const resource = saved.resource;
      return {
        microflowId: resource.id,
        version: resource.version,
        savedAt: resource.updatedAt,
        nodeCount: resource.schema.workflow.nodes.length,
        edgeCount: resource.schema.workflow.edges.length,
      };
    },
    async loadMicroflow(id: string) {
      const response = await client.get<GetMicroflowSchemaResponse>(`/microflows/${encodeURIComponent(id)}/schema`);
      return response.schema;
    },
    async validateMicroflow(request) {
      const id = request.schema.id;
      const mode = (request as { mode?: "edit" | "save" | "publish" | "testRun" }).mode ?? "edit";
      const response = await client.post<{ issues: ValidateMicroflowResponse["issues"]; summary?: { errorCount: number } }>(`/microflows/${encodeURIComponent(id)}/validate`, {
        schema: request.schema,
        mode,
        includeWarnings: true,
        includeInfo: true,
      });
      const issues = response.issues.map(issue => ({
        ...issue,
        id: issue.id.startsWith(`${id}:server:`) ? issue.id : `${id}:server:${issue.id}`,
        microflowId: id,
        source: issue.source ?? "server" as const,
        blockSave: issue.blockSave ?? issue.severity === "error",
        blockPublish: issue.blockPublish ?? issue.severity === "error",
      }));
      return {
        valid: response.summary ? response.summary.errorCount === 0 : issues.every(issue => issue.severity !== "error"),
        issues,
      };
    },
    async testRunMicroflow(request): Promise<TestRunMicroflowResponse> {
      return runWithSessionHydration(client, request, debugAdapter);
    },
    async cancelMicroflowRun(runId: string) {
      return client.post<{ runId: string; status: "cancelled" | "success" | "failed" }>(`/microflows/runs/${encodeURIComponent(runId)}/cancel`, {});
    },
    async getMicroflowRunSession(runId: string) {
      const dto = await client.get<MicroflowRunSession>(`/microflows/runs/${encodeURIComponent(runId)}`);
      return normalizeRunDetail(dto);
    },
    async getMicroflowRunTrace(runId: string) {
      const result = await client.get<{ trace: MicroflowTraceFrame[] }>(`/microflows/runs/${encodeURIComponent(runId)}/trace`);
      return result.trace;
    },
    async listMicroflowRuns(microflowId: string, query: MicroflowRunHistoryQuery = {}): Promise<ListMicroflowRunsResponse> {
      const response = await client.get<ListMicroflowRunsResponse>(`/microflows/${encodeURIComponent(microflowId)}/runs`, {
        pageIndex: query.pageIndex,
        pageSize: query.pageSize,
        status: query.status,
      });
      return {
        items: (response.items ?? []).map(normalizeRunHistoryItem),
        total: response.total ?? 0,
      };
    },
    async getMicroflowRunDetail(microflowId: string, runId: string) {
      const dto = await client.get<MicroflowRunSession>(`/microflows/${encodeURIComponent(microflowId)}/runs/${encodeURIComponent(runId)}`);
      return normalizeRunDetail(dto);
    },
    async publishMicroflow(id: string, payload: PublishMicroflowPayload = { version: "1.0.0", releaseNote: "", overwriteCurrent: true }): Promise<PublishMicroflowResponse> {
      const result = await client.post<import("../../publish/microflow-publish-types").MicroflowPublishResult>(`/microflows/${encodeURIComponent(id)}/publish`, {
        version: payload.version,
        description: payload.releaseNote,
        force: payload.overwriteCurrent,
        confirmBreakingChanges: payload.overwriteCurrent,
      });
      return {
        microflowId: result.resource.id,
        publishedVersion: result.version.version,
        publishedAt: result.snapshot.publishedAt,
        resource: toRuntimeResource(result.resource),
      };
    },
    async duplicateMicroflow(id: string) {
      return toRuntimeResource(await client.post<MicroflowResource>(`/microflows/${encodeURIComponent(id)}/duplicate`, {}));
    },
    async deleteMicroflow(id: string) {
      await client.delete(`/microflows/${encodeURIComponent(id)}`);
    },
    async archiveMicroflow(id: string) {
      return toRuntimeResource(await client.post<MicroflowResource>(`/microflows/${encodeURIComponent(id)}/archive`, {}));
    },
    async toggleFavorite(id: string, favorite: boolean) {
      return toRuntimeResource(await client.post<MicroflowResource>(`/microflows/${encodeURIComponent(id)}/favorite`, { favorite }));
    },
    async getMicroflowReferences(id: string) {
      const references = await client.get<import("../../references/microflow-reference-types").MicroflowReference[]>(`/microflows/${encodeURIComponent(id)}/references`);
      return references.map(toRuntimeReference);
    },
    async getTrace(runId: string) {
      const result = await client.get<{ trace: MicroflowTraceFrame[] }>(`/microflows/runs/${encodeURIComponent(runId)}/trace`);
      return result.trace;
    },
  };
}
