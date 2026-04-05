import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import type {
  RuntimeExecutionAuditTrailItem,
  RuntimeExecutionDebugRequest,
  RuntimeExecutionDetail,
  RuntimeExecutionListItem,
  RuntimeExecutionOperationResult,
  RuntimeExecutionTimeoutDiagnosis
} from "@/types/platform-console";
import { requestApi } from "@/services/api-core";

const RUNTIME_EXECUTION_BASE = "/api/v2/runtime-executions";

export async function getRuntimeExecutionsPaged(
  params: PagedRequest & {
    appId?: string;
    status?: string;
    startedFrom?: string;
    startedTo?: string;
  }
): Promise<PagedResult<RuntimeExecutionListItem>> {
  const query = new URLSearchParams({
    pageIndex: params.pageIndex.toString(),
    pageSize: params.pageSize.toString()
  });
  if (params.keyword) {
    query.set("keyword", params.keyword);
  }
  if (params.appId) {
    query.set("appId", params.appId);
  }
  if (params.status) {
    query.set("status", params.status);
  }
  if (params.startedFrom) {
    query.set("startedFrom", params.startedFrom);
  }
  if (params.startedTo) {
    query.set("startedTo", params.startedTo);
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

export async function cancelRuntimeExecution(id: string): Promise<RuntimeExecutionOperationResult> {
  const response = await requestApi<ApiResponse<RuntimeExecutionOperationResult>>(
    `${RUNTIME_EXECUTION_BASE}/${id}/cancel`,
    { method: "POST" }
  );
  if (!response.data) {
    throw new Error(response.message || "取消执行失败");
  }

  return response.data;
}

export async function retryRuntimeExecution(id: string): Promise<RuntimeExecutionOperationResult> {
  const response = await requestApi<ApiResponse<RuntimeExecutionOperationResult>>(
    `${RUNTIME_EXECUTION_BASE}/${id}/retry`,
    { method: "POST" }
  );
  if (!response.data) {
    throw new Error(response.message || "重试执行失败");
  }

  return response.data;
}

export async function resumeRuntimeExecution(id: string): Promise<RuntimeExecutionOperationResult> {
  const response = await requestApi<ApiResponse<RuntimeExecutionOperationResult>>(
    `${RUNTIME_EXECUTION_BASE}/${id}/resume`,
    { method: "POST" }
  );
  if (!response.data) {
    throw new Error(response.message || "恢复执行失败");
  }

  return response.data;
}

export async function debugRuntimeExecution(
  id: string,
  request: RuntimeExecutionDebugRequest
): Promise<RuntimeExecutionOperationResult> {
  const response = await requestApi<ApiResponse<RuntimeExecutionOperationResult>>(
    `${RUNTIME_EXECUTION_BASE}/${id}/debug`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "调试执行失败");
  }

  return response.data;
}

export async function getRuntimeExecutionTimeoutDiagnosis(id: string): Promise<RuntimeExecutionTimeoutDiagnosis> {
  const response = await requestApi<ApiResponse<RuntimeExecutionTimeoutDiagnosis>>(
    `${RUNTIME_EXECUTION_BASE}/${id}/timeout-diagnosis`
  );
  if (!response.data) {
    throw new Error(response.message || "获取超时诊断失败");
  }

  return response.data;
}
