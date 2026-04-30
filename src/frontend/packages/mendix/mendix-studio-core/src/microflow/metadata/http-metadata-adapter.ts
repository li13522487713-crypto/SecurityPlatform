/**
 * Integration/production adapter.
 * Uses backend API contracts and MicroflowApiResponse envelope.
 */
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
  };
}
