import type { MicroflowMetadataCatalog } from "@atlas/microflow/metadata";
import type { GetMicroflowMetadataRequest, MicroflowMetadataAdapter } from "@atlas/microflow/metadata";

import type { MicroflowApiResponse } from "../contracts/api/api-envelope";
import type { GetMicroflowMetadataResponseBody } from "../contracts/api/microflow-metadata-api-contract";

export interface HttpMicroflowMetadataAdapterOptions {
  apiBaseUrl: string;
  fetchImpl?: typeof fetch;
}

/**
 * 预留：GET `{apiBaseUrl}/api/microflow-metadata`，解包 {@link MicroflowApiResponse}。
 * 默认工程仍使用 mock adapter；接入真实后端时替换为工厂返回值即可。
 */
export function createHttpMicroflowMetadataAdapter(options: HttpMicroflowMetadataAdapterOptions): MicroflowMetadataAdapter {
  const base = options.apiBaseUrl.replace(/\/$/, "");
  const fetchFn = options.fetchImpl ?? fetch;

  async function load(request?: GetMicroflowMetadataRequest): Promise<MicroflowMetadataCatalog> {
    const params = new URLSearchParams();
    if (request?.workspaceId) {
      params.set("workspaceId", request.workspaceId);
    }
    if (request?.moduleId) {
      params.set("moduleId", request.moduleId);
    }
    if (request?.includeSystem !== undefined) {
      params.set("includeSystem", String(request.includeSystem));
    }
    if (request?.includeArchived !== undefined) {
      params.set("includeArchived", String(request.includeArchived));
    }
    const query = params.toString();
    const url = `${base}/api/microflow-metadata${query ? `?${query}` : ""}`;
    const response = await fetchFn(url, { method: "GET", headers: { Accept: "application/json" } });
    if (!response.ok) {
      throw new Error(`Microflow metadata HTTP ${response.status} ${response.statusText}`);
    }
    const envelope = await response.json() as MicroflowApiResponse<GetMicroflowMetadataResponseBody>;
    if (!envelope.success || !envelope.data) {
      const message = envelope.error?.message ?? "Microflow metadata response missing data";
      throw new Error(message);
    }
    const { updatedAt: _updatedAt, catalogVersion: _catalogVersion, ...catalog } = envelope.data;
    void _updatedAt;
    void _catalogVersion;
    return catalog as MicroflowMetadataCatalog;
  }

  return {
    getMetadataCatalog: load,
    refreshMetadataCatalog: load,
  };
}
