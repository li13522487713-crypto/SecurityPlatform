import type { ReactNode } from "react";
import type { PagedRequest, PagedResult } from "@atlas/shared-react-core/types";

export type SupportedLocale = "zh-CN" | "en-US";
export type ResourceType = "agent" | "knowledge-base" | "workflow" | "plugin" | "database" | "app" | "prompt";
export type KnowledgeBaseType = 0 | 1 | 2;
export type DocumentProcessingStatus = 0 | 1 | 2 | 3;

export interface AiLibraryItem {
  resourceType: ResourceType;
  resourceId: number;
  name: string;
  description?: string;
  updatedAt: string;
  path: string;
  resourceSubType?: string;
  status?: string;
  documentCount?: number;
  chunkCount?: number;
  updatedBy?: string;
  actions?: string[];
}

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

export interface DocumentResegmentRequest {
  chunkSize?: number;
  overlap?: number;
  strategy?: 0 | 1 | 2;
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

export interface LibraryKnowledgeApi {
  listLibrary: (request: PagedRequest, resourceType?: ResourceType) => Promise<PagedResult<AiLibraryItem>>;
  listKnowledgeBases: (request: PagedRequest, keyword?: string) => Promise<PagedResult<KnowledgeBaseDto>>;
  getKnowledgeBase: (id: number) => Promise<KnowledgeBaseDto>;
  createKnowledgeBase: (request: KnowledgeBaseCreateRequest) => Promise<number>;
  createPlugin?: (request: {
    name: string;
    description?: string;
    icon?: string;
    category?: string;
    type: number;
    sourceType: number;
    authType: number;
    definitionJson?: string;
    authConfigJson?: string;
    toolSchemaJson?: string;
    openApiSpecJson?: string;
  }) => Promise<number>;
  createDatabase?: (request: {
    name: string;
    description?: string;
    botId?: number;
    tableSchema: string;
  }) => Promise<number>;
  updateKnowledgeBase: (id: number, request: KnowledgeBaseCreateRequest) => Promise<void>;
  deleteKnowledgeBase: (id: number) => Promise<void>;
  listDocuments: (knowledgeBaseId: number, request: PagedRequest) => Promise<PagedResult<KnowledgeDocumentDto>>;
  uploadDocument: (knowledgeBaseId: number, file: File) => Promise<number>;
  deleteDocument: (knowledgeBaseId: number, documentId: number) => Promise<void>;
  getDocumentProgress: (
    knowledgeBaseId: number,
    documentId: number
  ) => Promise<{
    id: number;
    status: DocumentProcessingStatus;
    chunkCount: number;
    errorMessage?: string;
    processedAt?: string;
  }>;
  resegmentDocument: (
    knowledgeBaseId: number,
    documentId: number,
    request: DocumentResegmentRequest
  ) => Promise<void>;
  listChunks: (
    knowledgeBaseId: number,
    documentId: number,
    request: PagedRequest
  ) => Promise<PagedResult<DocumentChunkDto>>;
  createChunk: (knowledgeBaseId: number, request: ChunkCreateRequest) => Promise<number>;
  updateChunk: (knowledgeBaseId: number, chunkId: number, request: ChunkUpdateRequest) => Promise<void>;
  deleteChunk: (knowledgeBaseId: number, chunkId: number) => Promise<void>;
  runRetrievalTest?: (
    knowledgeBaseId: number,
    request: KnowledgeRetrievalTestRequest
  ) => Promise<KnowledgeRetrievalTestItem[]>;
  getApplicationDetail?: (appId: number) => Promise<{ id: number; workflowId?: number | null }>;
  downloadDatabaseTemplate?: (databaseId: number) => Promise<void>;
  publishPlugin?: (pluginId: number) => Promise<void>;
}

export interface LibraryPageProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  appKey: string;
  spaceId: string;
  onNavigate: (path: string) => void;
}

export interface KnowledgeDetailPageProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  appKey: string;
  spaceId: string;
  knowledgeBaseId: number;
  onNavigate: (path: string) => void;
  /** Studio 侧扩展：例如资源引用卡片 */
  resourceReferencesSlot?: ReactNode;
}

export interface KnowledgeUploadPageProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  appKey: string;
  spaceId: string;
  knowledgeBaseId: number;
  initialType?: string | null;
  onNavigate: (path: string) => void;
}
