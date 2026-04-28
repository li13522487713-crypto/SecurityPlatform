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
  MicroflowSchema,
  PublishMicroflowPayload,
  PublishMicroflowResponse,
  SaveMicroflowRequest,
  SaveMicroflowResponse,
  TestRunMicroflowResponse,
  ValidateMicroflowResponse,
} from "@atlas/microflow";
import type { MicroflowRunSession, MicroflowTraceFrame } from "@atlas/microflow";

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

  return {
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
      const schema = request.schema as MicroflowSchema;
      const saved = await client.put<SaveMicroflowSchemaResponse>(`/microflows/${encodeURIComponent(schema.id)}/schema`, {
        schema,
        saveReason: request.comment,
      });
      const resource = saved.resource;
      return {
        microflowId: resource.id,
        version: resource.version,
        savedAt: resource.updatedAt,
        nodeCount: resource.schema.objectCollection.objects.length,
        edgeCount: resource.schema.flows.length,
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
        correlationId: request.correlationId,
        options: request.options,
      });
      const session = response.session;
      const errorCode = session.error?.code ?? session.trace?.find(frame => frame.error)?.error?.code;
      const status = errorCode?.toUpperCase().includes("UNSUPPORTED")
        ? "unsupported"
        : session.status === "success"
          ? "succeeded"
          : session.status === "failed"
            ? "failed"
            : "cancelled";
      return {
        runId: session.id,
        status,
        startedAt: session.startedAt,
        durationMs: session.endedAt ? Math.max(0, new Date(session.endedAt).getTime() - new Date(session.startedAt).getTime()) : 0,
        frames: session.trace,
        error: session.error,
        session,
      };
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
