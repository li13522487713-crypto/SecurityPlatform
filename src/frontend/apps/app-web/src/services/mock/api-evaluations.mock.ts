import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi, toQuery } from "../api-core";

/**
 * 效果评测 + 测试集（PRD 02-7.5 / PRD 05-4.8）。已切换为真实 REST：
 *   Atlas.PlatformHost/Controllers/WorkspaceEvaluationsController.cs
 *   Atlas.Infrastructure/Services/Coze/InMemoryWorkspaceEvaluationService.cs
 *   Atlas.Infrastructure/Services/Coze/InMemoryWorkspaceTestsetService.cs
 */

export type EvaluationStatus = "pending" | "running" | "succeeded" | "failed";

export interface EvaluationItem {
  id: string;
  name: string;
  targetType: "workflow" | "agent";
  targetId: string;
  testsetId: string;
  status: EvaluationStatus;
  metricSummary: string;
  startedAt: string;
}

export interface EvaluationDetail extends EvaluationItem {
  totalCount: number;
  passCount: number;
  failCount: number;
  reportJson: string;
}

export interface TestsetItem {
  id: string;
  name: string;
  description?: string;
  workflowId?: string;
  rowCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface TestsetCreateRequest {
  name: string;
  description?: string;
  workflowId?: string;
  rows: Array<Record<string, unknown>>;
}

const EVAL_STATUS: EvaluationStatus[] = ["pending", "running", "succeeded", "failed"];

interface RawEvaluation extends Omit<EvaluationItem, "status"> {
  status: EvaluationStatus | number;
}

interface RawEvaluationDetail extends Omit<EvaluationDetail, "status"> {
  status: EvaluationStatus | number;
}

function normalizeEval<T extends { status: EvaluationStatus | number }>(item: T): T & { status: EvaluationStatus } {
  const status = typeof item.status === "number" ? EVAL_STATUS[item.status] ?? "pending" : item.status;
  return { ...item, status };
}

function workspaceBase(workspaceId: string): string {
  return `/workspaces/${encodeURIComponent(workspaceId)}`;
}

export async function listEvaluations(
  workspaceId: string,
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<EvaluationItem>> {
  const query = toQuery(
    {
      pageIndex: request.pageIndex ?? 1,
      pageSize: request.pageSize ?? 20
    },
    { keyword: request.keyword }
  );
  const response = await requestApi<ApiResponse<PagedResult<RawEvaluation>>>(
    `${workspaceBase(workspaceId)}/evaluations?${query}`
  );
  if (!response.data) {
    return { items: [], total: 0, pageIndex: request.pageIndex ?? 1, pageSize: request.pageSize ?? 20 };
  }
  return {
    ...response.data,
    items: response.data.items.map(normalizeEval)
  };
}

export async function getEvaluation(
  workspaceId: string,
  evaluationId: string
): Promise<EvaluationDetail> {
  const response = await requestApi<ApiResponse<RawEvaluationDetail>>(
    `${workspaceBase(workspaceId)}/evaluations/${encodeURIComponent(evaluationId)}`
  );
  if (!response.data) {
    throw new Error(response.message || "evaluation not found");
  }
  return normalizeEval(response.data);
}

export async function listTestsets(
  workspaceId: string,
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<TestsetItem>> {
  const query = toQuery(
    {
      pageIndex: request.pageIndex ?? 1,
      pageSize: request.pageSize ?? 50
    },
    { keyword: request.keyword }
  );
  const response = await requestApi<ApiResponse<PagedResult<TestsetItem>>>(
    `${workspaceBase(workspaceId)}/testsets?${query}`
  );
  return response.data ?? { items: [], total: 0, pageIndex: request.pageIndex ?? 1, pageSize: request.pageSize ?? 50 };
}

export async function createTestset(
  workspaceId: string,
  request: TestsetCreateRequest
): Promise<{ testsetId: string }> {
  const response = await requestApi<ApiResponse<{ id?: string; testsetId?: string }>>(
    `${workspaceBase(workspaceId)}/testsets`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  const id = response.data?.testsetId ?? response.data?.id ?? "";
  if (!id) {
    throw new Error(response.message || "Failed to create testset");
  }
  return { testsetId: id };
}
