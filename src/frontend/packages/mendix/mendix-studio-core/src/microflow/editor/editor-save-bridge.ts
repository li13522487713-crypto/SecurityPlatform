import {
  type CreateMicroflowInput as RuntimeCreateMicroflowInput,
  type MicroflowApiClient,
  type MicroflowResource as RuntimeMicroflowResource,
  type MicroflowSchema,
  type PublishMicroflowPayload,
  type PublishMicroflowResponse,
  type SaveMicroflowRequest,
  type SaveMicroflowResponse
} from "@atlas/microflow";

import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import type { MicroflowResource } from "../resource/resource-types";

function unavailableRuntimeMethod(name: string): never {
  throw new Error(`${name} 需要真实 runtimeAdapter；当前 Stage 06 仅接入真实 schema 加载与保存。`);
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
    schema: resource.schema
  };
}

export function createMicroflowEditorApiClient(adapter: MicroflowResourceAdapter, resource: MicroflowResource, runtimeAdapter?: MicroflowApiClient): MicroflowApiClient {
  return {
    ...(runtimeAdapter ?? {}),
    async listMicroflows() {
      const result = await adapter.listMicroflows({});
      return result.items.map(toRuntimeResource);
    },
    async createMicroflow(input: RuntimeCreateMicroflowInput) {
      const created = await adapter.createMicroflow({
        name: input.name,
        displayName: input.name,
        description: input.description,
        moduleId: input.moduleId,
        moduleName: input.moduleName,
        tags: input.tags,
        parameters: [],
        returnType: { kind: "void" },
        template: "blank"
      });
      return toRuntimeResource(created);
    },
    async getMicroflow(id: string) {
      const loaded = await adapter.getMicroflow(id);
      if (!loaded) {
        throw new Error(`Microflow ${id} was not found.`);
      }
      return toRuntimeResource(loaded);
    },
    async loadMicroflow(id: string) {
      if (runtimeAdapter) {
        return runtimeAdapter.loadMicroflow(id);
      }
      const loaded = await adapter.getMicroflow(id);
      if (!loaded) {
        throw new Error(`Microflow ${id} was not found.`);
      }
      return loaded.schema;
    },
    async saveMicroflow(request: SaveMicroflowRequest): Promise<SaveMicroflowResponse> {
      if (resource.archived || resource.permissions?.canEdit === false) {
        throw new Error("归档或无编辑权限的微流不可保存。");
      }
      const saved = await adapter.saveMicroflowSchema(resource.id, request.schema, {
        baseVersion: resource.schemaId || resource.version,
        saveReason: "editor-save",
      });
      return {
        microflowId: saved.id,
        version: saved.version,
        savedAt: saved.updatedAt,
        nodeCount: saved.schema.objectCollection.objects.length,
        edgeCount: saved.schema.flows.length
      };
    },
    async publishMicroflow(id: string, payload: PublishMicroflowPayload = { version: resource.version, releaseNote: "", overwriteCurrent: true }): Promise<PublishMicroflowResponse> {
      const published = await adapter.publishMicroflow(id, {
        version: payload.version,
        description: payload.releaseNote,
        force: payload.overwriteCurrent,
        confirmBreakingChanges: payload.overwriteCurrent
      });
      return {
        microflowId: published.resource.id,
        publishedVersion: published.version.version,
        publishedAt: published.snapshot.publishedAt,
        resource: toRuntimeResource(published.resource)
      };
    },
    async duplicateMicroflow(id: string) {
      return toRuntimeResource(await adapter.duplicateMicroflow(id));
    },
    async deleteMicroflow(id: string) {
      await adapter.deleteMicroflow(id);
    },
    async archiveMicroflow(id: string) {
      return toRuntimeResource(await adapter.archiveMicroflow(id));
    },
    async toggleFavorite(id: string, favorite: boolean) {
      return toRuntimeResource(await adapter.toggleFavorite(id, favorite));
    },
    async getMicroflowReferences(id: string) {
      const references = await adapter.getMicroflowReferences(id);
      return references.map(reference => ({
        id: reference.id,
        sourceType: reference.sourceType === "microflow" ? "workflow" : reference.sourceType === "page" || reference.sourceType === "button" || reference.sourceType === "form" ? "lowcode-app" : reference.sourceType === "workflow" ? "workflow" : "agent",
        sourceName: reference.sourceName,
        sourceId: reference.sourceId ?? reference.sourcePath ?? reference.id,
        updatedAt: new Date().toISOString()
      }));
    },
    async validateMicroflow(request) {
      return runtimeAdapter?.validateMicroflow(request) ?? unavailableRuntimeMethod("validateMicroflow");
    },
    async testRunMicroflow(request) {
      return runtimeAdapter?.testRunMicroflow(request) ?? unavailableRuntimeMethod("testRunMicroflow");
    },
    async cancelMicroflowRun(runId: string) {
      return runtimeAdapter?.cancelMicroflowRun(runId) ?? unavailableRuntimeMethod("cancelMicroflowRun");
    },
    async getMicroflowRunSession(runId: string) {
      return runtimeAdapter?.getMicroflowRunSession(runId) ?? unavailableRuntimeMethod("getMicroflowRunSession");
    },
    async getMicroflowRunTrace(runId: string) {
      return runtimeAdapter?.getMicroflowRunTrace(runId) ?? unavailableRuntimeMethod("getMicroflowRunTrace");
    },
    async getTrace(runId: string) {
      return runtimeAdapter?.getTrace(runId) ?? unavailableRuntimeMethod("getTrace");
    }
  };
}
