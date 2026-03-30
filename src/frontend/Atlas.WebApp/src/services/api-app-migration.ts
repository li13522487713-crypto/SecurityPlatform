import { requestApi, toQuery } from "@/services/api-core";

export interface AppMigrationTaskListItem {
  id: string;
  tenantId: string;
  appInstanceId: string;
  status: string;
  phase: string;
  totalItems: number;
  completedItems: number;
  failedItems: number;
  progressPercent: number;
  createdAt: string;
  startedAt?: string | null;
  finishedAt?: string | null;
  errorSummary?: string | null;
  /** SQLite 应用库结构按需对齐摘要（迁移 Schema 阶段写入） */
  schemaRepairLog?: string | null;
}

export interface AppMigrationTaskDetail extends AppMigrationTaskListItem {
  dataSourceId: string;
  currentObjectName?: string | null;
  currentBatchNo?: number | null;
  readOnlyWindow: boolean;
  enableDualWrite: boolean;
  enableRollback: boolean;
}

export interface AppMigrationTaskProgress {
  taskId: string;
  status: string;
  phase: string;
  totalItems: number;
  completedItems: number;
  failedItems: number;
  progressPercent: number;
  currentObjectName?: string | null;
  currentBatchNo?: number | null;
  updatedAt: string;
  errorSummary?: string | null;
  schemaRepairLog?: string | null;
}

export interface AppMigrationPrecheckResult {
  taskId: string;
  canStart: boolean;
  checks: string[];
  warnings: string[];
}

export interface AppIntegrityCheckSummary {
  taskId: string;
  passed: boolean;
  totalChecks: number;
  passedChecks: number;
  failedChecks: number;
  checkedAt: string;
}

export interface AppMigrationBindingRepairResult {
  appInstanceId: string;
  dataSourceId: string;
  repaired: boolean;
  message: string;
}

interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
}

interface PagedResult<T> {
  items: T[];
  total: number;
  pageIndex: number;
  pageSize: number;
}

export async function queryAppMigrationTasks(pageIndex = 1, pageSize = 20, keyword = "") {
  const query = toQuery({ pageIndex, pageSize, keyword });
  const response = await requestApi<ApiResponse<PagedResult<AppMigrationTaskListItem>>>(`/app-migrations?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询迁移任务失败");
  }
  return response.data;
}

export async function createAppMigrationTask(appInstanceId: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/app-migrations", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ appInstanceId })
  });
  if (!response.data) {
    throw new Error(response.message || "创建迁移任务失败");
  }
  return response.data;
}

export async function repairAppMigrationPrimaryBinding(appInstanceId: string): Promise<AppMigrationBindingRepairResult> {
  const response = await requestApi<ApiResponse<AppMigrationBindingRepairResult>>("/app-migrations/repair-primary-binding", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ appInstanceId })
  });
  if (!response.data) {
    throw new Error(response.message || "修复主绑定失败");
  }
  return response.data;
}

export async function getAppMigrationTask(id: string) {
  const response = await requestApi<ApiResponse<AppMigrationTaskDetail>>(`/app-migrations/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询迁移任务详情失败");
  }
  return response.data;
}

export async function precheckAppMigrationTask(id: string) {
  const response = await requestApi<ApiResponse<AppMigrationPrecheckResult>>(`/app-migrations/${id}/precheck`, {
    method: "POST"
  });
  if (!response.data) {
    throw new Error(response.message || "迁移预检查失败");
  }
  return response.data;
}

export async function startAppMigrationTask(id: string) {
  const response = await requestApi<ApiResponse<{ success: boolean; taskId: string; status: string; message?: string }>>(
    `/app-migrations/${id}/start`,
    { method: "POST" }
  );
  if (!response.data) {
    throw new Error(response.message || "启动迁移失败");
  }
  return response.data;
}

export async function getAppMigrationProgress(id: string) {
  const response = await requestApi<ApiResponse<AppMigrationTaskProgress>>(`/app-migrations/${id}/progress`);
  if (!response.data) {
    throw new Error(response.message || "查询迁移进度失败");
  }
  return response.data;
}

export async function validateAppMigrationTask(id: string) {
  const response = await requestApi<ApiResponse<AppIntegrityCheckSummary>>(`/app-migrations/${id}/validate`, {
    method: "POST"
  });
  if (!response.data) {
    throw new Error(response.message || "完整性校验失败");
  }
  return response.data;
}

export async function cutoverAppMigrationTask(id: string, readOnlyWindow = true, enableDualWrite = false) {
  const response = await requestApi<ApiResponse<{ success: boolean; taskId: string; status: string; message?: string }>>(
    `/app-migrations/${id}/cutover`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ enableReadOnlyWindow: readOnlyWindow, enableDualWrite })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "执行切换失败");
  }
  return response.data;
}

export async function rollbackAppMigrationTask(id: string) {
  const response = await requestApi<ApiResponse<{ success: boolean; taskId: string; status: string; message?: string }>>(
    `/app-migrations/${id}/rollback`,
    { method: "POST" }
  );
  if (!response.data) {
    throw new Error(response.message || "执行回切失败");
  }
  return response.data;
}

export async function resetAppMigrationTask(id: string) {
  const response = await requestApi<ApiResponse<{ success: boolean; taskId: string; status: string; message?: string }>>(
    `/app-migrations/${id}/reset`,
    { method: "POST" }
  );
  if (!response.data) {
    throw new Error(response.message || "重置迁移任务失败");
  }
  return response.data;
}
