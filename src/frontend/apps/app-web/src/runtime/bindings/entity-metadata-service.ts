import type { ApiResponse } from "@atlas/shared-core";
import { requestApi } from "@/services/api-core";
import type { EntityMeta, EntityRelation } from "./entity-metadata-types";
import {
  getEntityMeta as getEntityMetaCore,
  getEntityRelations as getEntityRelationsCore,
  clearMetadataCache as clearMetadataCacheCore,
  makeMetadataCacheKey,
  type RuntimeMetadataClient,
} from "@atlas/runtime-core";

const metadataCache = new Map<string, EntityMeta>();

function createMetadataClient(): RuntimeMetadataClient {
  return {
    request: async <T>(url: string, init?: { method?: string; body?: unknown }): Promise<T> => {
      const response = await requestApi<ApiResponse<T>>(
        url,
        {
          method: init?.method as "GET" | "POST" | "PUT" | "PATCH" | "DELETE" | undefined,
          body: init?.body === undefined ? undefined : JSON.stringify(init.body),
        },
      );
      return response.data as T;
    },
  };
}

export async function getEntityMeta(
  tableKey: string,
  appKey: string,
): Promise<EntityMeta> {
  const cacheKey = makeMetadataCacheKey(appKey, tableKey);
  const cached = metadataCache.get(cacheKey);
  if (cached) return cached;

  const meta = await getEntityMetaCore(tableKey, appKey, createMetadataClient());
  metadataCache.set(cacheKey, meta);
  return meta;
}

export async function getEntityRelations(
  tableKey: string,
  appKey: string,
): Promise<EntityRelation[]> {
  return getEntityRelationsCore(tableKey, appKey, createMetadataClient());
}

export function clearMetadataCache(): void {
  metadataCache.clear();
  clearMetadataCacheCore();
}
