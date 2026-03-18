import { requestApi } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import type {
  CozeLayerMappingOverview,
  DebugLayerEmbedMetadata,
  ReleaseCenterDetail,
  ReleaseCenterListItem,
  RuntimeExecutionDetail,
  RuntimeExecutionListItem,
  RuntimeExecutionAuditTrailItem
} from "@/types/platform-v2";

const RELEASE_CENTER_BASE = "/api/v2/release-center/releases";
const RUNTIME_EXECUTION_BASE = "/api/v2/runtime-executions";

export async function getReleaseCenterPaged(
  request: PagedRequest
): Promise<PagedResult<ReleaseCenterListItem>> {
  const query = new URLSearchParams({
    pageIndex: request.pageIndex.toString(),
    pageSize: request.pageSize.toString()
  });
  if (request.keyword) {
    query.set("keyword", request.keyword);
  }

  const response = await requestApi<ApiResponse<PagedResult<ReleaseCenterListItem>>>(
    `${RELEASE_CENTER_BASE}?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询发布中心列表失败");
  }

  return response.data;
}

export async function getReleaseCenterDetail(releaseId: string): Promise<ReleaseCenterDetail> {
  const response = await requestApi<ApiResponse<ReleaseCenterDetail>>(`${RELEASE_CENTER_BASE}/${releaseId}`);
  if (!response.data) {
    throw new Error(response.message || "查询发布详情失败");
  }

  return response.data;
}

export async function rollbackReleaseCenter(releaseId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`${RELEASE_CENTER_BASE}/${releaseId}/rollback`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "发布回滚失败");
  }
}

export async function getRuntimeExecutionAuditTrails(
  executionId: string,
  request: PagedRequest
): Promise<PagedResult<RuntimeExecutionAuditTrailItem>> {
  const query = new URLSearchParams({
    pageIndex: request.pageIndex.toString(),
    pageSize: request.pageSize.toString()
  });
  if (request.keyword) {
    query.set("keyword", request.keyword);
  }

  const response = await requestApi<ApiResponse<PagedResult<RuntimeExecutionAuditTrailItem>>>(
    `${RUNTIME_EXECUTION_BASE}/${executionId}/audit-trails?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询执行审计轨迹失败");
  }

  return response.data;
}

export async function getRuntimeExecutionsPaged(
  request: PagedRequest
): Promise<PagedResult<RuntimeExecutionListItem>> {
  const query = new URLSearchParams({
    pageIndex: request.pageIndex.toString(),
    pageSize: request.pageSize.toString()
  });
  if (request.keyword) {
    query.set("keyword", request.keyword);
  }

  const response = await requestApi<ApiResponse<PagedResult<RuntimeExecutionListItem>>>(
    `${RUNTIME_EXECUTION_BASE}?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询运行执行列表失败");
  }

  return response.data;
}

export async function getRuntimeExecutionDetail(executionId: string): Promise<RuntimeExecutionDetail> {
  const response = await requestApi<ApiResponse<RuntimeExecutionDetail>>(`${RUNTIME_EXECUTION_BASE}/${executionId}`);
  if (!response.data) {
    throw new Error(response.message || "查询运行执行详情失败");
  }

  return response.data;
}

export async function getCozeLayerMappingsOverview(): Promise<CozeLayerMappingOverview> {
  const response = await requestApi<ApiResponse<CozeLayerMappingOverview>>("/api/v2/coze-mappings/overview");
  if (!response.data) {
    throw new Error(response.message || "查询 Coze 六层映射失败");
  }

  return response.data;
}

export async function getDebugLayerEmbedMetadata(): Promise<DebugLayerEmbedMetadata> {
  const response = await requestApi<ApiResponse<DebugLayerEmbedMetadata>>("/api/v2/debug-layer/embed-metadata");
  if (!response.data) {
    throw new Error(response.message || "查询调试层嵌入元数据失败");
  }

  return response.data;
}
