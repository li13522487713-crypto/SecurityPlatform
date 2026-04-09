import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import type { RequestOptions } from "@atlas/shared-core/api";

export const DYNAMIC_TABLES_MAX_PAGE_SIZE = 200;

export interface AppScopedDynamicTableListItem {
  tableKey: string;
  displayName: string;
  status?: string;
}

type RequestApiInvoker = <T>(
  path: string,
  init?: RequestInit,
  options?: RequestInit & RequestOptions
) => Promise<T>;

interface SharedDynamicTablesApiDeps {
  requestApi: RequestApiInvoker;
  maxPageSize?: number;
}

export function createSharedDynamicTablesApi<
  TDynamicTableListItem,
  TDynamicTableSummary,
  TDynamicFieldDefinition,
  TDynamicTableCreateRequest,
  TDeleteCheckResult,
  TCreateResult
>({ requestApi, maxPageSize = DYNAMIC_TABLES_MAX_PAGE_SIZE }: SharedDynamicTablesApiDeps) {
  async function getDynamicTablesPaged(
    pagedRequest: PagedRequest,
    options?: RequestInit & RequestOptions
  ): Promise<PagedResult<TDynamicTableListItem>> {
    const queryParams = new URLSearchParams({
      PageIndex: pagedRequest.pageIndex.toString(),
      PageSize: pagedRequest.pageSize.toString()
    });
    const keyword = pagedRequest.keyword?.trim();
    if (keyword) {
      queryParams.set("Keyword", keyword);
    }

    const response = await requestApi<ApiResponse<PagedResult<TDynamicTableListItem>>>(
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
  ): Promise<TDynamicTableListItem[]> {
    const collected: TDynamicTableListItem[] = [];
    let pageIndex = 1;
    while (true) {
      const response = await getDynamicTablesPaged(
        { pageIndex, pageSize: maxPageSize, keyword: keyword ?? "" },
        options
      );
      collected.push(...response.items);
      if (response.items.length < maxPageSize || collected.length >= response.total) {
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
  ): Promise<TDynamicTableSummary | null> {
    const response = await requestApi<ApiResponse<TDynamicTableSummary | null>>(
      `/dynamic-tables/${encodeURIComponent(tableKey)}/summary`,
      init
    );
    return response.data ?? null;
  }

  async function getDynamicTableFields(
    tableKey: string,
    init?: RequestInit
  ): Promise<TDynamicFieldDefinition[]> {
    const response = await requestApi<ApiResponse<TDynamicFieldDefinition[]>>(
      `/dynamic-tables/${encodeURIComponent(tableKey)}/fields`,
      init
    );
    if (!response.data) {
      throw new Error(response.message || "查询失败");
    }
    return response.data;
  }

  async function createDynamicTable(
    request: TDynamicTableCreateRequest,
    init?: RequestInit
  ): Promise<TCreateResult> {
    const headers = new Headers(init?.headers);
    headers.set("Content-Type", "application/json");
    const response = await requestApi<ApiResponse<TCreateResult>>("/dynamic-tables", {
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

  async function getDynamicTableDeleteCheck(tableKey: string): Promise<TDeleteCheckResult> {
    const response = await requestApi<ApiResponse<TDeleteCheckResult>>(
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
