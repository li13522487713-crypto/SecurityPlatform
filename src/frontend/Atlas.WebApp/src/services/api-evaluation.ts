import { requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";

export type EvaluationTaskStatus = 0 | 1 | 2 | 3;
export type EvaluationCaseStatus = 0 | 1 | 2 | 3;

export interface EvaluationDatasetCreateRequest {
  name: string;
  description?: string;
  scene?: string;
}

export interface EvaluationDatasetDto {
  id: number;
  name: string;
  description: string;
  scene: string;
  caseCount: number;
  createdByUserId: number;
  createdAt: string;
  updatedAt: string;
}

export interface EvaluationCaseCreateRequest {
  input: string;
  expectedOutput?: string;
  referenceOutput?: string;
  tags?: string[];
}

export interface EvaluationCaseDto {
  id: number;
  datasetId: number;
  input: string;
  expectedOutput: string;
  referenceOutput: string;
  tags: string[];
  createdAt: string;
  updatedAt: string;
}

export interface EvaluationTaskCreateRequest {
  name: string;
  datasetId: number;
  agentId: string;
}

export interface EvaluationTaskDto {
  id: number;
  name: string;
  datasetId: number;
  agentId: string;
  status: EvaluationTaskStatus;
  totalCases: number;
  completedCases: number;
  score: number;
  errorMessage: string;
  createdAt: string;
  updatedAt: string;
  startedAt?: string;
  completedAt?: string;
}

export interface EvaluationResultDto {
  id: number;
  taskId: number;
  caseId: number;
  actualOutput: string;
  score: number;
  judgeReason: string;
  status: EvaluationCaseStatus;
  createdAt: string;
}

export interface EvaluationComparisonResult {
  leftTaskId: number;
  leftScore: number;
  rightTaskId: number;
  rightScore: number;
  delta: number;
  winner: "left" | "right" | "draw";
}

interface IdPayload {
  id: string;
}

function ensureData<T>(response: ApiResponse<T>, fallbackMessage: string): T {
  if (!response.data) {
    throw new Error(response.message || fallbackMessage);
  }
  return response.data;
}

export async function getEvaluationDatasetsPaged(request: PagedRequest) {
  const query = toQuery(request);
  const response = await requestApi<ApiResponse<PagedResult<EvaluationDatasetDto>>>(`/evaluations/datasets?${query}`);
  return ensureData(response, "查询评测数据集失败");
}

export async function createEvaluationDataset(request: EvaluationDatasetCreateRequest) {
  const response = await requestApi<ApiResponse<IdPayload>>(
    "/evaluations/datasets",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  return Number(ensureData(response, "创建评测数据集失败").id);
}

export async function getEvaluationCases(datasetId: number) {
  const response = await requestApi<ApiResponse<EvaluationCaseDto[]>>(`/evaluations/datasets/${datasetId}/cases`);
  return ensureData(response, "查询评测用例失败");
}

export async function createEvaluationCase(datasetId: number, request: EvaluationCaseCreateRequest) {
  const response = await requestApi<ApiResponse<IdPayload>>(
    `/evaluations/datasets/${datasetId}/cases`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  return Number(ensureData(response, "创建评测用例失败").id);
}

export async function getEvaluationTasksPaged(request: PagedRequest) {
  const query = toQuery(request);
  const response = await requestApi<ApiResponse<PagedResult<EvaluationTaskDto>>>(`/evaluations/tasks?${query}`);
  return ensureData(response, "查询评测任务失败");
}

export async function createEvaluationTask(request: EvaluationTaskCreateRequest) {
  const response = await requestApi<ApiResponse<IdPayload>>(
    "/evaluations/tasks",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  return Number(ensureData(response, "创建评测任务失败").id);
}

export async function getEvaluationTask(taskId: number) {
  const response = await requestApi<ApiResponse<EvaluationTaskDto>>(`/evaluations/tasks/${taskId}`);
  return ensureData(response, "查询评测任务详情失败");
}

export async function getEvaluationTaskResults(taskId: number) {
  const response = await requestApi<ApiResponse<EvaluationResultDto[]>>(`/evaluations/tasks/${taskId}/results`);
  return ensureData(response, "查询评测结果明细失败");
}

export async function compareEvaluationTasks(leftTaskId: number, rightTaskId: number) {
  const response = await requestApi<ApiResponse<EvaluationComparisonResult>>(
    `/evaluations/comparisons?leftTaskId=${leftTaskId}&rightTaskId=${rightTaskId}`
  );
  return ensureData(response, "比较评测任务失败");
}
