/**
 * 统一数据访问层。
 *
 * 支持两套数据路由：
 *   1. PageRuntime records：通过 pageKey 路由（兼容 Phase 1）
 *   2. DynamicTable records：通过 tableKey 路由（Phase 2 模型驱动）
 */

import type { ApiResponse } from "@atlas/shared-core";
import { requestApi, resolveAppHostPrefix, isDirectRuntimeMode } from "@/services/api-core";

export interface RuntimeDataQueryParams {
  pageKey: string;
  appKey: string;
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

export interface EntityDataQueryParams {
  tableKey: string;
  appKey: string;
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  sortBy?: string;
  sortDesc?: boolean;
  filters?: Array<{
    field: string;
    operator: string;
    value: unknown;
  }>;
}

function resolvePrefix(appKey: string): string {
  return isDirectRuntimeMode() ? "" : resolveAppHostPrefix(appKey);
}

export async function queryRuntimeRecords(
  params: RuntimeDataQueryParams,
): Promise<unknown> {
  const { pageKey, appKey, pageIndex = 1, pageSize = 20, keyword, sortBy, sortDesc } = params;

  const encodedPageKey = encodeURIComponent(pageKey);
  const prefix = resolvePrefix(appKey);
  const basePath = `${prefix}/api/app/runtime/pages/${encodedPageKey}/records`;

  const queryParams = new URLSearchParams({
    pageIndex: pageIndex.toString(),
    pageSize: pageSize.toString(),
  });
  if (keyword) queryParams.set("keyword", keyword);
  if (sortBy) queryParams.set("sortBy", sortBy);
  if (sortDesc !== undefined) queryParams.set("sortDesc", String(sortDesc));

  const resp = await requestApi<ApiResponse<unknown>>(
    `${basePath}?${queryParams.toString()}`,
  );
  return resp.data;
}

export async function getRuntimeRecord(
  pageKey: string,
  appKey: string,
  recordId: string,
): Promise<unknown> {
  const encodedPageKey = encodeURIComponent(pageKey);
  const prefix = resolvePrefix(appKey);
  const path = `${prefix}/api/app/runtime/pages/${encodedPageKey}/records/${recordId}`;
  const resp = await requestApi<ApiResponse<unknown>>(path);
  return resp.data;
}

/**
 * Phase 2: 通过 tableKey 直接查询动态表记录（模型驱动）。
 */
export async function queryEntityRecords(
  params: EntityDataQueryParams,
): Promise<unknown> {
  const { tableKey, appKey, pageIndex = 1, pageSize = 20, keyword, sortBy, sortDesc, filters } = params;

  const encodedKey = encodeURIComponent(tableKey);
  const prefix = resolvePrefix(appKey);
  const path = `${prefix}/api/app/dynamic-tables/${encodedKey}/records/query`;

  const resp = await requestApi<ApiResponse<unknown>>(path, {
    method: "POST",
    body: JSON.stringify({
      pageIndex,
      pageSize,
      keyword: keyword ?? "",
      sortBy: sortBy ?? "",
      sortDesc: sortDesc ?? false,
      filters: filters ?? [],
    }),
  });

  return resp.data;
}

export async function getEntityRecord(
  tableKey: string,
  appKey: string,
  recordId: string | number,
): Promise<unknown> {
  const encodedKey = encodeURIComponent(tableKey);
  const prefix = resolvePrefix(appKey);
  const path = `${prefix}/api/app/dynamic-tables/${encodedKey}/records/${recordId}`;
  const resp = await requestApi<ApiResponse<unknown>>(path);
  return resp.data;
}

export async function createEntityRecord(
  tableKey: string,
  appKey: string,
  data: Record<string, unknown>,
): Promise<unknown> {
  const encodedKey = encodeURIComponent(tableKey);
  const prefix = resolvePrefix(appKey);
  const path = `${prefix}/api/app/dynamic-tables/${encodedKey}/records`;
  const resp = await requestApi<ApiResponse<unknown>>(path, {
    method: "POST",
    body: JSON.stringify(data),
  });
  return resp.data;
}

export async function updateEntityRecord(
  tableKey: string,
  appKey: string,
  recordId: string | number,
  data: Record<string, unknown>,
): Promise<unknown> {
  const encodedKey = encodeURIComponent(tableKey);
  const prefix = resolvePrefix(appKey);
  const path = `${prefix}/api/app/dynamic-tables/${encodedKey}/records/${recordId}`;
  const resp = await requestApi<ApiResponse<unknown>>(path, {
    method: "PUT",
    body: JSON.stringify(data),
  });
  return resp.data;
}
