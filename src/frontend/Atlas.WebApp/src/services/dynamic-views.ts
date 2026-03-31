import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import type {
  DataViewDefinition,
  DeleteCheckResult,
  DynamicViewHistoryItem,
  DynamicViewListItem,
  DynamicViewPreviewRequest,
  DynamicViewPublishResult,
  DynamicViewSqlPreviewResult,
  DynamicViewQueryResult,
  DynamicViewQueryRequest,
  DeleteCheckBlocker,
  DynamicTransformJobDto,
  DynamicTransformExecutionDto,
  DynamicExternalExtractPreviewResult,
  DynamicPhysicalViewPublishResult,
  DynamicExternalExtractDataSource,
  DynamicExternalExtractSchemaResult,
  DynamicPhysicalViewPublication,
  DynamicTransformJobUpdateRequest
} from "@/types/dynamic-dataflow";
import { requestApi } from "@/services/api-core";

export async function getDynamicViewsPaged(request: PagedRequest) {
  const query = new URLSearchParams({
    PageIndex: request.pageIndex.toString(),
    PageSize: request.pageSize.toString(),
    Keyword: request.keyword ?? ""
  }).toString();

  const response = await requestApi<ApiResponse<PagedResult<DynamicViewListItem>>>(`/dynamic-views?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function getDynamicView(viewKey: string) {
  const response = await requestApi<ApiResponse<DataViewDefinition | null>>(`/dynamic-views/${encodeURIComponent(viewKey)}`);
  return response.data ?? null;
}

export async function createDynamicView(request: DataViewDefinition) {
  const response = await requestApi<ApiResponse<{ viewKey: string }>>("/dynamic-views", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建失败");
  }
  return response.data?.viewKey ?? request.viewKey;
}

export async function updateDynamicView(viewKey: string, request: DataViewDefinition) {
  const response = await requestApi<ApiResponse<{ viewKey: string }>>(`/dynamic-views/${encodeURIComponent(viewKey)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function deleteDynamicView(viewKey: string) {
  const response = await requestApi<ApiResponse<{ viewKey: string }>>(`/dynamic-views/${encodeURIComponent(viewKey)}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function previewDynamicView(request: DynamicViewPreviewRequest): Promise<DynamicViewQueryResult> {
  const response = await requestApi<ApiResponse<DynamicViewQueryResult>>("/dynamic-views/preview", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "预览失败");
  }
  return response.data;
}

export async function publishDynamicView(viewKey: string, comment?: string): Promise<DynamicViewPublishResult> {
  const response = await requestApi<ApiResponse<DynamicViewPublishResult>>(`/dynamic-views/${encodeURIComponent(viewKey)}/publish`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ comment: comment ?? null })
  });
  if (!response.data) {
    throw new Error(response.message || "发布失败");
  }
  return response.data;
}

export async function getDynamicViewHistory(viewKey: string): Promise<DynamicViewHistoryItem[]> {
  const response = await requestApi<ApiResponse<DynamicViewHistoryItem[]>>(`/dynamic-views/${encodeURIComponent(viewKey)}/history`);
  return response.data ?? [];
}

export async function rollbackDynamicView(viewKey: string, version: number, comment?: string): Promise<DynamicViewPublishResult> {
  const response = await requestApi<ApiResponse<DynamicViewPublishResult>>(`/dynamic-views/${encodeURIComponent(viewKey)}/rollback/${version}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ comment: comment ?? null })
  });
  if (!response.data) {
    throw new Error(response.message || "回滚失败");
  }
  return response.data;
}

export async function queryDynamicViewRecords(viewKey: string, request: DynamicViewQueryRequest): Promise<DynamicViewQueryResult> {
  const response = await requestApi<ApiResponse<DynamicViewQueryResult>>(`/dynamic-views/${encodeURIComponent(viewKey)}/records/query`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function previewDynamicViewSql(definition: DataViewDefinition): Promise<DynamicViewSqlPreviewResult> {
  const response = await requestApi<ApiResponse<DynamicViewSqlPreviewResult>>("/dynamic-views/preview-sql", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ definition })
  });
  if (!response.data) {
    throw new Error(response.message || "SQL 预览失败");
  }
  return response.data;
}

export async function getDynamicViewReferences(viewKey: string): Promise<DeleteCheckBlocker[]> {
  const response = await requestApi<ApiResponse<DeleteCheckBlocker[]>>(`/dynamic-views/${encodeURIComponent(viewKey)}/references`);
  return response.data ?? [];
}

export async function getDynamicViewDeleteCheck(viewKey: string): Promise<DeleteCheckResult> {
  const response = await requestApi<ApiResponse<DeleteCheckResult>>(`/dynamic-views/${encodeURIComponent(viewKey)}/delete-check`);
  if (!response.data) {
    throw new Error(response.message || "删除检查失败");
  }
  return response.data;
}

export async function getDynamicTableDeleteCheck(tableKey: string): Promise<DeleteCheckResult> {
  const response = await requestApi<ApiResponse<DeleteCheckResult>>(`/dynamic-tables/${encodeURIComponent(tableKey)}/delete-check`);
  if (!response.data) {
    throw new Error(response.message || "删除检查失败");
  }
  return response.data;
}

export async function listDynamicTransformJobs(): Promise<DynamicTransformJobDto[]> {
  const response = await requestApi<ApiResponse<DynamicTransformJobDto[]>>("/dynamic-transform-jobs");
  return response.data ?? [];
}

export async function createDynamicTransformJob(payload: {
  appId: string;
  jobKey: string;
  name: string;
  definitionJson: string;
  cronExpression?: string | null;
  enabled?: boolean;
  sourceConfigJson?: string;
  targetConfigJson?: string;
}): Promise<DynamicTransformJobDto> {
  const response = await requestApi<ApiResponse<DynamicTransformJobDto>>("/dynamic-transform-jobs", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.data) {
    throw new Error(response.message || "创建转换任务失败");
  }
  return response.data;
}

export async function getDynamicTransformJob(jobKey: string): Promise<DynamicTransformJobDto | null> {
  const response = await requestApi<ApiResponse<DynamicTransformJobDto | null>>(`/dynamic-transform-jobs/${encodeURIComponent(jobKey)}`);
  return response.data ?? null;
}

export async function updateDynamicTransformJob(jobKey: string, payload: DynamicTransformJobUpdateRequest): Promise<DynamicTransformJobDto> {
  const response = await requestApi<ApiResponse<DynamicTransformJobDto>>(`/dynamic-transform-jobs/${encodeURIComponent(jobKey)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.data) {
    throw new Error(response.message || "更新转换任务失败");
  }
  return response.data;
}

export async function runDynamicTransformJob(jobKey: string): Promise<DynamicTransformExecutionDto> {
  const response = await requestApi<ApiResponse<DynamicTransformExecutionDto>>(`/dynamic-transform-jobs/${encodeURIComponent(jobKey)}/run`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: "{}"
  });
  if (!response.data) {
    throw new Error(response.message || "执行转换任务失败");
  }
  return response.data;
}

export async function pauseDynamicTransformJob(jobKey: string): Promise<DynamicTransformJobDto> {
  const response = await requestApi<ApiResponse<DynamicTransformJobDto>>(`/dynamic-transform-jobs/${encodeURIComponent(jobKey)}/pause`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: "{}"
  });
  if (!response.data) {
    throw new Error(response.message || "暂停转换任务失败");
  }
  return response.data;
}

export async function resumeDynamicTransformJob(jobKey: string): Promise<DynamicTransformJobDto> {
  const response = await requestApi<ApiResponse<DynamicTransformJobDto>>(`/dynamic-transform-jobs/${encodeURIComponent(jobKey)}/resume`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: "{}"
  });
  if (!response.data) {
    throw new Error(response.message || "恢复转换任务失败");
  }
  return response.data;
}

export async function deleteDynamicTransformJob(jobKey: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ jobKey: string }>>(`/dynamic-transform-jobs/${encodeURIComponent(jobKey)}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除转换任务失败");
  }
}

export async function listDynamicTransformJobHistory(jobKey: string, pageIndex = 1, pageSize = 20): Promise<DynamicTransformExecutionDto[]> {
  const query = new URLSearchParams({
    PageIndex: pageIndex.toString(),
    PageSize: pageSize.toString()
  }).toString();
  const response = await requestApi<ApiResponse<DynamicTransformExecutionDto[]>>(`/dynamic-transform-jobs/${encodeURIComponent(jobKey)}/history?${query}`);
  return response.data ?? [];
}

export async function getDynamicTransformExecution(jobKey: string, executionId: string): Promise<DynamicTransformExecutionDto | null> {
  const response = await requestApi<ApiResponse<DynamicTransformExecutionDto | null>>(
    `/dynamic-transform-jobs/${encodeURIComponent(jobKey)}/executions/${encodeURIComponent(executionId)}`
  );
  return response.data ?? null;
}

export async function listExternalExtractDataSources(): Promise<DynamicExternalExtractDataSource[]> {
  const response = await requestApi<ApiResponse<DynamicExternalExtractDataSource[]>>("/dynamic-views/external-extract/data-sources");
  return response.data ?? [];
}

export async function getExternalExtractSchema(dataSourceId: string): Promise<DynamicExternalExtractSchemaResult> {
  const response = await requestApi<ApiResponse<DynamicExternalExtractSchemaResult>>(`/dynamic-views/external-extract/${encodeURIComponent(dataSourceId)}/schema`);
  if (!response.data) {
    throw new Error(response.message || "加载外部数据源 Schema 失败");
  }
  return response.data;
}

export async function previewExternalExtract(payload: {
  dataSourceId: number;
  sql: string;
  limit?: number;
}): Promise<DynamicExternalExtractPreviewResult> {
  const response = await requestApi<ApiResponse<DynamicExternalExtractPreviewResult>>("/dynamic-views/external-extract/preview", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.data) {
    throw new Error(response.message || "外部数据抽取预览失败");
  }
  return response.data;
}

export async function publishPhysicalView(
  viewKey: string,
  payload?: { replaceIfExists?: boolean; physicalViewName?: string; dataSourceId?: number | null; comment?: string }
): Promise<DynamicPhysicalViewPublishResult> {
  const response = await requestApi<ApiResponse<DynamicPhysicalViewPublishResult>>(
    `/dynamic-views/${encodeURIComponent(viewKey)}/publish-physical`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        replaceIfExists: payload?.replaceIfExists ?? true,
        physicalViewName: payload?.physicalViewName ?? null,
        dataSourceId: payload?.dataSourceId ?? null,
        comment: payload?.comment ?? null
      })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "发布物理视图失败");
  }
  return response.data;
}

export async function listPhysicalViewPublications(viewKey: string): Promise<DynamicPhysicalViewPublication[]> {
  const response = await requestApi<ApiResponse<DynamicPhysicalViewPublication[]>>(
    `/dynamic-views/${encodeURIComponent(viewKey)}/physical-publications`
  );
  return response.data ?? [];
}

export async function rollbackPhysicalViewPublication(viewKey: string, version: number): Promise<DynamicPhysicalViewPublishResult> {
  const response = await requestApi<ApiResponse<DynamicPhysicalViewPublishResult>>(
    `/dynamic-views/${encodeURIComponent(viewKey)}/physical-rollback/${version}`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: "{}"
    }
  );
  if (!response.data) {
    throw new Error(response.message || "物理视图回滚失败");
  }
  return response.data;
}

export async function deletePhysicalViewPublication(viewKey: string, publicationId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ viewKey: string; publicationId: string }>>(
    `/dynamic-views/${encodeURIComponent(viewKey)}/physical-publications/${encodeURIComponent(publicationId)}`,
    {
      method: "DELETE"
    }
  );
  if (!response.success) {
    throw new Error(response.message || "删除物理视图发布记录失败");
  }
}
