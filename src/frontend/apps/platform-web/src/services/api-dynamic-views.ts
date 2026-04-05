import type { ApiResponse } from "@atlas/shared-core";
import { requestApi } from "@/services/api-core";
import type {
  DataViewDefinition,
  DynamicTransformJobDto,
  DynamicViewHistoryItem,
  DynamicViewSqlPreviewResult,
} from "@/types/dynamic-dataflow";

export async function getDynamicView(viewKey: string): Promise<DataViewDefinition | null> {
  const response = await requestApi<ApiResponse<DataViewDefinition | null>>(
    `/dynamic-views/${encodeURIComponent(viewKey)}`
  );
  return response.data ?? null;
}

export async function createDynamicView(request: DataViewDefinition): Promise<string> {
  const response = await requestApi<ApiResponse<{ viewKey: string }>>("/dynamic-views", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!response.success) {
    throw new Error(response.message || "创建失败");
  }
  return response.data?.viewKey ?? request.viewKey;
}

export async function updateDynamicView(viewKey: string, request: DataViewDefinition): Promise<void> {
  const response = await requestApi<ApiResponse<{ viewKey: string }>>(
    `/dynamic-views/${encodeURIComponent(viewKey)}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function getDynamicViewHistory(viewKey: string): Promise<DynamicViewHistoryItem[]> {
  const response = await requestApi<ApiResponse<DynamicViewHistoryItem[]>>(
    `/dynamic-views/${encodeURIComponent(viewKey)}/history`
  );
  return response.data ?? [];
}

export async function rollbackDynamicView(viewKey: string, version: number): Promise<void> {
  const response = await requestApi<ApiResponse<{ viewKey: string }>>(
    `/dynamic-views/${encodeURIComponent(viewKey)}/rollback/${version}`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: "{}",
    }
  );
  if (!response.success) {
    throw new Error(response.message || "回滚失败");
  }
}

export async function previewDynamicViewSql(
  definition: DataViewDefinition
): Promise<DynamicViewSqlPreviewResult> {
  const response = await requestApi<ApiResponse<DynamicViewSqlPreviewResult>>("/dynamic-views/preview-sql", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ definition }),
  });
  if (!response.data) {
    throw new Error(response.message || "预览失败");
  }
  return response.data;
}

export async function listDynamicTransformJobs(): Promise<DynamicTransformJobDto[]> {
  const response = await requestApi<ApiResponse<DynamicTransformJobDto[]>>("/dynamic-transform-jobs");
  return response.data ?? [];
}

export async function runDynamicTransformJob(jobKey: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/dynamic-transform-jobs/${encodeURIComponent(jobKey)}/run`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: "{}",
    }
  );
  if (!response.success) {
    throw new Error(response.message || "执行失败");
  }
}

export async function pauseDynamicTransformJob(jobKey: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ jobKey: string }>>(
    `/dynamic-transform-jobs/${encodeURIComponent(jobKey)}/pause`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: "{}",
    }
  );
  if (!response.success) {
    throw new Error(response.message || "暂停失败");
  }
}

export async function resumeDynamicTransformJob(jobKey: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ jobKey: string }>>(
    `/dynamic-transform-jobs/${encodeURIComponent(jobKey)}/resume`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: "{}",
    }
  );
  if (!response.success) {
    throw new Error(response.message || "恢复失败");
  }
}
