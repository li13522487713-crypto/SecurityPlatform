import { requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";

export type KnowledgeBaseType = 0 | 1 | 2;
export type DocumentProcessingStatus = 0 | 1 | 2 | 3;
export type KnowledgeRetrievalStrategy = "vector" | "bm25" | "hybrid";

export interface KnowledgeBaseDto {
  id: number;
  name: string;
  description?: string;
  type: KnowledgeBaseType;
  documentCount: number;
  chunkCount: number;
  createdAt: string;
}

export interface KnowledgeDocumentDto {
  id: number;
  knowledgeBaseId: number;
  fileId?: number;
  fileName: string;
  contentType?: string;
  fileSizeBytes: number;
  status: DocumentProcessingStatus;
  errorMessage?: string;
  chunkCount: number;
  createdAt: string;
  processedAt?: string;
}

export interface DocumentChunkDto {
  id: number;
  knowledgeBaseId: number;
  documentId: number;
  chunkIndex: number;
  content: string;
  startOffset: number;
  endOffset: number;
  hasEmbedding: boolean;
  createdAt: string;
}

export interface KnowledgeBaseCreateRequest {
  name: string;
  description?: string;
  type: KnowledgeBaseType;
}

export interface KnowledgeBaseUpdateRequest extends KnowledgeBaseCreateRequest {}

export interface DocumentCreateRequest {
  fileId: number;
}

export interface DocumentResegmentRequest {
  chunkSize?: number;
  overlap?: number;
  strategy?: 0 | 1 | 2;
}

export interface KnowledgeRetrievalConfigDto {
  strategy: KnowledgeRetrievalStrategy;
  enableRerank: boolean;
  vectorTopK: number;
  bm25TopK: number;
  bm25CandidateCount: number;
  rrfK: number;
}

export interface KnowledgeRetrievalConfigUpdateRequest extends KnowledgeRetrievalConfigDto {}

export interface KnowledgeRetrievalTestRequest {
  query: string;
  topK?: number;
}

export interface KnowledgeRetrievalTestItem {
  knowledgeBaseId: number;
  documentId: number;
  chunkId: number;
  content: string;
  score: number;
  documentName?: string;
}

export interface ChunkCreateRequest {
  documentId: number;
  chunkIndex: number;
  content: string;
  startOffset: number;
  endOffset: number;
}

export interface ChunkUpdateRequest {
  content: string;
  startOffset: number;
  endOffset: number;
}

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

export async function createKnowledgeBase(request: KnowledgeBaseCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/knowledge-bases", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) throw new Error(response.message || "创建知识库失败");
  return Number(response.data.id);
}

export async function updateKnowledgeBase(id: number, request: KnowledgeBaseUpdateRequest) {
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

export async function createKnowledgeDocument(knowledgeBaseId: number, request: DocumentCreateRequest) {
  const form = new FormData();
  form.append("fileId", String(request.fileId));
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/knowledge-bases/${knowledgeBaseId}/documents`,
    {
      method: "POST",
      body: form
    }
  );
  if (!response.success || !response.data) throw new Error(response.message || "新增文档失败");
  return Number(response.data.id);
}

export async function createKnowledgeDocumentByFile(knowledgeBaseId: number, file: File) {
  const form = new FormData();
  form.append("file", file);
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/knowledge-bases/${knowledgeBaseId}/documents`,
    {
      method: "POST",
      body: form
    }
  );
  if (!response.success || !response.data) throw new Error(response.message || "新增文档失败");
  return Number(response.data.id);
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
  const response = await requestApi<ApiResponse<{ id: string }>>(`/knowledge-bases/${knowledgeBaseId}/chunks`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) throw new Error(response.message || "新增分片失败");
  return Number(response.data.id);
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

export async function getKnowledgeRetrievalConfig(knowledgeBaseId: number) {
  const response = await requestApi<ApiResponse<KnowledgeRetrievalConfigDto>>(
    `/knowledge-bases/${knowledgeBaseId}/retrieval-config`
  );
  if (!response.data) {
    throw new Error(response.message || "查询检索配置失败");
  }
  return response.data;
}

export async function updateKnowledgeRetrievalConfig(
  knowledgeBaseId: number,
  request: KnowledgeRetrievalConfigUpdateRequest
) {
  const response = await requestApi<ApiResponse<object>>(
    `/knowledge-bases/${knowledgeBaseId}/retrieval-config`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新检索配置失败");
  }
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
