/**
 * 统一运行时数据访问服务层（运行时内核层抽象）。
 *
 * 运行时内核本身只依赖抽象的请求能力，由应用侧（app-web）适配到 requestApi。
 */

export interface RuntimeDataRequestInit {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  query?: Record<string, string>;
  body?: unknown;
}

export interface RuntimeDataClient {
  request<T>(url: string, init?: RuntimeDataRequestInit): Promise<T>;
  buildRuntimeRecordsUrl(pageKey: string, appKey?: string): string;
}

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

export async function queryRuntimeRecords(
  params: RuntimeDataQueryParams,
  client: RuntimeDataClient,
): Promise<unknown> {
  const {
    pageKey,
    appKey,
    pageIndex = 1,
    pageSize = 20,
    keyword,
    sortBy,
    sortDesc,
  } = params;

  const basePath = client.buildRuntimeRecordsUrl(pageKey, appKey);
  const query: Record<string, string> = {
    pageIndex: pageIndex.toString(),
    pageSize: pageSize.toString(),
  };
  if (keyword) query.keyword = keyword;
  if (sortBy) query.sortBy = sortBy;
  if (sortDesc !== undefined) query.sortDesc = String(sortDesc);

  return client.request(`${basePath}?${new URLSearchParams(query).toString()}`, {
    method: "GET",
  });
}

export async function getRuntimeRecord(
  pageKey: string,
  appKey: string,
  recordId: string,
  client: RuntimeDataClient,
): Promise<unknown> {
  const encodedPageKey = encodeURIComponent(pageKey);
  const encodedRecordId = encodeURIComponent(recordId);
  const basePath = client.buildRuntimeRecordsUrl(encodedPageKey, appKey);
  return client.request(`${basePath}/${encodedRecordId}`, {
    method: "GET",
  });
}

export async function queryEntityRecords(
  params: EntityDataQueryParams,
  client: RuntimeDataClient,
): Promise<unknown> {
  const {
    tableKey,
    appKey,
    pageIndex = 1,
    pageSize = 20,
    keyword,
    sortBy,
    sortDesc,
    filters,
  } = params;

  const encodedKey = encodeURIComponent(tableKey);
  const body = {
    pageIndex,
    pageSize,
    keyword: keyword ?? "",
    sortBy: sortBy ?? "",
    sortDesc: sortDesc ?? false,
    filters: filters ?? [],
  };

  return client.request(`/api/app/dynamic-tables/${encodedKey}/records/query`, {
    method: "POST",
    body,
  });
}

export async function getEntityRecord(
  tableKey: string,
  appKey: string,
  recordId: string | number,
  client: RuntimeDataClient,
): Promise<unknown> {
  const encodedKey = encodeURIComponent(tableKey);
  const encodedId = encodeURIComponent(String(recordId));
  return client.request(`/api/app/dynamic-tables/${encodedKey}/records/${encodedId}`, {
    method: "GET",
  });
}

export async function createEntityRecord(
  tableKey: string,
  appKey: string,
  data: Record<string, unknown>,
  client: RuntimeDataClient,
): Promise<unknown> {
  const encodedKey = encodeURIComponent(tableKey);
  return client.request(`/api/app/dynamic-tables/${encodedKey}/records`, {
    method: "POST",
    body: data,
  });
}

export async function updateEntityRecord(
  tableKey: string,
  appKey: string,
  recordId: string | number,
  data: Record<string, unknown>,
  client: RuntimeDataClient,
): Promise<unknown> {
  const encodedKey = encodeURIComponent(tableKey);
  const encodedId = encodeURIComponent(String(recordId));
  return client.request(`/api/app/dynamic-tables/${encodedKey}/records/${encodedId}`, {
    method: "PUT",
    body: data,
  });
}
