import type { PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { matchKeyword, mockPaged, mockResolve } from "./mock-utils";

/**
 * Mock：效果评测（PRD 02-左侧导航 7.5 + 工作流编辑器测试集）。
 *
 * 路由：
 *   GET    /api/v1/workspaces/{workspaceId}/evaluations
 *   GET    /api/v1/workspaces/{workspaceId}/testsets
 *   POST   /api/v1/workspaces/{workspaceId}/testsets
 *   GET    /api/v1/workspaces/{workspaceId}/evaluations/{evaluationId}
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

const EVALUATIONS: EvaluationItem[] = [];
const TESTSETS: TestsetItem[] = [];

export async function listEvaluations(
  _workspaceId: string,
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<EvaluationItem>> {
  const items = EVALUATIONS.filter(item => matchKeyword(item.name, request.keyword));
  return mockPaged(items, request);
}

export async function getEvaluation(_workspaceId: string, evaluationId: string): Promise<EvaluationDetail> {
  const summary = EVALUATIONS.find(item => item.id === evaluationId);
  if (!summary) {
    throw Object.assign(new Error("evaluation not found"), { code: "NOT_FOUND" });
  }
  return mockResolve({
    ...summary,
    totalCount: 0,
    passCount: 0,
    failCount: 0,
    reportJson: "{}"
  });
}

export async function listTestsets(
  _workspaceId: string,
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<TestsetItem>> {
  const items = TESTSETS.filter(item => matchKeyword(item.name, request.keyword));
  return mockPaged(items, request);
}

export async function createTestset(
  _workspaceId: string,
  request: TestsetCreateRequest
): Promise<{ testsetId: string }> {
  const id = `testset-${Date.now()}`;
  TESTSETS.push({
    id,
    name: request.name.trim(),
    description: request.description?.trim() ?? "",
    workflowId: request.workflowId,
    rowCount: request.rows.length,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString()
  });
  return mockResolve({ testsetId: id });
}
