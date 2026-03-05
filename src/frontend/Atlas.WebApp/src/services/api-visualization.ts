// 可视化流程监控模块 API
import type {
  ApiResponse,
  PagedRequest,
  PagedResult,
  AuditListItem,
  VisualizationOverview,
  VisualizationProcessSummary,
  VisualizationProcessDetail,
  VisualizationInstanceSummary,
  VisualizationInstanceDetail,
  PublishVisualizationRequest,
  ValidateVisualizationRequest,
  VisualizationValidationResult,
  VisualizationPublishResult,
  SaveVisualizationProcessRequest,
  SaveVisualizationProcessResult,
  VisualizationMetricsResponse
} from "@/types/api";
import { requestApi, toQuery } from "@/services/api-core";

export async function getVisualizationOverview(params?: {
  department?: string;
  flowType?: string;
  from?: string;
  to?: string;
}): Promise<VisualizationOverview> {
  const query = params
    ? new URLSearchParams(
        Object.entries(params).reduce((acc, [k, v]) => {
          if (v) acc[k] = v;
          return acc;
        }, {} as Record<string, string>)
      ).toString()
    : "";
  const response = await requestApi<ApiResponse<VisualizationOverview>>(
    `/visualization/overview${query ? `?${query}` : ""}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取概览失败");
  }
  return response.data;
}

export async function getVisualizationProcesses(
  pagedRequest: PagedRequest
): Promise<PagedResult<VisualizationProcessSummary>> {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<VisualizationProcessSummary>>>(
    `/visualization/processes?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取流程列表失败");
  }
  return response.data;
}

export async function getVisualizationInstances(
  pagedRequest: PagedRequest,
  params?: { processId?: string; status?: string }
): Promise<PagedResult<VisualizationInstanceSummary>> {
  const queryParams = new URLSearchParams(toQuery(pagedRequest));
  if (params?.processId) {
    queryParams.append("processId", params.processId);
  }
  if (params?.status) {
    queryParams.append("status", params.status);
  }
  const query = queryParams.toString();
  const response = await requestApi<ApiResponse<PagedResult<VisualizationInstanceSummary>>>(
    `/visualization/instances?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取实例列表失败");
  }
  return response.data;
}

export async function validateVisualizationProcess(
  request: ValidateVisualizationRequest
): Promise<VisualizationValidationResult> {
  const response = await requestApi<ApiResponse<VisualizationValidationResult>>(
    "/visualization/processes/validation",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "校验失败");
  }
  return response.data;
}

export async function publishVisualizationProcess(
  request: PublishVisualizationRequest
): Promise<VisualizationPublishResult> {
  const response = await requestApi<ApiResponse<VisualizationPublishResult>>(
    `/visualization/processes/${request.processId}/publication`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "发布失败");
  }
  return response.data;
}

export async function saveVisualizationProcess(
  request: SaveVisualizationProcessRequest
): Promise<SaveVisualizationProcessResult> {
  const isUpdate = Boolean(request.processId);
  const path = isUpdate
    ? `/visualization/processes/${request.processId}`
    : "/visualization/processes";
  const response = await requestApi<ApiResponse<SaveVisualizationProcessResult>>(path, {
    method: isUpdate ? "PUT" : "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "保存失败");
  }
  return response.data;
}

export async function getVisualizationProcessDetail(id: string): Promise<VisualizationProcessDetail> {
  const response = await requestApi<ApiResponse<VisualizationProcessDetail>>(`/visualization/processes/${id}`);
  if (!response.data) {
    throw new Error(response.message || "获取流程详情失败");
  }
  return response.data;
}

export async function getVisualizationInstanceDetail(
  id: string
): Promise<VisualizationInstanceDetail> {
  const response = await requestApi<ApiResponse<VisualizationInstanceDetail>>(
    `/visualization/instances/${id}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取实例详情失败");
  }
  return response.data;
}

export async function getVisualizationMetrics(params?: {
  department?: string;
  flowType?: string;
  from?: string;
  to?: string;
}): Promise<VisualizationMetricsResponse> {
  const query = params
    ? new URLSearchParams(
        Object.entries(params).reduce((acc, [k, v]) => {
          if (v) acc[k] = v;
          return acc;
        }, {} as Record<string, string>)
      ).toString()
    : "";
  const response = await requestApi<ApiResponse<VisualizationMetricsResponse>>(
    `/visualization/metrics${query ? `?${query}` : ""}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取指标失败");
  }
  return response.data;
}

export async function getVisualizationAudit(
  pagedRequest: PagedRequest
): Promise<PagedResult<AuditListItem>> {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<AuditListItem>>>(`/visualization/audit?${query}`);
  if (!response.data) {
    throw new Error(response.message || "获取审计记录失败");
  }
  return response.data;
}
