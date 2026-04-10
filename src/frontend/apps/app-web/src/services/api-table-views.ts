import type {
  ApiResponse,
  PagedRequest,
  PagedResult,
  TableViewListItem,
  TableViewDetail,
  TableViewConfig,
  TableViewCreateRequest,
  TableViewUpdateRequest,
  TableViewConfigUpdateRequest,
  TableViewApiFunctions
} from "@atlas/shared-core";
import { requestApi, toQuery } from "./api-core";

export async function getTableViewsPaged(
  tableKey: string,
  pagedRequest: PagedRequest
): Promise<PagedResult<TableViewListItem>> {
  const queryParams = new URLSearchParams(toQuery(pagedRequest));
  queryParams.append("tableKey", tableKey);
  const response = await requestApi<ApiResponse<PagedResult<TableViewListItem>>>(
    `/table-views?${queryParams}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getDefaultTableView(tableKey: string): Promise<TableViewDetail | null> {
  const response = await requestApi<ApiResponse<TableViewDetail | null>>(
    `/table-views/default?tableKey=${encodeURIComponent(tableKey)}`
  );
  return response.data ?? null;
}

export async function getDefaultTableViewConfig(tableKey: string): Promise<TableViewConfig | null> {
  const response = await requestApi<ApiResponse<TableViewConfig | null>>(
    `/table-views/default-config?tableKey=${encodeURIComponent(tableKey)}`
  );
  return response.data ?? null;
}

export async function getTableViewDetail(id: string): Promise<TableViewDetail> {
  const response = await requestApi<ApiResponse<TableViewDetail>>(`/table-views/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function createTableView(request: TableViewCreateRequest): Promise<TableViewListItem> {
  const response = await requestApi<ApiResponse<TableViewListItem>>("/table-views", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建失败");
  }
  return response.data;
}

export async function updateTableView(id: string, request: TableViewUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<unknown>>(`/table-views/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function updateTableViewConfig(
  id: string,
  request: TableViewConfigUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<unknown>>(`/table-views/${id}/config`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function setDefaultTableView(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<unknown>>(`/table-views/${id}/set-default`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "设置失败");
  }
}

export async function deleteTableView(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<unknown>>(`/table-views/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export const tableViewApi: TableViewApiFunctions = {
  getTableViewsPaged,
  getTableViewDetail,
  getDefaultTableView,
  getDefaultTableViewConfig,
  createTableView,
  updateTableView,
  updateTableViewConfig,
  setDefaultTableView
};
