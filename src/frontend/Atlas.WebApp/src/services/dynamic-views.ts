import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import type {
  DataViewDefinition,
  DeleteCheckResult,
  DynamicViewHistoryItem,
  DynamicViewListItem,
  DynamicViewPreviewRequest,
  DynamicViewPublishResult,
  DynamicViewQueryResult,
  DynamicViewQueryRequest,
  DeleteCheckBlocker
} from "@/types/dynamic-dataflow";
import { requestApi } from "@/services/api-core";

export async function getDynamicViewsPaged(request: PagedRequest) {
  const query = new URLSearchParams({
    PageIndex: request.pageIndex.toString(),
    PageSize: request.pageSize.toString(),
    Keyword: request.keyword ?? ""
  }).toString();

  const response = await requestApi<ApiResponse<PagedResult<DynamicViewListItem>>>(`/dynamic-views?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function getDynamicView(viewKey: string) {
  const response = await requestApi<ApiResponse<DataViewDefinition | null>>(`/dynamic-views/${encodeURIComponent(viewKey)}`);
  return response.data ?? null;
}

export async function createDynamicView(request: DataViewDefinition) {
  const response = await requestApi<ApiResponse<{ viewKey: string }>>("/dynamic-views", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建失败");
  }
  return response.data?.viewKey ?? request.viewKey;
}

export async function updateDynamicView(viewKey: string, request: DataViewDefinition) {
  const response = await requestApi<ApiResponse<{ viewKey: string }>>(`/dynamic-views/${encodeURIComponent(viewKey)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function deleteDynamicView(viewKey: string) {
  const response = await requestApi<ApiResponse<{ viewKey: string }>>(`/dynamic-views/${encodeURIComponent(viewKey)}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function previewDynamicView(request: DynamicViewPreviewRequest): Promise<DynamicViewQueryResult> {
  const response = await requestApi<ApiResponse<DynamicViewQueryResult>>("/dynamic-views/preview", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "预览失败");
  }
  return response.data;
}

export async function publishDynamicView(viewKey: string, comment?: string): Promise<DynamicViewPublishResult> {
  const response = await requestApi<ApiResponse<DynamicViewPublishResult>>(`/dynamic-views/${encodeURIComponent(viewKey)}/publish`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ comment: comment ?? null })
  });
  if (!response.data) {
    throw new Error(response.message || "发布失败");
  }
  return response.data;
}

export async function getDynamicViewHistory(viewKey: string): Promise<DynamicViewHistoryItem[]> {
  const response = await requestApi<ApiResponse<DynamicViewHistoryItem[]>>(`/dynamic-views/${encodeURIComponent(viewKey)}/history`);
  return response.data ?? [];
}

export async function rollbackDynamicView(viewKey: string, version: number, comment?: string): Promise<DynamicViewPublishResult> {
  const response = await requestApi<ApiResponse<DynamicViewPublishResult>>(`/dynamic-views/${encodeURIComponent(viewKey)}/rollback/${version}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ comment: comment ?? null })
  });
  if (!response.data) {
    throw new Error(response.message || "回滚失败");
  }
  return response.data;
}

export async function queryDynamicViewRecords(viewKey: string, request: DynamicViewQueryRequest): Promise<DynamicViewQueryResult> {
  const response = await requestApi<ApiResponse<DynamicViewQueryResult>>(`/dynamic-views/${encodeURIComponent(viewKey)}/records/query`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getDynamicViewReferences(viewKey: string): Promise<DeleteCheckBlocker[]> {
  const response = await requestApi<ApiResponse<DeleteCheckBlocker[]>>(`/dynamic-views/${encodeURIComponent(viewKey)}/references`);
  return response.data ?? [];
}

export async function getDynamicViewDeleteCheck(viewKey: string): Promise<DeleteCheckResult> {
  const response = await requestApi<ApiResponse<DeleteCheckResult>>(`/dynamic-views/${encodeURIComponent(viewKey)}/delete-check`);
  if (!response.data) {
    throw new Error(response.message || "删除检查失败");
  }
  return response.data;
}

export async function getDynamicTableDeleteCheck(tableKey: string): Promise<DeleteCheckResult> {
  const response = await requestApi<ApiResponse<DeleteCheckResult>>(`/dynamic-tables/${encodeURIComponent(tableKey)}/delete-check`);
  if (!response.data) {
    throw new Error(response.message || "删除检查失败");
  }
  return response.data;
}
