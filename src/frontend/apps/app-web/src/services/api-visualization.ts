import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import { requestApi, toQuery } from "./api-core";

export interface VisualizationInstanceSummary {
  id: string;
  flowName: string;
  status: string;
  currentNode?: string | null;
  startedAt: string;
  durationMinutes?: number | null;
}

export interface VisualizationTraceNode {
  nodeId: string;
  name: string;
  status: string;
  startedAt?: string | null;
  endedAt?: string | null;
  durationMinutes?: number | null;
}

export interface VisualizationInstanceDetail {
  id: string;
  flowName: string;
  status: string;
  currentNode?: string | null;
  startedAt: string;
  finishedAt?: string | null;
  trace: VisualizationTraceNode[];
  riskHints: string[];
}

export async function getVisualizationInstances(
  request: PagedRequest,
  filters?: { processId?: string; status?: string }
): Promise<PagedResult<VisualizationInstanceSummary>> {
  const response = await requestApi<ApiResponse<PagedResult<VisualizationInstanceSummary>>>(
    `/visualization/instances?${toQuery(request, {
      processId: filters?.processId,
      status: filters?.status,
    })}`
  );
  if (!response.data) {
    throw new Error(response.message || "Request failed");
  }
  return response.data;
}

export async function getVisualizationInstanceDetail(id: string): Promise<VisualizationInstanceDetail> {
  const response = await requestApi<ApiResponse<VisualizationInstanceDetail>>(`/visualization/instances/${id}`);
  if (!response.data) {
    throw new Error(response.message || "Request failed");
  }
  return response.data;
}
