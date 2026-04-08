/**
 * Entity metadata 查询服务。
 *
 * 调用后端 DynamicTables API 获取表结构元数据，
 * 用于模型驱动的 binding 配置生成。
 */

import type { ApiResponse } from "@atlas/shared-core";
import { requestApi, resolveAppHostPrefix, isDirectRuntimeMode } from "@/services/api-core";
import type { EntityFieldMeta, EntityMeta, EntityRelation } from "./entity-metadata-types";

const metadataCache = new Map<string, EntityMeta>();

export async function getEntityMeta(
  tableKey: string,
  appKey: string,
): Promise<EntityMeta> {
  const cacheKey = `${appKey}:${tableKey}`;
  const cached = metadataCache.get(cacheKey);
  if (cached) return cached;

  const prefix = isDirectRuntimeMode() ? "" : resolveAppHostPrefix(appKey);
  const encodedKey = encodeURIComponent(tableKey);

  const [tableResp, fieldsResp] = await Promise.all([
    requestApi<ApiResponse<{ tableKey: string; displayName: string }>>(
      `${prefix}/api/app/dynamic-tables/${encodedKey}`,
    ),
    requestApi<ApiResponse<EntityFieldMeta[]>>(
      `${prefix}/api/app/dynamic-tables/${encodedKey}/fields`,
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
): Promise<EntityRelation[]> {
  const prefix = isDirectRuntimeMode() ? "" : resolveAppHostPrefix(appKey);
  const encodedKey = encodeURIComponent(tableKey);
  const resp = await requestApi<ApiResponse<EntityRelation[]>>(
    `${prefix}/api/app/dynamic-tables/${encodedKey}/relations`,
  );
  return resp.data ?? [];
}

export function clearMetadataCache(): void {
  metadataCache.clear();
}
