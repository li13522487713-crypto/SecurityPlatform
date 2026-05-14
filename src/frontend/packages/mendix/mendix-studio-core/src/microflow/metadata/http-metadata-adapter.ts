/**
 * Integration/production adapter.
 * Uses backend API contracts and MicroflowApiResponse envelope.
 */
import type { MicroflowMetadataCatalog } from "@atlas/microflow/metadata";
import type { GetMicroflowMetadataRequest, MicroflowMetadataAdapter, GetDatabaseSourcesRequest } from "@atlas/microflow/metadata";

import type { GetMicroflowMetadataResponseBody } from "../contracts/api/microflow-metadata-api-contract";
import { MicroflowApiClient, type MicroflowApiClientOptions } from "../adapter/http/microflow-api-client";

export interface HttpMicroflowMetadataAdapterOptions extends MicroflowApiClientOptions {
  apiClient?: MicroflowApiClient;
}

/**
 * HTTP metadata adapter：integration/production path。
 * 失败时抛出统一 API error，不在内部 fallback 到 mock。
 */
export function createHttpMicroflowMetadataAdapter(options: HttpMicroflowMetadataAdapterOptions): MicroflowMetadataAdapter {
  const client = options.apiClient ?? new MicroflowApiClient(options);
  const appId = (options as HttpMicroflowMetadataAdapterOptions & { appId?: string }).appId;

  async function load(request?: GetMicroflowMetadataRequest): Promise<MicroflowMetadataCatalog> {
    const body = await client.get<GetMicroflowMetadataResponseBody>("/microflow-metadata", {
      appId: appId,
      workspaceId: request?.workspaceId ?? options.workspaceId,
      moduleId: request?.moduleId,
      includeSystem: request?.includeSystem,
      includeArchived: request?.includeArchived,
    });
    const { updatedAt: _updatedAt, catalogVersion: _catalogVersion, ...catalog } = body;
    void _updatedAt;
    void _catalogVersion;
    return catalog as MicroflowMetadataCatalog;
  }

  return {
    getMetadataCatalog: load,
    refreshMetadataCatalog: load,
    getEntity: qualifiedName => client.get(`/microflow-metadata/entities/${encodeURIComponent(qualifiedName)}`, {
      appId,
      workspaceId: options.workspaceId,
    }),
    getEnumeration: qualifiedName => client.get(`/microflow-metadata/enumerations/${encodeURIComponent(qualifiedName)}`, {
      appId,
      workspaceId: options.workspaceId,
    }),
    getMicroflowRefs: request => client.get("/microflow-metadata/microflows", {
      appId,
      workspaceId: request?.workspaceId ?? options.workspaceId,
      moduleId: request?.moduleId,
      includeSystem: request?.includeSystem,
      includeArchived: request?.includeArchived,
      keyword: request?.keyword,
      status: request?.status,
    }),
    getPageRefs: request => client.get("/microflow-metadata/pages", {
      appId,
      workspaceId: request?.workspaceId ?? options.workspaceId,
      moduleId: request?.moduleId,
      keyword: request?.keyword,
    }),
    getWorkflowRefs: request => client.get("/microflow-metadata/workflows", {
      appId,
      workspaceId: request?.workspaceId ?? options.workspaceId,
      moduleId: request?.moduleId,
      keyword: request?.keyword,
    }),
    getDatabaseSources: async (request?: GetDatabaseSourcesRequest) => {
      try {
        const result = await client.get<{ items: unknown[] } | unknown[]>("/database-center/sources", {
          workspaceId: request?.workspaceId ?? options.workspaceId,
          keyword: request?.keyword,
          pageSize: 200,
        });
        const items = Array.isArray(result) ? result : (result as { items: unknown[] }).items ?? [];
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        return items as any[];
      } catch {
        return [];
      }
    },
    getDatabaseSchemaStructure: (sourceId: string, schemaName?: string) => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      return client.get<any>(`/database-center/sources/${encodeURIComponent(sourceId)}/schemas/${encodeURIComponent(schemaName ?? "default")}/structure`, {
        workspaceId: options.workspaceId,
      });
    },
    previewDatabaseSql: (sourceId: string, sql: string, schemaName?: string) => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      return client.post<any>("/database-center/sql/preview", {
        sourceId,
        sql,
        schema: schemaName,
      });
    },
  };
}
