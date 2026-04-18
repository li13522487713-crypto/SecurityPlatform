import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import type {
  ChunkCreateRequest,
  ChunkUpdateRequest,
  ChunkingProfile,
  DocumentProcessingStatus,
  DocumentChunkDto,
  DocumentResegmentRequest,
  KnowledgeBaseCreateRequest,
  KnowledgeBaseDto,
  KnowledgeBinding,
  KnowledgeBindingCreateRequest,
  KnowledgeDocumentDto,
  KnowledgeDocumentLifecycleStatus,
  KnowledgeImageItem,
  KnowledgeJob,
  KnowledgeJobsListRequest,
  KnowledgePermission,
  KnowledgePermissionGrantRequest,
  KnowledgeProviderConfig,
  KnowledgeRetrievalTestItem,
  KnowledgeRetrievalTestRequest,
  KnowledgeTableColumn,
  KnowledgeTableRow,
  KnowledgeVersion,
  KnowledgeVersionCreateRequest,
  KnowledgeVersionDiff,
  ParsingStrategy,
  RetrievalLog,
  RetrievalLogQuery,
  RetrievalProfile,
  RetrievalRequest,
  RetrievalResponse
} from "@atlas/library-module-react";
import { extractResourceId, requestApi, toQuery } from "./api-core";

export async function getKnowledgeBasesPaged(request: PagedRequest, keyword?: string) {
  const query = toQuery(request, { keyword });
  const response = await requestApi<ApiResponse<PagedResult<KnowledgeBaseDto>>>(`/knowledge-bases?${query}`);
  if (!response.data) throw new Error(response.message || "查询知识库失败");
  return response.data;
}

export async function getKnowledgeBaseById(id: number) {
  const response = await requestApi<ApiResponse<KnowledgeBaseDto>>(`/knowledge-bases/${id}`);
  if (!response.data) throw new Error(response.message || "查询知识库失败");
  return response.data;
}

export async function createKnowledgeBase(request: KnowledgeBaseCreateRequest & { workspaceId?: number }) {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>("/knowledge-bases", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  const knowledgeBaseId = extractResourceId(response.data);
  if (!response.success || !knowledgeBaseId) throw new Error(response.message || "创建知识库失败");
  return Number(knowledgeBaseId);
}

export async function updateKnowledgeBase(id: number, request: KnowledgeBaseCreateRequest & { workspaceId?: number }) {
  const response = await requestApi<ApiResponse<object>>(`/knowledge-bases/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新知识库失败");
}

export async function deleteKnowledgeBase(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/knowledge-bases/${id}`, { method: "DELETE" });
  if (!response.success) throw new Error(response.message || "删除知识库失败");
}

export async function getKnowledgeDocumentsPaged(knowledgeBaseId: number, request: PagedRequest) {
  const query = toQuery(request);
  const response = await requestApi<ApiResponse<PagedResult<KnowledgeDocumentDto>>>(
    `/knowledge-bases/${knowledgeBaseId}/documents?${query}`
  );
  if (!response.data) throw new Error(response.message || "查询文档失败");
  return response.data;
}

export async function createKnowledgeDocumentByFile(
  knowledgeBaseId: number,
  file: File,
  options?: { tagsJson?: string; imageMetadataJson?: string; parsingStrategy?: ParsingStrategy }
) {
  const form = new FormData();
  form.append("file", file);
  if (options?.tagsJson) {
    form.append("tagsJson", options.tagsJson);
  }
  if (options?.imageMetadataJson) {
    form.append("imageMetadataJson", options.imageMetadataJson);
  }
  if (options?.parsingStrategy) {
    form.append("parsingStrategyJson", JSON.stringify(options.parsingStrategy));
  }
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>(
    `/knowledge-bases/${knowledgeBaseId}/documents`,
    {
      method: "POST",
      body: form
    }
  );
  const documentId = extractResourceId(response.data);
  if (!response.success || !documentId) throw new Error(response.message || "新增文档失败");
  return Number(documentId);
}

export async function deleteKnowledgeDocument(knowledgeBaseId: number, documentId: number) {
  const response = await requestApi<ApiResponse<object>>(
    `/knowledge-bases/${knowledgeBaseId}/documents/${documentId}`,
    { method: "DELETE" }
  );
  if (!response.success) throw new Error(response.message || "删除文档失败");
}

export async function getDocumentProgress(knowledgeBaseId: number, documentId: number) {
  const response = await requestApi<ApiResponse<{
    id: number;
    status: DocumentProcessingStatus;
    chunkCount: number;
    errorMessage?: string;
    processedAt?: string;
    lifecycleStatus?: KnowledgeDocumentLifecycleStatus;
    parseJobId?: number | null;
    indexJobId?: number | null;
  }>>(`/knowledge-bases/${knowledgeBaseId}/documents/${documentId}/progress`);
  if (!response.data) throw new Error(response.message || "查询进度失败");
  return response.data;
}

export async function resegmentDocument(
  knowledgeBaseId: number,
  documentId: number,
  request: DocumentResegmentRequest
) {
  const response = await requestApi<ApiResponse<object>>(
    `/knowledge-bases/${knowledgeBaseId}/documents/${documentId}/resegment`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "重分段失败");
}

export async function getDocumentChunksPaged(
  knowledgeBaseId: number,
  documentId: number,
  request: PagedRequest
) {
  const query = toQuery(request);
  const response = await requestApi<ApiResponse<PagedResult<DocumentChunkDto>>>(
    `/knowledge-bases/${knowledgeBaseId}/documents/${documentId}/chunks?${query}`
  );
  if (!response.data) throw new Error(response.message || "查询分片失败");
  return response.data;
}

export async function createChunk(knowledgeBaseId: number, request: ChunkCreateRequest) {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>(`/knowledge-bases/${knowledgeBaseId}/chunks`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  const chunkId = extractResourceId(response.data);
  if (!response.success || !chunkId) throw new Error(response.message || "新增分片失败");
  return Number(chunkId);
}

export async function updateChunk(knowledgeBaseId: number, chunkId: number, request: ChunkUpdateRequest) {
  const response = await requestApi<ApiResponse<object>>(`/knowledge-bases/${knowledgeBaseId}/chunks/${chunkId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新分片失败");
}

export async function deleteChunk(knowledgeBaseId: number, chunkId: number) {
  const response = await requestApi<ApiResponse<object>>(`/knowledge-bases/${knowledgeBaseId}/chunks/${chunkId}`, {
    method: "DELETE"
  });
  if (!response.success) throw new Error(response.message || "删除分片失败");
}

export async function testKnowledgeRetrieval(
  knowledgeBaseId: number,
  request: KnowledgeRetrievalTestRequest
) {
  const response = await requestApi<ApiResponse<KnowledgeRetrievalTestItem[]>>(
    `/knowledge-bases/${knowledgeBaseId}/retrieval-test`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "检索测试失败");
  }
  return response.data;
}

/* -------------------------------------------------------------------------- */
/*           v5 §32-44 知识库专题扩展客户端（jobs / bindings / 等）             */
/* -------------------------------------------------------------------------- */

export async function listKnowledgeJobs(knowledgeBaseId: number, request: KnowledgeJobsListRequest) {
  const query = toQuery(request as PagedRequest, {
    status: request.status,
    type: request.type
  });
  const response = await requestApi<ApiResponse<PagedResult<KnowledgeJob>>>(
    `/knowledge-bases/${knowledgeBaseId}/jobs?${query}`
  );
  if (!response.data) throw new Error(response.message || "查询任务失败");
  return response.data;
}

export async function listAllKnowledgeJobs(request: KnowledgeJobsListRequest) {
  const query = toQuery(request as PagedRequest, {
    status: request.status,
    type: request.type
  });
  const response = await requestApi<ApiResponse<PagedResult<KnowledgeJob>>>(
    `/knowledge-bases/jobs?${query}`
  );
  if (!response.data) throw new Error(response.message || "查询任务失败");
  return response.data;
}

export async function getKnowledgeJob(knowledgeBaseId: number, jobId: number) {
  const response = await requestApi<ApiResponse<KnowledgeJob>>(
    `/knowledge-bases/${knowledgeBaseId}/jobs/${jobId}`
  );
  if (!response.data) throw new Error(response.message || "任务不存在");
  return response.data;
}

export async function rerunParseJob(
  knowledgeBaseId: number,
  documentId: number,
  parsingStrategy?: ParsingStrategy
): Promise<number> {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>(
    `/knowledge-bases/${knowledgeBaseId}/jobs/parse`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ documentId, parsingStrategy })
    }
  );
  const id = extractResourceId(response.data);
  if (!response.success || !id) throw new Error(response.message || "重跑解析失败");
  return Number(id);
}

export async function rebuildKnowledgeIndex(knowledgeBaseId: number, documentId?: number): Promise<number> {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>(
    `/knowledge-bases/${knowledgeBaseId}/jobs/rebuild-index`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ documentId })
    }
  );
  const id = extractResourceId(response.data);
  if (!response.success || !id) throw new Error(response.message || "重建索引失败");
  return Number(id);
}

export async function retryKnowledgeJob(knowledgeBaseId: number, jobId: number) {
  const response = await requestApi<ApiResponse<object>>(
    `/knowledge-bases/${knowledgeBaseId}/jobs/${jobId}:retry`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "重投失败");
}

export async function cancelKnowledgeJob(knowledgeBaseId: number, jobId: number) {
  const response = await requestApi<ApiResponse<object>>(
    `/knowledge-bases/${knowledgeBaseId}/jobs/${jobId}:cancel`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "取消任务失败");
}

export async function listKnowledgeBindings(knowledgeBaseId: number, request: PagedRequest) {
  const response = await requestApi<ApiResponse<PagedResult<KnowledgeBinding>>>(
    `/knowledge-bases/${knowledgeBaseId}/bindings?${toQuery(request)}`
  );
  if (!response.data) throw new Error(response.message || "查询绑定失败");
  return response.data;
}

export async function listAllKnowledgeBindings(request: PagedRequest) {
  const response = await requestApi<ApiResponse<PagedResult<KnowledgeBinding>>>(
    `/knowledge-bases/bindings?${toQuery(request)}`
  );
  if (!response.data) throw new Error(response.message || "查询绑定失败");
  return response.data;
}

export async function createKnowledgeBinding(
  knowledgeBaseId: number,
  request: KnowledgeBindingCreateRequest
): Promise<number> {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>(
    `/knowledge-bases/${knowledgeBaseId}/bindings`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  const id = extractResourceId(response.data);
  if (!response.success || !id) throw new Error(response.message || "新增绑定失败");
  return Number(id);
}

export async function removeKnowledgeBinding(knowledgeBaseId: number, bindingId: number) {
  const response = await requestApi<ApiResponse<object>>(
    `/knowledge-bases/${knowledgeBaseId}/bindings/${bindingId}`,
    { method: "DELETE" }
  );
  if (!response.success) throw new Error(response.message || "解除绑定失败");
}

export async function listKnowledgePermissions(knowledgeBaseId: number, request: PagedRequest) {
  const response = await requestApi<ApiResponse<PagedResult<KnowledgePermission>>>(
    `/knowledge-bases/${knowledgeBaseId}/permissions?${toQuery(request)}`
  );
  if (!response.data) throw new Error(response.message || "查询权限失败");
  return response.data;
}

export async function grantKnowledgePermission(
  knowledgeBaseId: number,
  request: KnowledgePermissionGrantRequest
): Promise<number> {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>(
    `/knowledge-bases/${knowledgeBaseId}/permissions`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  const id = extractResourceId(response.data);
  if (!response.success || !id) throw new Error(response.message || "授权失败");
  return Number(id);
}

export async function revokeKnowledgePermission(knowledgeBaseId: number, permissionId: number) {
  const response = await requestApi<ApiResponse<object>>(
    `/knowledge-bases/${knowledgeBaseId}/permissions/${permissionId}`,
    { method: "DELETE" }
  );
  if (!response.success) throw new Error(response.message || "撤销权限失败");
}

export async function listKnowledgeVersions(knowledgeBaseId: number, request: PagedRequest) {
  const response = await requestApi<ApiResponse<PagedResult<KnowledgeVersion>>>(
    `/knowledge-bases/${knowledgeBaseId}/versions?${toQuery(request)}`
  );
  if (!response.data) throw new Error(response.message || "查询版本失败");
  return response.data;
}

export async function createKnowledgeVersionSnapshot(
  knowledgeBaseId: number,
  request: KnowledgeVersionCreateRequest
): Promise<number> {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>(
    `/knowledge-bases/${knowledgeBaseId}/versions`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  const id = extractResourceId(response.data);
  if (!response.success || !id) throw new Error(response.message || "创建快照失败");
  return Number(id);
}

export async function releaseKnowledgeVersion(knowledgeBaseId: number, versionId: number) {
  const response = await requestApi<ApiResponse<object>>(
    `/knowledge-bases/${knowledgeBaseId}/versions/${versionId}:release`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "发布版本失败");
}

export async function rollbackKnowledgeVersion(knowledgeBaseId: number, versionId: number) {
  const response = await requestApi<ApiResponse<object>>(
    `/knowledge-bases/${knowledgeBaseId}/versions/${versionId}:rollback`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "回退版本失败");
}

export async function diffKnowledgeVersions(
  knowledgeBaseId: number,
  fromVersionId: number,
  toVersionId: number
) {
  const response = await requestApi<ApiResponse<KnowledgeVersionDiff>>(
    `/knowledge-bases/${knowledgeBaseId}/versions/diff?from=${fromVersionId}&to=${toVersionId}`
  );
  if (!response.data) throw new Error(response.message || "版本对比失败");
  return response.data;
}

export async function listKnowledgeRetrievalLogs(
  knowledgeBaseId: number,
  request: RetrievalLogQuery
) {
  const query = toQuery(request as PagedRequest, {
    callerType: request.callerType,
    fromTs: request.fromTs,
    toTs: request.toTs
  });
  const response = await requestApi<ApiResponse<PagedResult<RetrievalLog>>>(
    `/knowledge-bases/${knowledgeBaseId}/retrieval-logs?${query}`
  );
  if (!response.data) throw new Error(response.message || "查询检索日志失败");
  return response.data;
}

export async function getKnowledgeRetrievalLog(traceId: string) {
  const response = await requestApi<ApiResponse<RetrievalLog>>(
    `/knowledge-bases/retrieval-logs/${encodeURIComponent(traceId)}`
  );
  if (!response.data) throw new Error(response.message || "检索日志不存在");
  return response.data;
}

export async function runKnowledgeRetrieval(request: RetrievalRequest): Promise<RetrievalResponse> {
  const response = await requestApi<ApiResponse<RetrievalResponse>>(
    `/knowledge-bases/retrieval`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) throw new Error(response.message || "统一检索失败");
  return response.data;
}

export async function listKnowledgeTableColumns(knowledgeBaseId: number, documentId: number) {
  const response = await requestApi<ApiResponse<KnowledgeTableColumn[]>>(
    `/knowledge-bases/${knowledgeBaseId}/documents/${documentId}/table-columns`
  );
  if (!response.data) throw new Error(response.message || "查询列定义失败");
  return response.data;
}

export async function listKnowledgeTableRows(
  knowledgeBaseId: number,
  documentId: number,
  request: PagedRequest
) {
  const response = await requestApi<ApiResponse<PagedResult<KnowledgeTableRow>>>(
    `/knowledge-bases/${knowledgeBaseId}/documents/${documentId}/table-rows?${toQuery(request)}`
  );
  if (!response.data) throw new Error(response.message || "查询行视图失败");
  return response.data;
}

export async function listKnowledgeImageItems(
  knowledgeBaseId: number,
  documentId: number,
  request: PagedRequest
) {
  const response = await requestApi<ApiResponse<PagedResult<KnowledgeImageItem>>>(
    `/knowledge-bases/${knowledgeBaseId}/documents/${documentId}/image-items?${toQuery(request)}`
  );
  if (!response.data) throw new Error(response.message || "查询图片项目失败");
  return response.data;
}

export async function listKnowledgeProviderConfigs() {
  const response = await requestApi<ApiResponse<KnowledgeProviderConfig[]>>(
    `/knowledge-bases/provider-configs`
  );
  if (!response.data) throw new Error(response.message || "查询 Provider 配置失败");
  return response.data;
}

export async function updateKnowledgeChunkingProfile(knowledgeBaseId: number, profile: ChunkingProfile) {
  // 当前后端把 ChunkingProfile 持久化在 KnowledgeBaseMeta sidecar 中，通过 update KB 时透传 chunkingProfile 字段。
  const response = await requestApi<ApiResponse<object>>(
    `/knowledge-bases/${knowledgeBaseId}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ chunkingProfile: profile })
    }
  );
  if (!response.success) throw new Error(response.message || "保存切片策略失败");
}

export async function updateKnowledgeRetrievalProfile(knowledgeBaseId: number, profile: RetrievalProfile) {
  const response = await requestApi<ApiResponse<object>>(
    `/knowledge-bases/${knowledgeBaseId}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ retrievalProfile: profile })
    }
  );
  if (!response.success) throw new Error(response.message || "保存检索策略失败");
}
