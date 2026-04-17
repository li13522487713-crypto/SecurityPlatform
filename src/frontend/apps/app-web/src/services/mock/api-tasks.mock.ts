import type { PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { matchKeyword, mockPaged, mockResolve } from "./mock-utils";

/**
 * Mock：任务中心（PRD 02-左侧导航 7.4）。
 *
 * 路由：
 *   GET /api/v1/workspaces/{workspaceId}/tasks
 *   GET /api/v1/workspaces/{workspaceId}/tasks/{taskId}
 */

export type TaskStatus = "pending" | "running" | "succeeded" | "failed";

export interface TaskItem {
  id: string;
  name: string;
  type: "workflow" | "batch" | "evaluation" | "publish";
  status: TaskStatus;
  startedAt: string;
  durationMs: number;
  ownerDisplayName: string;
}

export interface TaskDetail extends TaskItem {
  inputJson?: string;
  outputJson?: string;
  errorMessage?: string;
  logs: Array<{ timestamp: string; level: "info" | "warn" | "error"; message: string }>;
}

const TASKS: TaskItem[] = [
  {
    id: "task-1",
    name: "客服意图识别批跑",
    type: "batch",
    status: "succeeded",
    startedAt: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
    durationMs: 18_000,
    ownerDisplayName: "RootUser"
  },
  {
    id: "task-2",
    name: "营销文案生成工作流",
    type: "workflow",
    status: "running",
    startedAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
    durationMs: 0,
    ownerDisplayName: "RootUser"
  }
];

export async function listTasks(
  _workspaceId: string,
  request: PagedRequest & { status?: TaskStatus; type?: TaskItem["type"]; keyword?: string }
): Promise<PagedResult<TaskItem>> {
  const items = TASKS.filter(item => (request.status ? item.status === request.status : true))
    .filter(item => (request.type ? item.type === request.type : true))
    .filter(item => matchKeyword(item.name, request.keyword));
  return mockPaged(items, request);
}

export async function getTask(_workspaceId: string, taskId: string): Promise<TaskDetail> {
  const summary = TASKS.find(item => item.id === taskId) ?? TASKS[0];
  return mockResolve<TaskDetail>({
    ...summary,
    inputJson: '{"message":"hello"}',
    outputJson: '{"intent":"greeting"}',
    logs: [
      { timestamp: new Date().toISOString(), level: "info", message: "task started" },
      { timestamp: new Date().toISOString(), level: "info", message: "node executed" }
    ]
  });
}
