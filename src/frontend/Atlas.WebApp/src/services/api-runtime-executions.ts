import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import type {
  RuntimeExecutionAuditTrailItem,
  RuntimeExecutionDetail,
  RuntimeExecutionListItem
} from "@/types/platform-v2";
import { requestApi } from "@/services/api-core";

const RUNTIME_EXECUTION_BASE = "/api/v2/runtime-executions";

export async function getRuntimeExecutionsPaged(
  params: PagedRequest & { keyword?: string }
): Promise<PagedResult<RuntimeExecutionListItem>> {
  const query = new URLSearchParams({
    pageIndex: params.pageIndex.toString(),
    pageSize: params.pageSize.toString()
  });
  if (params.keyword) {
    query.set("keyword", params.keyword);
  }

  const response = await requestApi<ApiResponse<PagedResult<RuntimeExecutionListItem>>>(
    `${RUNTIME_EXECUTION_BASE}?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询运行执行列表失败");
  }

  return response.data;
}

export async function getRuntimeExecutionDetail(id: string): Promise<RuntimeExecutionDetail> {
  const response = await requestApi<ApiResponse<RuntimeExecutionDetail>>(`${RUNTIME_EXECUTION_BASE}/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询运行执行详情失败");
  }

  return response.data;
}

export async function getRuntimeExecutionAuditTrails(
  id: string,
  params: PagedRequest & { keyword?: string }
): Promise<PagedResult<RuntimeExecutionAuditTrailItem>> {
  const query = new URLSearchParams({
    pageIndex: params.pageIndex.toString(),
    pageSize: params.pageSize.toString()
  });
  if (params.keyword) {
    query.set("keyword", params.keyword);
  }

  const response = await requestApi<ApiResponse<PagedResult<RuntimeExecutionAuditTrailItem>>>(
    `${RUNTIME_EXECUTION_BASE}/${id}/audit-trails?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询运行执行审计轨迹失败");
  }

  return response.data;
}
