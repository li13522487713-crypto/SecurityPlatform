import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import type {
  ChunkCreateRequest,
  ChunkUpdateRequest,
  DocumentProcessingStatus,
  DocumentChunkDto,
  DocumentResegmentRequest,
  KnowledgeBaseCreateRequest,
  KnowledgeBaseDto,
  KnowledgeDocumentDto,
  KnowledgeRetrievalTestItem,
  KnowledgeRetrievalTestRequest
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
  options?: { tagsJson?: string; imageMetadataJson?: string }
) {
  const form = new FormData();
  form.append("file", file);
  if (options?.tagsJson) {
    form.append("tagsJson", options.tagsJson);
  }
  if (options?.imageMetadataJson) {
    form.append("imageMetadataJson", options.imageMetadataJson);
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
