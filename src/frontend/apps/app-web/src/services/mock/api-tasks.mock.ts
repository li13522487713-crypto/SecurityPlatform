import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi, toQuery } from "../api-core";

/**
 * 任务中心（PRD 02-7.4）。已切换为真实 REST：
 *   Atlas.PlatformHost/Controllers/WorkspaceTasksController.cs
 *   Atlas.Infrastructure/Services/Coze/InMemoryWorkspaceTaskService.cs
 *
 * 后端 M3 阶段返回空集合（Empty 状态），第二阶段对接 BatchProcess + Hangfire 聚合视图。
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

interface RawTaskItem extends Omit<TaskItem, "status"> {
  status: TaskStatus | number;
}

interface RawTaskDetail extends Omit<TaskDetail, "status"> {
  status: TaskStatus | number;
}

const STATUS_INDEX: TaskStatus[] = ["pending", "running", "succeeded", "failed"];

function normalizeStatus(value: TaskStatus | number): TaskStatus {
  if (typeof value === "number") {
    return STATUS_INDEX[value] ?? "pending";
  }
  return value;
}

function normalizeItem(item: RawTaskItem): TaskItem {
  return { ...item, status: normalizeStatus(item.status) };
}

function tasksBase(workspaceId: string): string {
  return `/workspaces/${encodeURIComponent(workspaceId)}/tasks`;
}

export async function listTasks(
  workspaceId: string,
  request: PagedRequest & { status?: TaskStatus; type?: TaskItem["type"]; keyword?: string }
): Promise<PagedResult<TaskItem>> {
  const query = toQuery(
    {
      pageIndex: request.pageIndex ?? 1,
      pageSize: request.pageSize ?? 20
    },
    {
      status: request.status,
      type: request.type,
      keyword: request.keyword
    }
  );
  const response = await requestApi<ApiResponse<PagedResult<RawTaskItem>>>(
    `${tasksBase(workspaceId)}?${query}`
  );
  if (!response.data) {
    return { items: [], total: 0, pageIndex: request.pageIndex ?? 1, pageSize: request.pageSize ?? 20 };
  }
  return {
    ...response.data,
    items: response.data.items.map(normalizeItem)
  };
}

export async function getTask(workspaceId: string, taskId: string): Promise<TaskDetail> {
  const response = await requestApi<ApiResponse<RawTaskDetail>>(
    `${tasksBase(workspaceId)}/${encodeURIComponent(taskId)}`
  );
  if (!response.data) {
    throw new Error(response.message || "Task not found");
  }
  return { ...response.data, status: normalizeStatus(response.data.status) };
}
