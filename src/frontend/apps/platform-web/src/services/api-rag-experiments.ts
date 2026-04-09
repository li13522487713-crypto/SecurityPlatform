import type { ApiResponse } from "@atlas/shared-core";
import { requestApi } from "@/services/api-core";

const RAG_EXPERIMENT_BASE = "/api/v1/rag-experiments";

export interface RagExperimentRunItem {
  id: number;
  experimentName: string;
  variant: string;
  strategy: string;
  queryHash: string;
  topK: number;
  hitCount: number;
  latencyMs: number;
  isShadow: boolean;
  createdAt: string;
}

export interface RagShadowComparisonItem {
  id: number;
  mainRunId: number;
  shadowRunId: number;
  experimentName: string;
  mainVariant: string;
  shadowVariant: string;
  overlapScore: number;
  mainAvgScore: number;
  shadowAvgScore: number;
  createdAt: string;
}

export async function getRagExperimentRuns(limit = 50): Promise<RagExperimentRunItem[]> {
  const response = await requestApi<ApiResponse<RagExperimentRunItem[]>>(
    `${RAG_EXPERIMENT_BASE}/runs?limit=${encodeURIComponent(String(limit))}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取 RAG 实验运行数据失败");
  }

  return response.data;
}

export async function getRagShadowComparisons(limit = 50): Promise<RagShadowComparisonItem[]> {
  const response = await requestApi<ApiResponse<RagShadowComparisonItem[]>>(
    `${RAG_EXPERIMENT_BASE}/comparisons?limit=${encodeURIComponent(String(limit))}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取 RAG Shadow 对比数据失败");
  }

  return response.data;
}
