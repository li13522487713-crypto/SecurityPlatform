import type { ApiResponse } from "@atlas/shared-core";
import type { EntityFieldMeta, EntityMeta, EntityRelation } from "./entity-metadata-types";

const metadataCache = new Map<string, EntityMeta>();

export interface RuntimeMetadataClient {
  request<T>(url: string, init?: { method?: string; body?: unknown }): Promise<T>;
}

export function makeMetadataCacheKey(appKey: string, tableKey: string) {
  return `${appKey}:${tableKey}`;
}

export async function getEntityMeta(
  tableKey: string,
  appKey: string,
  client: RuntimeMetadataClient,
): Promise<EntityMeta> {
  const cacheKey = makeMetadataCacheKey(appKey, tableKey);
  const cached = metadataCache.get(cacheKey);
  if (cached) return cached;

  const encodedKey = encodeURIComponent(tableKey);
  const [tableResp, fieldsResp] = await Promise.all([
    client.request<ApiResponse<{ tableKey: string; displayName: string }>>(
      `/api/app/dynamic-tables/${encodedKey}`,
    ),
    client.request<ApiResponse<EntityFieldMeta[]>>(
      `/api/app/dynamic-tables/${encodedKey}/fields`,
    ),
  ]);

  const meta: EntityMeta = {
    tableKey,
    displayName: tableResp.data?.displayName ?? tableKey,
    fields: fieldsResp.data ?? [],
  };

  metadataCache.set(cacheKey, meta);
  return meta;
}

export async function getEntityRelations(
  tableKey: string,
  appKey: string,
  client: RuntimeMetadataClient,
): Promise<EntityRelation[]> {
  void appKey;
  const encodedKey = encodeURIComponent(tableKey);
  const response = await client.request<ApiResponse<EntityRelation[]>>(
    `/api/app/dynamic-tables/${encodedKey}/relations`,
  );
  return response.data ?? [];
}

export function clearMetadataCache(): void {
  metadataCache.clear();
}
