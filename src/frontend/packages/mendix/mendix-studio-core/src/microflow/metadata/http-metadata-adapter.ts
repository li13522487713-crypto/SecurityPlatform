import type { MicroflowMetadataCatalog } from "@atlas/microflow/metadata";
import type { GetMicroflowMetadataRequest, MicroflowMetadataAdapter } from "@atlas/microflow/metadata";

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

  async function load(request?: GetMicroflowMetadataRequest): Promise<MicroflowMetadataCatalog> {
    const body = await client.get<GetMicroflowMetadataResponseBody>("/api/microflow-metadata", {
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
    getEntity: qualifiedName => client.get(`/api/microflow-metadata/entities/${encodeURIComponent(qualifiedName)}`),
    getEnumeration: qualifiedName => client.get(`/api/microflow-metadata/enumerations/${encodeURIComponent(qualifiedName)}`),
    getMicroflowRefs: request => client.get("/api/microflow-metadata/microflows", {
      workspaceId: request?.workspaceId ?? options.workspaceId,
      moduleId: request?.moduleId,
      includeSystem: request?.includeSystem,
      includeArchived: request?.includeArchived,
      keyword: request?.keyword,
    }),
    async getPageRefs(request) {
      const catalog = await load(request);
      return catalog.pages;
    },
    async getWorkflowRefs(request) {
      const catalog = await load(request);
      return catalog.workflows;
    },
  };
}
