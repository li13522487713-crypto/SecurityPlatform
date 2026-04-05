import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import type { RuntimeContextDetail, RuntimeContextListItem } from "@/types/platform-console";
import { requestApi } from "@/services/api-core";

const RUNTIME_CONTEXT_BASE = "/api/v2/runtime-contexts";

export async function getRuntimeContextsPaged(
  params: PagedRequest & { appKey?: string; pageKey?: string }
): Promise<PagedResult<RuntimeContextListItem>> {
  const query = new URLSearchParams({
    pageIndex: params.pageIndex.toString(),
    pageSize: params.pageSize.toString()
  });
  if (params.keyword) {
    query.set("keyword", params.keyword);
  }
  if (params.appKey) {
    query.set("appKey", params.appKey);
  }
  if (params.pageKey) {
    query.set("pageKey", params.pageKey);
  }

  const response = await requestApi<ApiResponse<PagedResult<RuntimeContextListItem>>>(
    `${RUNTIME_CONTEXT_BASE}?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询运行上下文列表失败");
  }

  return response.data;
}

export async function getRuntimeContextByRoute(appKey: string, pageKey: string): Promise<RuntimeContextDetail> {
  const response = await requestApi<ApiResponse<RuntimeContextDetail>>(
    `${RUNTIME_CONTEXT_BASE}/${encodeURIComponent(appKey)}/${encodeURIComponent(pageKey)}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询运行上下文详情失败");
  }

  return response.data;
}

export async function getRuntimeContextById(id: string): Promise<RuntimeContextDetail> {
  const response = await requestApi<ApiResponse<RuntimeContextDetail>>(
    `${RUNTIME_CONTEXT_BASE}/${encodeURIComponent(id)}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询运行上下文详情失败");
  }

  return response.data;
}
