import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core";
import { requestApi, resolveAppHostPrefix } from "./api-core";
import type { RequestOptions } from "./api-core";
import type {
  AppScopedDynamicTableListItem,
  DeleteCheckResult,
  DynamicFieldDefinition,
  DynamicRecordDto,
  DynamicRecordListResult,
  DynamicRecordQueryRequest,
  DynamicRecordUpsertRequest,
  DynamicTableAlterPreviewResponse,
  DynamicTableAlterRequest,
  DynamicTableCreateRequest,
  DynamicTableListItem,
  DynamicTableSummary
} from "@/types/dynamic-tables";

const DYNAMIC_TABLES_MAX_PAGE_SIZE = 200;

interface SharedDynamicTablesApi {
  getDynamicTablesPaged: (
    pagedRequest: PagedRequest,
    options?: RequestInit & RequestOptions
  ) => Promise<PagedResult<DynamicTableListItem>>;
  getAllDynamicTables: (
    keyword?: string,
    options?: RequestInit & RequestOptions
  ) => Promise<DynamicTableListItem[]>;
  getAppScopedDynamicTables: (appId: string, keyword?: string) => Promise<AppScopedDynamicTableListItem[]>;
  getDynamicTableSummary: (tableKey: string, init?: RequestInit) => Promise<DynamicTableSummary | null>;
  getDynamicTableFields: (tableKey: string, init?: RequestInit) => Promise<DynamicFieldDefinition[]>;
  createDynamicTable: (
    request: DynamicTableCreateRequest,
    init?: RequestInit
  ) => Promise<{ id: string; tableKey: string }>;
  deleteDynamicTable: (tableKey: string, init?: RequestInit) => Promise<void>;
  getDynamicTableDeleteCheck: (tableKey: string) => Promise<DeleteCheckResult>;
  archiveDynamicTable: (tableKey: string) => Promise<void>;
  restoreDynamicTable: (tableKey: string) => Promise<void>;
}

function buildAppIdHeaders(appId?: string): Record<string, string> {
  const headers: Record<string, string> = {};
  const trimmed = appId?.trim();
  if (trimmed && /^\d+$/.test(trimmed) && trimmed !== "0") {
    headers["X-App-Id"] = trimmed;
    headers["X-App-Workspace"] = "1";
  }
  return headers;
}

// ---------------------------------------------------------------------------
// Table metadata (AppHost: /api/v1/dynamic-tables)
// ---------------------------------------------------------------------------

function createSharedDynamicTablesApi(): SharedDynamicTablesApi {
  async function getDynamicTablesPaged(
    pagedRequest: PagedRequest,
    options?: RequestInit & RequestOptions
  ): Promise<PagedResult<DynamicTableListItem>> {
    const queryParams = new URLSearchParams({
      PageIndex: pagedRequest.pageIndex.toString(),
      PageSize: pagedRequest.pageSize.toString()
    });
    const keyword = pagedRequest.keyword?.trim();
    if (keyword) {
      queryParams.set("Keyword", keyword);
    }

    const response = await requestApi<ApiResponse<PagedResult<DynamicTableListItem>>>(
      `/dynamic-tables?${queryParams.toString()}`,
      undefined,
      options
    );
    if (!response.data) {
      throw new Error(response.message || "查询失败");
    }
    return response.data;
  }

  async function getAllDynamicTables(
    keyword?: string,
    options?: RequestInit & RequestOptions
  ): Promise<DynamicTableListItem[]> {
    const collected: DynamicTableListItem[] = [];
    let pageIndex = 1;
    while (true) {
      const response = await getDynamicTablesPaged(
        { pageIndex, pageSize: DYNAMIC_TABLES_MAX_PAGE_SIZE, keyword: keyword ?? "" },
        options
      );
      collected.push(...response.items);
      if (
        response.items.length < DYNAMIC_TABLES_MAX_PAGE_SIZE ||
        collected.length >= response.total
      ) {
        break;
      }

      pageIndex += 1;
    }

    return collected;
  }

  async function getAppScopedDynamicTables(
    appId: string,
    keyword?: string
  ): Promise<AppScopedDynamicTableListItem[]> {
    const parsedAppId = Number(appId);
    if (!Number.isFinite(parsedAppId) || parsedAppId <= 0) {
      return [];
    }

    const queryParams = new URLSearchParams();
    const normalizedKeyword = keyword?.trim();
    if (normalizedKeyword) {
      queryParams.set("keyword", normalizedKeyword);
    }

    const query = queryParams.toString();
    const endpoint = `/api/v2/tenant-app-instances/${parsedAppId}/roles/available-dynamic-tables${query ? `?${query}` : ""}`;
    const response = await requestApi<ApiResponse<Array<{ tableKey: string; displayName: string }>>>(endpoint);
    const items = response.data ?? [];
    return items.map((item) => ({
      tableKey: item.tableKey,
      displayName: item.displayName
    }));
  }

  async function getDynamicTableSummary(
    tableKey: string,
    init?: RequestInit
  ): Promise<DynamicTableSummary | null> {
    const response = await requestApi<ApiResponse<DynamicTableSummary | null>>(
      `/dynamic-tables/${encodeURIComponent(tableKey)}/summary`,
      init
    );
    return response.data ?? null;
  }

  async function getDynamicTableFields(
    tableKey: string,
    init?: RequestInit
  ): Promise<DynamicFieldDefinition[]> {
    const response = await requestApi<ApiResponse<DynamicFieldDefinition[]>>(
      `/dynamic-tables/${encodeURIComponent(tableKey)}/fields`,
      init
    );
    if (!response.data) {
      throw new Error(response.message || "查询失败");
    }
    return response.data;
  }

  async function createDynamicTable(
    request: DynamicTableCreateRequest,
    init?: RequestInit
  ): Promise<{ id: string; tableKey: string }> {
    const headers = new Headers(init?.headers);
    headers.set("Content-Type", "application/json");
    const response = await requestApi<ApiResponse<{ id: string; tableKey: string }>>("/dynamic-tables", {
      ...init,
      method: "POST",
      headers,
      body: JSON.stringify(request)
    });
    if (!response.data) {
      throw new Error(response.message || "创建失败");
    }
    return response.data;
  }

  async function deleteDynamicTable(tableKey: string, init?: RequestInit): Promise<void> {
    const response = await requestApi<ApiResponse<unknown>>(
      `/dynamic-tables/${encodeURIComponent(tableKey)}`,
      {
        method: "DELETE",
        ...init
      }
    );
    if (!response.success) {
      throw new Error(response.message || "删除失败");
    }
  }

  async function getDynamicTableDeleteCheck(tableKey: string): Promise<DeleteCheckResult> {
    const response = await requestApi<ApiResponse<DeleteCheckResult>>(
      `/dynamic-tables/${encodeURIComponent(tableKey)}/delete-check`
    );
    if (!response.data) {
      throw new Error(response.message || "删除检查失败");
    }
    return response.data;
  }

  async function archiveDynamicTable(tableKey: string): Promise<void> {
    const response = await requestApi<ApiResponse<null>>(
      `/dynamic-tables/${encodeURIComponent(tableKey)}/archive`,
      { method: "PATCH" }
    );
    if (!response.success) {
      throw new Error(response.message || "归档失败");
    }
  }

  async function restoreDynamicTable(tableKey: string): Promise<void> {
    const response = await requestApi<ApiResponse<null>>(
      `/dynamic-tables/${encodeURIComponent(tableKey)}/restore`,
      { method: "PATCH" }
    );
    if (!response.success) {
      throw new Error(response.message || "恢复失败");
    }
  }

  return {
    getDynamicTablesPaged,
    getAllDynamicTables,
    getAppScopedDynamicTables,
    getDynamicTableSummary,
    getDynamicTableFields,
    createDynamicTable,
    deleteDynamicTable,
    getDynamicTableDeleteCheck,
    archiveDynamicTable,
    restoreDynamicTable
  };
}

const sharedDynamicTablesApi = createSharedDynamicTablesApi();

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

export const getDynamicTableSummary = (
  tableKey: string,
  appId?: string
): Promise<DynamicTableSummary | null> =>
  sharedDynamicTablesApi.getDynamicTableSummary(tableKey, {
    headers: { ...buildAppIdHeaders(appId) }
  });

export const getDynamicTableFields = (
  tableKey: string,
  appId?: string
): Promise<DynamicFieldDefinition[]> =>
  sharedDynamicTablesApi.getDynamicTableFields(tableKey, {
    headers: { ...buildAppIdHeaders(appId) }
  });

export const createDynamicTable = (
  request: DynamicTableCreateRequest
): Promise<{ id: string; tableKey: string }> => sharedDynamicTablesApi.createDynamicTable(request);

export const deleteDynamicTable = (tableKey: string): Promise<void> =>
  sharedDynamicTablesApi.deleteDynamicTable(tableKey);

export const getDynamicTableDeleteCheck = (tableKey: string): Promise<DeleteCheckResult> =>
  sharedDynamicTablesApi.getDynamicTableDeleteCheck(tableKey);

export const archiveDynamicTable = (tableKey: string): Promise<void> =>
  sharedDynamicTablesApi.archiveDynamicTable(tableKey);

export const restoreDynamicTable = (tableKey: string): Promise<void> =>
  sharedDynamicTablesApi.restoreDynamicTable(tableKey);

export async function alterDynamicTablePreview(
  tableKey: string,
  request: DynamicTableAlterRequest,
  appId?: string
): Promise<DynamicTableAlterPreviewResponse> {
  const response = await requestApi<ApiResponse<DynamicTableAlterPreviewResponse>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/schema/alter/preview`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json", ...buildAppIdHeaders(appId) },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) throw new Error(response.message || "预览失败");
  return response.data;
}

export async function alterDynamicTable(
  tableKey: string,
  request: DynamicTableAlterRequest,
  appId?: string
): Promise<void> {
  const response = await requestApi<ApiResponse<unknown>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/schema/alter`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json", ...buildAppIdHeaders(appId) },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "变更失败");
}

// ---------------------------------------------------------------------------
// Record CRUD (AppHost: /api/v1/dynamic-tables/:tableKey/records)
// ---------------------------------------------------------------------------

interface RecordContext {
  appKey: string;
  tableKey: string;
  appId?: string;
}

function recordsBase(ctx: RecordContext): string {
  const prefix = resolveAppHostPrefix(ctx.appKey);
  return `${prefix}/api/v1/dynamic-tables/${encodeURIComponent(ctx.tableKey)}/records`;
}

export async function queryDynamicRecords(
  appKey: string,
  tableKey: string,
  request: DynamicRecordQueryRequest,
  appId?: string
): Promise<DynamicRecordListResult> {
  const ctx: RecordContext = { appKey, tableKey, appId };
  const response = await requestApi<ApiResponse<DynamicRecordListResult>>(
    `${recordsBase(ctx)}/query`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json", ...buildAppIdHeaders(appId) },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getDynamicRecord(
  appKey: string,
  tableKey: string,
  id: string,
  appId?: string
): Promise<DynamicRecordDto | null> {
  const ctx: RecordContext = { appKey, tableKey, appId };
  const response = await requestApi<ApiResponse<DynamicRecordDto | null>>(
    `${recordsBase(ctx)}/${encodeURIComponent(id)}`,
    { headers: { ...buildAppIdHeaders(appId) } }
  );
  return response.data ?? null;
}

export async function createDynamicRecord(
  appKey: string,
  tableKey: string,
  request: DynamicRecordUpsertRequest,
  appId?: string
): Promise<{ id: string }> {
  const ctx: RecordContext = { appKey, tableKey, appId };
  const response = await requestApi<ApiResponse<{ id: string }>>(
    recordsBase(ctx),
    {
      method: "POST",
      headers: { "Content-Type": "application/json", ...buildAppIdHeaders(appId) },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) throw new Error(response.message || "创建失败");
  return response.data;
}

export async function updateDynamicRecord(
  appKey: string,
  tableKey: string,
  id: string,
  request: DynamicRecordUpsertRequest,
  appId?: string
): Promise<void> {
  const ctx: RecordContext = { appKey, tableKey, appId };
  const response = await requestApi<ApiResponse<unknown>>(
    `${recordsBase(ctx)}/${encodeURIComponent(id)}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json", ...buildAppIdHeaders(appId) },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function deleteDynamicRecord(
  appKey: string,
  tableKey: string,
  id: string,
  appId?: string
): Promise<void> {
  const ctx: RecordContext = { appKey, tableKey, appId };
  const response = await requestApi<ApiResponse<unknown>>(
    `${recordsBase(ctx)}/${encodeURIComponent(id)}`,
    { method: "DELETE", headers: { ...buildAppIdHeaders(appId) } }
  );
  if (!response.success) throw new Error(response.message || "删除失败");
}

export async function deleteDynamicRecordsBatch(
  appKey: string,
  tableKey: string,
  ids: string[],
  appId?: string
): Promise<void> {
  const ctx: RecordContext = { appKey, tableKey, appId };
  const response = await requestApi<ApiResponse<unknown>>(
    recordsBase(ctx),
    {
      method: "DELETE",
      headers: { "Content-Type": "application/json", ...buildAppIdHeaders(appId) },
      body: JSON.stringify({ ids: ids.map(Number) })
    }
  );
  if (!response.success) throw new Error(response.message || "批量删除失败");
}
