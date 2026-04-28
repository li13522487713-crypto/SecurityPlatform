/**
 * Integration/production adapter.
 * Uses backend API contracts and MicroflowApiResponse envelope.
 */
import type { MicroflowAuthoringSchema } from "@atlas/microflow";

import type { MicroflowPublishImpactAnalysis, MicroflowPublishInput, MicroflowPublishResult } from "../../publish/microflow-publish-types";
import type { MicroflowReference } from "../../references/microflow-reference-types";
import type {
  MicroflowCreateInput,
  MicroflowDuplicateInput,
  MicroflowResource,
  MicroflowResourceListResult,
  MicroflowResourcePatch,
  MicroflowResourceQuery,
} from "../../resource/resource-types";
import type { MicroflowVersionDetail, MicroflowVersionDiff, MicroflowVersionSummary } from "../../versions/microflow-version-types";
import type { AnalyzeMicroflowImpactRequest, GetMicroflowReferencesRequest } from "../../contracts/api/microflow-reference-api-contract";
import type { MicroflowApiPageResult } from "../../contracts/api/api-envelope";
import type { GetMicroflowSchemaResponse, SaveMicroflowSchemaResponse } from "../../contracts/api/microflow-schema-api-contract";
import type { MicroflowResourceAdapter, SaveMicroflowSchemaOptions } from "../microflow-resource-adapter";
import { MicroflowApiClient, type MicroflowApiClientOptions, type MicroflowQuery } from "./microflow-api-client";
import { getMicroflowApiError } from "./microflow-api-error";

export interface HttpMicroflowResourceAdapterOptions extends MicroflowApiClientOptions {
  apiClient?: MicroflowApiClient;
}

function toListQuery(query?: MicroflowResourceQuery): MicroflowQuery {
  if (!query) {
    return {};
  }
  const { view: _view, ...rest } = query;
  void _view;
  return rest;
}

function toReferenceQuery(query?: GetMicroflowReferencesRequest): MicroflowQuery {
  return query ? { ...query } : {};
}

function toImpactQuery(query?: AnalyzeMicroflowImpactRequest): MicroflowQuery {
  return {
    version: query?.version,
    includeBreakingChanges: query?.includeBreakingChanges ?? true,
    includeReferences: query?.includeReferences ?? true,
  };
}

function isDevelopmentRuntime(): boolean {
  try {
    return Boolean((import.meta as { env?: { DEV?: boolean } }).env?.DEV);
  } catch {
    return false;
  }
}

export function createHttpMicroflowResourceAdapter(options: HttpMicroflowResourceAdapterOptions): MicroflowResourceAdapter {
  const client = options.apiClient ?? new MicroflowApiClient(options);

  return {
    async listMicroflows(query) {
      const result = await client.get<MicroflowApiPageResult<MicroflowResource>>("/api/microflows", toListQuery(query));
      return {
        items: result.items,
        total: result.total,
        pageIndex: result.pageIndex,
        pageSize: result.pageSize,
        hasMore: result.hasMore,
      };
    },
    async getMicroflow(id) {
      return client.get<MicroflowResource>(`/api/microflows/${encodeURIComponent(id)}`);
    },
    async getMicroflowSchema(id) {
      const response = await client.get<GetMicroflowSchemaResponse>(`/api/microflows/${encodeURIComponent(id)}/schema`);
      return response.schema;
    },
    async createMicroflow(input: MicroflowCreateInput) {
      try {
        return await client.post<MicroflowResource>("/api/microflows", { workspaceId: options.workspaceId, input });
      } catch (caught) {
        if (isDevelopmentRuntime()) {
          const apiError = getMicroflowApiError(caught);
          console.warn("[microflow-create-diagnostics]", {
            method: "POST",
            path: "/api/microflows",
            apiBaseUrl: options.apiBaseUrl,
            workspaceId: options.workspaceId,
            moduleId: input.moduleId,
            headers: {
              hasWorkspaceHeader: Boolean(options.workspaceId),
              hasTenantHeader: Boolean(options.tenantId),
              hasUserHeader: Boolean(options.currentUser?.id),
            },
            payload: {
              workspaceId: options.workspaceId,
              input: {
                name: input.name,
                moduleId: input.moduleId,
              },
            },
            status: apiError.httpStatus,
            code: apiError.code,
            traceId: apiError.traceId,
          });
        }
        throw caught;
      }
    },
    async updateMicroflow(id: string, patch: MicroflowResourcePatch) {
      return client.patch<MicroflowResource>(`/api/microflows/${encodeURIComponent(id)}`, { patch });
    },
    async saveMicroflowSchema(id: string, schema: MicroflowAuthoringSchema, saveOptions?: SaveMicroflowSchemaOptions) {
      const response = await client.put<SaveMicroflowSchemaResponse>(`/api/microflows/${encodeURIComponent(id)}/schema`, { schema, ...saveOptions });
      return response.resource;
    },
    async duplicateMicroflow(id: string, input?: MicroflowDuplicateInput) {
      return client.post<MicroflowResource>(`/api/microflows/${encodeURIComponent(id)}/duplicate`, input ?? {});
    },
    async renameMicroflow(id: string, name: string, displayName?: string) {
      return client.post<MicroflowResource>(`/api/microflows/${encodeURIComponent(id)}/rename`, { name, displayName });
    },
    async toggleFavorite(id: string, favorite: boolean) {
      return client.post<MicroflowResource>(`/api/microflows/${encodeURIComponent(id)}/favorite`, { favorite });
    },
    async archiveMicroflow(id: string) {
      return client.post<MicroflowResource>(`/api/microflows/${encodeURIComponent(id)}/archive`, {});
    },
    async restoreMicroflow(id: string) {
      return client.post<MicroflowResource>(`/api/microflows/${encodeURIComponent(id)}/restore`, {});
    },
    async deleteMicroflow(id: string) {
      await client.delete<{ id: string }>(`/api/microflows/${encodeURIComponent(id)}`);
    },
    async publishMicroflow(id: string, input: MicroflowPublishInput) {
      return client.post<MicroflowPublishResult>(`/api/microflows/${encodeURIComponent(id)}/publish`, input);
    },
    async getMicroflowReferences(id: string, query?: GetMicroflowReferencesRequest) {
      return client.get<MicroflowReference[]>(`/api/microflows/${encodeURIComponent(id)}/references`, toReferenceQuery(query));
    },
    async getMicroflowVersions(id: string) {
      return client.get<MicroflowVersionSummary[]>(`/api/microflows/${encodeURIComponent(id)}/versions`);
    },
    async getMicroflowVersionDetail(id: string, versionId: string) {
      return client.get<MicroflowVersionDetail>(`/api/microflows/${encodeURIComponent(id)}/versions/${encodeURIComponent(versionId)}`);
    },
    async rollbackMicroflowVersion(id: string, versionId: string, request?: { reason?: string }) {
      return client.post<MicroflowResource>(`/api/microflows/${encodeURIComponent(id)}/versions/${encodeURIComponent(versionId)}/rollback`, request ?? {});
    },
    async duplicateMicroflowVersion(id: string, versionId: string, input?: MicroflowDuplicateInput) {
      return client.post<MicroflowResource>(`/api/microflows/${encodeURIComponent(id)}/versions/${encodeURIComponent(versionId)}/duplicate`, input ?? {});
    },
    async analyzeMicroflowPublishImpact(id: string, query: AnalyzeMicroflowImpactRequest) {
      return client.get<MicroflowPublishImpactAnalysis>(`/api/microflows/${encodeURIComponent(id)}/impact`, toImpactQuery(query));
    },
    async compareMicroflowVersion(id: string, versionId: string) {
      return client.get<MicroflowVersionDiff>(`/api/microflows/${encodeURIComponent(id)}/versions/${encodeURIComponent(versionId)}/compare-current`);
    },
  };
}
