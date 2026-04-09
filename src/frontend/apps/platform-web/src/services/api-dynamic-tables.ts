import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import { getProjectId } from "@atlas/shared-core";
import { createSharedDynamicTablesApi } from "@atlas/shared-biz";
import type { AppScopedDynamicTableListItem as SharedAppScopedDynamicTableListItem } from "@atlas/shared-biz";
import { requestApi } from "./api-core";
import type { RequestOptions } from "./api-core";
import type {
  DynamicFieldDefinition,
  DynamicFieldPermissionRule,
  DynamicFieldPermissionUpsertRequest,
  DynamicRelationDefinition,
  DynamicRelationUpsertRequest,
  DynamicTableCreateRequest,
  DynamicTableListItem,
  DynamicTableSummary
} from "@/types/dynamic-tables";
export type AppScopedDynamicTableListItem = SharedAppScopedDynamicTableListItem;

export interface DeleteCheckBlocker {
  type: "form" | "page" | "approval" | "relation" | "view" | "etlJob";
  id: string;
  name: string;
  path?: string;
}

export interface DeleteCheckResult {
  canDelete: boolean;
  blockers: DeleteCheckBlocker[];
  warnings: string[];
}

const sharedDynamicTablesApi = createSharedDynamicTablesApi<
  DynamicTableListItem,
  DynamicTableSummary,
  DynamicFieldDefinition,
  DynamicTableCreateRequest,
  DeleteCheckResult,
  { id: string }
>({
  requestApi
});

export const getDynamicTablesPaged = (
  pagedRequest: PagedRequest,
  options?: RequestInit & RequestOptions
): Promise<PagedResult<DynamicTableListItem>> =>
  sharedDynamicTablesApi.getDynamicTablesPaged(pagedRequest, options);

export const getAllDynamicTables = (
  keyword?: string,
  options?: RequestInit & RequestOptions
): Promise<DynamicTableListItem[]> => sharedDynamicTablesApi.getAllDynamicTables(keyword, options);

export const getAppScopedDynamicTables = (
  appId: string,
  keyword?: string
): Promise<AppScopedDynamicTableListItem[]> =>
  sharedDynamicTablesApi.getAppScopedDynamicTables(appId, keyword);

export function getCurrentProjectAppId(): string | null {
  const projectId = getProjectId();
  if (!projectId) {
    return null;
  }
  const normalized = projectId.trim();
  return normalized.length > 0 ? normalized : null;
}

export const getDynamicTableSummary = (tableKey: string): Promise<DynamicTableSummary | null> =>
  sharedDynamicTablesApi.getDynamicTableSummary(tableKey);

export const getDynamicTableFields = (tableKey: string): Promise<DynamicFieldDefinition[]> =>
  sharedDynamicTablesApi.getDynamicTableFields(tableKey);

export async function getDynamicTableFieldsBatch(
  tableKeys: string[]
): Promise<Record<string, DynamicFieldDefinition[]>> {
  const uniqueKeys = Array.from(new Set(tableKeys.map((key) => key.trim()).filter((key) => key.length > 0)));
  const result: Record<string, DynamicFieldDefinition[]> = {};
  await Promise.all(
    uniqueKeys.map(async (tableKey) => {
      result[tableKey] = await getDynamicTableFields(tableKey);
    })
  );
  return result;
}

export async function getDynamicFieldPermissions(tableKey: string): Promise<DynamicFieldPermissionRule[]> {
  const response = await requestApi<ApiResponse<DynamicFieldPermissionRule[]>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/field-permissions`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function setDynamicFieldPermissions(
  tableKey: string,
  request: DynamicFieldPermissionUpsertRequest
) {
  const response = await requestApi<ApiResponse<{ tableKey: string }>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/field-permissions`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function getDynamicTableRelations(tableKey: string): Promise<DynamicRelationDefinition[]> {
  const response = await requestApi<ApiResponse<DynamicRelationDefinition[]>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/relations`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function setDynamicTableRelations(tableKey: string, request: DynamicRelationUpsertRequest) {
  const response = await requestApi<ApiResponse<{ tableKey: string }>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/relations`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export const createDynamicTable = (request: DynamicTableCreateRequest): Promise<{ id: string }> =>
  sharedDynamicTablesApi.createDynamicTable(request);

export const deleteDynamicTable = (tableKey: string): Promise<void> =>
  sharedDynamicTablesApi.deleteDynamicTable(tableKey);

export const getDynamicTableDeleteCheck = (tableKey: string): Promise<DeleteCheckResult> =>
  sharedDynamicTablesApi.getDynamicTableDeleteCheck(tableKey);

export const archiveDynamicTable = (tableKey: string): Promise<void> =>
  sharedDynamicTablesApi.archiveDynamicTable(tableKey);

export const restoreDynamicTable = (tableKey: string): Promise<void> =>
  sharedDynamicTablesApi.restoreDynamicTable(tableKey);
