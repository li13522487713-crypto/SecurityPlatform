import type {
  ApiResponse,
  PagedRequest,
  PagedResult,
  ApprovalTaskResponse,
  VisualizationMetricsResponse,
} from "@atlas/shared-core";
import { requestApi, toQuery } from "./api-core";

export async function getMyTasksPaged(
  pagedRequest: PagedRequest,
  status?: number,
  flowDefinitionId?: string
) {
  const params = new URLSearchParams(toQuery(pagedRequest));
  if (status !== undefined) params.append("status", status.toString());
  if (flowDefinitionId) params.append("flowDefinitionId", flowDefinitionId);
  const response = await requestApi<ApiResponse<PagedResult<ApprovalTaskResponse>>>(
    `/approval/tasks/my?${params.toString()}`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
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
        Object.entries(params).reduce(
          (acc, [k, v]) => { if (v) acc[k] = v; return acc; },
          {} as Record<string, string>
        )
      ).toString()
    : "";
  const response = await requestApi<ApiResponse<VisualizationMetricsResponse>>(
    `/visualization/metrics${query ? `?${query}` : ""}`
  );
  if (!response.data) throw new Error(response.message || "获取指标失败");
  return response.data;
}
