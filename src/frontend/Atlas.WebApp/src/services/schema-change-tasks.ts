import type { ApiResponse } from "@/types/api";
import { requestApi } from "@/services/api";

export type SchemaChangeTaskState =
  | "Pending"
  | "Validating"
  | "WaitingApproval"
  | "Applying"
  | "Applied"
  | "Failed"
  | "RolledBack"
  | "Cancelled";

export interface SchemaChangeTaskListItem {
  id: string;
  tableKey?: string | null;
  draftIds: string[];
  currentState: SchemaChangeTaskState;
  isHighRisk: boolean;
  validationResult?: string | null;
  affectedResourcesSummary?: string | null;
  errorMessage?: string | null;
  startedAt?: string | null;
  endedAt?: string | null;
  operator?: number | null;
  createdAt: string;
}

export interface SchemaChangeTaskCreateRequest {
  tableKey: string;
  draftIds: string[];
}

export async function listSchemaChangeTasks(tableKey?: string): Promise<SchemaChangeTaskListItem[]> {
  const query = tableKey ? `?tableKey=${encodeURIComponent(tableKey)}` : "";
  const response = await requestApi<ApiResponse<SchemaChangeTaskListItem[]>>(
    `/api/v1/schema-change-tasks${query}`
  );
  return response.data ?? [];
}

export async function createSchemaChangeTask(request: SchemaChangeTaskCreateRequest): Promise<SchemaChangeTaskListItem> {
  const response = await requestApi<ApiResponse<SchemaChangeTaskListItem>>(
    "/api/v1/schema-change-tasks",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) throw new Error(response.message || "创建变更任务失败");
  return response.data;
}

export async function executeSchemaChangeTask(taskId: string): Promise<SchemaChangeTaskListItem> {
  const response = await requestApi<ApiResponse<SchemaChangeTaskListItem>>(
    `/api/v1/schema-change-tasks/${encodeURIComponent(taskId)}/execute`,
    { method: "POST" }
  );
  if (!response.data) throw new Error(response.message || "执行变更任务失败");
  return response.data;
}

export async function cancelSchemaChangeTask(taskId: string): Promise<void> {
  await requestApi<ApiResponse<null>>(
    `/api/v1/schema-change-tasks/${encodeURIComponent(taskId)}/cancel`,
    { method: "POST" }
  );
}
