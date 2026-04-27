/**
 * Integration/production adapter.
 * Uses backend API contracts and MicroflowApiResponse envelope.
 */
import type {
  CreateMicroflowInput as RuntimeCreateMicroflowInput,
  MicroflowApiClient as RuntimeMicroflowApiClient,
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
      const result = await client.get<{ items: MicroflowResource[] }>("/api/microflows", query);
      return result.items.map(toRuntimeResource);
    },
    async createMicroflow(input: RuntimeCreateMicroflowInput) {
      const resource = await client.post<MicroflowResource>("/api/microflows", {
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
      return toRuntimeResource(await client.get<MicroflowResource>(`/api/microflows/${encodeURIComponent(id)}`));
    },
    async saveMicroflow(request: SaveMicroflowRequest): Promise<SaveMicroflowResponse> {
      const schema = request.schema as MicroflowSchema;
      const saved = await client.put<SaveMicroflowSchemaResponse>(`/api/microflows/${encodeURIComponent(schema.id)}/schema`, {
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
      const response = await client.get<GetMicroflowSchemaResponse>(`/api/microflows/${encodeURIComponent(id)}/schema`);
      return response.schema;
    },
    async validateMicroflow(request) {
      const id = request.schema.id;
      const response = await client.post<{ issues: ValidateMicroflowResponse["issues"]; summary?: { errorCount: number } }>(`/api/microflows/${encodeURIComponent(id)}/validate`, {
        schema: request.schema,
        mode: "edit",
        includeWarnings: true,
        includeInfo: true,
      });
      return {
        valid: response.summary ? response.summary.errorCount === 0 : response.issues.every(issue => issue.severity !== "error"),
        issues: response.issues,
      };
    },
    async testRunMicroflow(request): Promise<TestRunMicroflowResponse> {
      const id = request.microflowId ?? request.schema.id;
      const response = await client.post<{ session: MicroflowRunSession }>(`/api/microflows/${encodeURIComponent(id)}/test-run`, {
        schema: request.schema,
        input: request.input,
        options: request.options,
      });
      const session = response.session;
      return {
        runId: session.id,
        status: session.status === "success" ? "succeeded" : session.status === "failed" ? "failed" : "cancelled",
        startedAt: session.startedAt,
        durationMs: session.endedAt ? Math.max(0, new Date(session.endedAt).getTime() - new Date(session.startedAt).getTime()) : 0,
        frames: session.trace,
        error: session.error,
        session,
      };
    },
    async cancelMicroflowRun(runId: string) {
      return client.post<{ runId: string; status: "cancelled" | "success" | "failed" }>(`/api/microflows/runs/${encodeURIComponent(runId)}/cancel`, {});
    },
    async getMicroflowRunSession(runId: string) {
      return client.get<MicroflowRunSession>(`/api/microflows/runs/${encodeURIComponent(runId)}`);
    },
    async getMicroflowRunTrace(runId: string) {
      const result = await client.get<{ trace: MicroflowTraceFrame[] }>(`/api/microflows/runs/${encodeURIComponent(runId)}/trace`);
      return result.trace;
    },
    async publishMicroflow(id: string, payload: PublishMicroflowPayload = { version: "1.0.0", releaseNote: "", overwriteCurrent: true }): Promise<PublishMicroflowResponse> {
      const result = await client.post<import("../../publish/microflow-publish-types").MicroflowPublishResult>(`/api/microflows/${encodeURIComponent(id)}/publish`, {
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
      return toRuntimeResource(await client.post<MicroflowResource>(`/api/microflows/${encodeURIComponent(id)}/duplicate`, {}));
    },
    async deleteMicroflow(id: string) {
      await client.delete(`/api/microflows/${encodeURIComponent(id)}`);
    },
    async archiveMicroflow(id: string) {
      return toRuntimeResource(await client.post<MicroflowResource>(`/api/microflows/${encodeURIComponent(id)}/archive`, {}));
    },
    async toggleFavorite(id: string, favorite: boolean) {
      return toRuntimeResource(await client.post<MicroflowResource>(`/api/microflows/${encodeURIComponent(id)}/favorite`, { favorite }));
    },
    async getMicroflowReferences(id: string) {
      const references = await client.get<import("../../references/microflow-reference-types").MicroflowReference[]>(`/api/microflows/${encodeURIComponent(id)}/references`);
      return references.map(toRuntimeReference);
    },
    async getTrace(runId: string) {
      const result = await client.get<{ trace: MicroflowTraceFrame[] }>(`/api/microflows/runs/${encodeURIComponent(runId)}/trace`);
      return result.trace;
    },
  };
}
