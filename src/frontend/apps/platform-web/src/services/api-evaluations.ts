import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import { requestApi } from "@/services/api-core";

const EVALUATION_BASE = "/api/v1/evaluations";

export interface EvaluationTaskItem {
  id: string;
  name: string;
  datasetId: string;
  agentId: string;
  enableRag: boolean;
  status: string;
  totalCases: number;
  completedCases: number;
  score: number;
  aggregateMetrics: Record<string, number>;
  errorMessage: string;
  createdAt: string;
  updatedAt: string;
  startedAt?: string;
  completedAt?: string;
}

export interface EvaluationResultItem {
  id: string;
  taskId: string;
  caseId: string;
  actualOutput: string;
  score: number;
  faithfulnessScore: number;
  contextPrecisionScore: number;
  contextRecallScore: number;
  answerRelevanceScore: number;
  citationAccuracyScore: number;
  hallucinationScore: number;
  judgeReason: string;
  metrics: Record<string, number>;
  status: string;
  createdAt: string;
}

export async function getEvaluationTasks(
  request: PagedRequest
): Promise<PagedResult<EvaluationTaskItem>> {
  const query = new URLSearchParams({
    pageIndex: request.pageIndex.toString(),
    pageSize: request.pageSize.toString()
  });
  if (request.keyword) {
    query.set("keyword", request.keyword);
  }

  const response = await requestApi<ApiResponse<PagedResult<EvaluationTaskItem>>>(
    `${EVALUATION_BASE}/tasks?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取评测任务失败");
  }

  return response.data;
}

export async function getEvaluationTask(taskId: string): Promise<EvaluationTaskItem> {
  const response = await requestApi<ApiResponse<EvaluationTaskItem>>(
    `${EVALUATION_BASE}/tasks/${encodeURIComponent(taskId)}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取评测任务详情失败");
  }

  return response.data;
}

export async function getEvaluationTaskResults(taskId: string): Promise<EvaluationResultItem[]> {
  const response = await requestApi<ApiResponse<EvaluationResultItem[]>>(
    `${EVALUATION_BASE}/tasks/${encodeURIComponent(taskId)}/results`
  );
  if (!response.data) {
    throw new Error(response.message || "获取评测结果失败");
  }

  return response.data;
}
