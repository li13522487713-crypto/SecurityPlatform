import type { ReactNode } from "react";
import type { PagedRequest, PagedResult } from "@atlas/shared-react-core/types";

export type SupportedLocale = "zh-CN" | "en-US";
export type ResourceType = "agent" | "knowledge-base" | "workflow" | "plugin" | "database" | "app" | "prompt";
/** Numeric KB type kept for legacy REST: 0=text 1=table 2=image */
export type KnowledgeBaseType = 0 | 1 | 2;
export type DocumentProcessingStatus = 0 | 1 | 2 | 3;

/* -------------------------------------------------------------------------- */
/*                            v5 报告 §32-44 契约                              */
/* -------------------------------------------------------------------------- */

/** 知识库形态：与 KnowledgeBaseType 对应 (text=0, table=1, image=2) */
export type KnowledgeBaseKind = "text" | "table" | "image";

/** 内容存储后端 / RAG 提供方 */
export type KnowledgeBaseProviderKind = "builtin" | "qdrant" | "external";

/** 文档完整生命周期状态机（与 v5 §35 对齐） */
export type KnowledgeDocumentLifecycleStatus =
  | "Draft"
  | "Uploading"
  | "Uploaded"
  | "Parsing"
  | "Chunking"
  | "Indexing"
  | "Ready"
  | "Failed"
  | "Archived";

export type KnowledgeJobType = "parse" | "index" | "rebuild" | "gc";

export type KnowledgeJobStatus =
  | "Queued"
  | "Running"
  | "Succeeded"
  | "Failed"
  | "Retrying"
  | "DeadLetter"
  | "Canceled";

export type KnowledgePermissionScope = "space" | "project" | "kb" | "document";
export type KnowledgePermissionAction = "view" | "edit" | "delete" | "publish" | "manage" | "retrieve";
export type KnowledgePermissionSubjectType = "user" | "role" | "group";

export type KnowledgeBindingCallerType = "agent" | "app" | "workflow" | "chatflow";
export type KnowledgeRetrievalCallerType = KnowledgeBindingCallerType | "studio";

export type KnowledgeProviderRole =
  | "upload"
  | "storage"
  | "vector"
  | "embedding"
  | "generation";

/** 解析策略对象（v5 §37 issue #847 所暴露字段超集） */
export interface ParsingStrategy {
  parsingType: "quick" | "precise";
  extractImage: boolean;
  extractTable: boolean;
  imageOcr: boolean;
  filterPages?: string;
  /** 表格知识库专用 */
  sheetId?: string;
  headerLine?: number;
  dataStartLine?: number;
  rowsCount?: number;
  /** 图片知识库专用 */
  captionType?: "manual" | "auto-vlm" | "filename";
}

/** 切片策略对象 */
export interface ChunkingProfile {
  mode: "fixed" | "semantic" | "table-row" | "image-item";
  size: number;
  overlap: number;
  separators: string[];
  /** 仅表格 KB：作为索引列加权 */
  indexColumns?: string[];
}

/** 检索策略对象 */
export interface RetrievalProfile {
  topK: number;
  minScore: number;
  enableRerank: boolean;
  rerankModel?: string;
  enableHybrid: boolean;
  weights: {
    vector: number;
    bm25: number;
    table: number;
    image: number;
  };
  enableQueryRewrite: boolean;
}

export const DEFAULT_PARSING_STRATEGY: ParsingStrategy = {
  parsingType: "quick",
  extractImage: false,
  extractTable: false,
  imageOcr: false
};

export const DEFAULT_CHUNKING_PROFILE: ChunkingProfile = {
  mode: "fixed",
  size: 512,
  overlap: 64,
  separators: ["\n\n", "\n", "。", "."]
};

export const DEFAULT_RETRIEVAL_PROFILE: RetrievalProfile = {
  topK: 5,
  minScore: 0.0,
  enableRerank: false,
  enableHybrid: true,
  enableQueryRewrite: false,
  weights: { vector: 0.6, bm25: 0.4, table: 0.0, image: 0.0 }
};

/* ---------------------- 任务系统（v5 §35/§37）------------------------ */

export interface KnowledgeJobLogEntry {
  ts: string;
  level: "info" | "warn" | "error";
  message: string;
}

export interface KnowledgeJob {
  id: number;
  knowledgeBaseId: number;
  type: KnowledgeJobType;
  documentId?: number | null;
  status: KnowledgeJobStatus;
  progress: number;
  attempts: number;
  maxAttempts: number;
  errorMessage?: string;
  startedAt?: string;
  finishedAt?: string;
  enqueuedAt: string;
  payloadJson?: string;
  logs: KnowledgeJobLogEntry[];
}

/* ---------------------- 治理（v5 §39/§40）---------------------------- */

export interface KnowledgeBinding {
  id: number;
  knowledgeBaseId: number;
  callerType: KnowledgeBindingCallerType;
  callerId: string;
  callerName: string;
  retrievalProfileOverrideJson?: string;
  createdAt: string;
  updatedAt: string;
}

export interface KnowledgePermission {
  id: number;
  scope: KnowledgePermissionScope;
  scopeId: string;
  knowledgeBaseId?: number | null;
  documentId?: number | null;
  subjectType: KnowledgePermissionSubjectType;
  subjectId: string;
  subjectName: string;
  actions: KnowledgePermissionAction[];
  grantedBy: string;
  grantedAt: string;
}

export interface KnowledgeVersion {
  id: number;
  knowledgeBaseId: number;
  label: string;
  note?: string;
  status: "draft" | "released" | "archived";
  snapshotRef: string;
  documentCount: number;
  chunkCount: number;
  createdBy: string;
  createdAt: string;
  releasedAt?: string;
}

export interface KnowledgeProviderConfig {
  id: string;
  role: KnowledgeProviderRole;
  providerName: string;
  displayName: string;
  endpoint?: string;
  region?: string;
  bucketOrIndex?: string;
  isDefault: boolean;
  status: "active" | "degraded" | "inactive";
  updatedAt: string;
  metadataJson?: string;
}

/* ---------------------- 检索透明度（v5 §38）-------------------------- */

export interface RetrievalCallerContext {
  callerType: KnowledgeRetrievalCallerType;
  callerId?: string;
  callerName?: string;
  /** Agent 调用时的会话 ID */
  conversationId?: string;
  /** Workflow 调用时的 traceId */
  workflowTraceId?: string;
  /** App 调用时的页面/组件 */
  pageId?: string;
  componentId?: string;
  tenantId?: string;
  userId?: string;
}

export interface RetrievalCandidate {
  knowledgeBaseId: number;
  documentId: number;
  chunkId: number;
  source: "vector" | "bm25" | "table" | "image";
  score: number;
  rerankScore?: number;
  content: string;
  documentName?: string;
  startOffset?: number;
  endOffset?: number;
  rowIndex?: number | null;
  imageRef?: string | null;
  metadata?: Record<string, string>;
}

export interface RetrievalRequest {
  query: string;
  knowledgeBaseIds: number[];
  topK: number;
  minScore?: number;
  filters?: Record<string, string | number | boolean>;
  retrievalProfile?: RetrievalProfile;
  callerContext: RetrievalCallerContext;
  debug: boolean;
}

export interface RetrievalLog {
  traceId: string;
  knowledgeBaseId: number;
  rawQuery: string;
  rewrittenQuery?: string;
  filters?: Record<string, string | number | boolean>;
  callerContext: RetrievalCallerContext;
  candidates: RetrievalCandidate[];
  reranked: RetrievalCandidate[];
  finalContext: string;
  embeddingModel: string;
  vectorStore: string;
  latencyMs: number;
  createdAt: string;
}

export interface RetrievalResponse {
  log: RetrievalLog;
}

/* ---------------------- 表格 / 图片 KB（v5 §37）---------------------- */

export interface KnowledgeTableColumn {
  id: number;
  knowledgeBaseId: number;
  documentId: number;
  ordinal: number;
  name: string;
  isIndexColumn: boolean;
  dataType: "string" | "number" | "boolean" | "date";
}

export interface KnowledgeTableRow {
  id: number;
  knowledgeBaseId: number;
  documentId: number;
  rowIndex: number;
  cellsJson: string;
  chunkId?: number | null;
}

export interface KnowledgeImageAnnotation {
  id: number;
  imageItemId: number;
  type: "tag" | "caption" | "ocr" | "vlm";
  text: string;
  confidence?: number;
}

export interface KnowledgeImageItem {
  id: number;
  knowledgeBaseId: number;
  documentId: number;
  fileId?: number;
  fileName: string;
  width?: number;
  height?: number;
  thumbnailUrl?: string;
  annotations: KnowledgeImageAnnotation[];
}

/* -------------------------------------------------------------------------- */
/*                              已存在的旧契约                                */
/* -------------------------------------------------------------------------- */

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
  /* ---------- v5 扩展（mock 必填，真实 API 为可选） ---------- */
  kind?: KnowledgeBaseKind;
  providerKind?: KnowledgeBaseProviderKind;
  providerConfigId?: string;
  lifecycleStatus?: KnowledgeDocumentLifecycleStatus;
  chunkingProfile?: ChunkingProfile;
  retrievalProfile?: RetrievalProfile;
  tags?: string[];
  bindingCount?: number;
  pendingJobCount?: number;
  failedJobCount?: number;
  versionLabel?: string;
  updatedAt?: string;
  ownerName?: string;
  workspaceId?: string;
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
  /** JSON array of tag strings */
  tagsJson: string;
  /** JSON object — image KB annotations / metadata */
  imageMetadataJson: string;
  /* ---------- v5 扩展 ---------- */
  lifecycleStatus?: KnowledgeDocumentLifecycleStatus;
  parsingStrategy?: ParsingStrategy;
  parseJobId?: number | null;
  indexJobId?: number | null;
  versionLabel?: string;
  ownerUserId?: string;
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
  /** 1-based row index for table KB chunks */
  rowIndex?: number | null;
  /** Shared column headers JSON for table row chunks */
  columnHeadersJson?: string | null;
}

export interface KnowledgeBaseCreateRequest {
  name: string;
  description?: string;
  type: KnowledgeBaseType;
  /* ---------- v5 扩展 ---------- */
  kind?: KnowledgeBaseKind;
  providerKind?: KnowledgeBaseProviderKind;
  providerConfigId?: string;
  chunkingProfile?: ChunkingProfile;
  retrievalProfile?: RetrievalProfile;
  tags?: string[];
}

/** 0 = quick, 1 = precise (full parser pipeline) */
export type DocumentParseStrategy = 0 | 1;

export interface DocumentResegmentRequest {
  chunkSize?: number;
  overlap?: number;
  strategy?: 0 | 1 | 2;
  parseStrategy?: DocumentParseStrategy;
  /** v5：携带完整解析策略对象，会覆盖上方 parseStrategy 标量字段 */
  parsingStrategy?: ParsingStrategy;
  chunkingProfile?: ChunkingProfile;
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
  knowledgeBaseIds?: number[];
  tags?: string[];
  minScore?: number;
  offset?: number;
  ownerFilter?: string;
  metadataFilter?: Record<string, string>;
  /** v5：透传 caller_context、retrievalProfile、debug 给后端 */
  retrievalProfile?: RetrievalProfile;
  callerContext?: RetrievalCallerContext;
  debug?: boolean;
}

export interface KnowledgeRetrievalTestItem {
  knowledgeBaseId: number;
  documentId: number;
  chunkId: number;
  content: string;
  score: number;
  documentName?: string;
  documentCreatedAt?: string;
  startOffset?: number;
  endOffset?: number;
  tagsJson?: string | null;
  documentNamespace?: string | null;
  /** v5：rerank 分数 */
  rerankScore?: number;
  source?: RetrievalCandidate["source"];
}

/* -------------------------------------------------------------------------- */
/*                          统一适配器接口（前后共用）                         */
/* -------------------------------------------------------------------------- */

export interface KnowledgeJobsListRequest extends PagedRequest {
  status?: KnowledgeJobStatus;
  type?: KnowledgeJobType;
}

export interface KnowledgeBindingCreateRequest {
  callerType: KnowledgeBindingCallerType;
  callerId: string;
  callerName: string;
  retrievalProfileOverride?: RetrievalProfile;
}

export interface KnowledgePermissionGrantRequest {
  scope: KnowledgePermissionScope;
  scopeId: string;
  knowledgeBaseId?: number;
  documentId?: number;
  subjectType: KnowledgePermissionSubjectType;
  subjectId: string;
  subjectName: string;
  actions: KnowledgePermissionAction[];
}

export interface KnowledgeVersionCreateRequest {
  label: string;
  note?: string;
}

export interface KnowledgeVersionDiffEntry {
  kind: "document" | "chunk" | "profile" | "binding";
  changeType: "added" | "removed" | "modified";
  ref: string;
  summary: string;
}

export interface KnowledgeVersionDiff {
  fromVersionId: number;
  toVersionId: number;
  entries: KnowledgeVersionDiffEntry[];
}

export interface RetrievalLogQuery extends PagedRequest {
  callerType?: KnowledgeRetrievalCallerType;
  fromTs?: string;
  toTs?: string;
}

export interface LibraryKnowledgeApi {
  /* --------------------- 资源中心 ---------------------- */
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

  /* --------------------- 上传与解析 -------------------- */
  listDocuments: (knowledgeBaseId: number, request: PagedRequest) => Promise<PagedResult<KnowledgeDocumentDto>>;
  uploadDocument: (
    knowledgeBaseId: number,
    file: File,
    options?: {
      tagsJson?: string;
      imageMetadataJson?: string;
      /** v5：本次上传使用的解析策略，会持久化到 KnowledgeDocumentDto */
      parsingStrategy?: ParsingStrategy;
    }
  ) => Promise<number>;
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
    /** v5：完整生命周期状态 */
    lifecycleStatus?: KnowledgeDocumentLifecycleStatus;
    parseJobId?: number | null;
    indexJobId?: number | null;
  }>;
  resegmentDocument: (
    knowledgeBaseId: number,
    documentId: number,
    request: DocumentResegmentRequest
  ) => Promise<void>;

  /* --------------------- 切片与索引 -------------------- */
  listChunks: (
    knowledgeBaseId: number,
    documentId: number,
    request: PagedRequest
  ) => Promise<PagedResult<DocumentChunkDto>>;
  createChunk: (knowledgeBaseId: number, request: ChunkCreateRequest) => Promise<number>;
  updateChunk: (knowledgeBaseId: number, chunkId: number, request: ChunkUpdateRequest) => Promise<void>;
  deleteChunk: (knowledgeBaseId: number, chunkId: number) => Promise<void>;

  /** v5：表格 KB 行视图 */
  listTableRows?: (
    knowledgeBaseId: number,
    documentId: number,
    request: PagedRequest
  ) => Promise<PagedResult<KnowledgeTableRow>>;
  /** v5：表格 KB 列定义 */
  listTableColumns?: (knowledgeBaseId: number, documentId: number) => Promise<KnowledgeTableColumn[]>;
  /** v5：图片 KB 项目视图 */
  listImageItems?: (
    knowledgeBaseId: number,
    documentId: number,
    request: PagedRequest
  ) => Promise<PagedResult<KnowledgeImageItem>>;

  /** v5：保存知识库切片 / 检索策略 */
  updateChunkingProfile?: (knowledgeBaseId: number, profile: ChunkingProfile) => Promise<void>;
  updateRetrievalProfile?: (knowledgeBaseId: number, profile: RetrievalProfile) => Promise<void>;

  /* --------------------- 任务系统 ---------------------- */
  listJobs?: (knowledgeBaseId: number, request: KnowledgeJobsListRequest) => Promise<PagedResult<KnowledgeJob>>;
  listJobsAcrossKnowledgeBases?: (request: KnowledgeJobsListRequest) => Promise<PagedResult<KnowledgeJob>>;
  getJob?: (knowledgeBaseId: number, jobId: number) => Promise<KnowledgeJob>;
  rerunParseJob?: (
    knowledgeBaseId: number,
    documentId: number,
    parsingStrategy?: ParsingStrategy
  ) => Promise<number>;
  rebuildIndex?: (knowledgeBaseId: number, documentId?: number) => Promise<number>;
  retryDeadLetter?: (knowledgeBaseId: number, jobId: number) => Promise<void>;
  cancelJob?: (knowledgeBaseId: number, jobId: number) => Promise<void>;
  /** v5：订阅 mock scheduler 任务事件，返回 unsubscribe 函数 */
  subscribeJobs?: (
    knowledgeBaseId: number,
    listener: (job: KnowledgeJob) => void
  ) => () => void;

  /* --------------------- 检索与注入 -------------------- */
  runRetrievalTest?: (
    knowledgeBaseId: number,
    request: KnowledgeRetrievalTestRequest
  ) => Promise<KnowledgeRetrievalTestItem[]>;
  /** v5：完整透明检索（debug=true 时返回 RetrievalLog） */
  runRetrieval?: (request: RetrievalRequest) => Promise<RetrievalResponse>;
  listRetrievalLogs?: (knowledgeBaseId: number, request: RetrievalLogQuery) => Promise<PagedResult<RetrievalLog>>;
  getRetrievalLog?: (traceId: string) => Promise<RetrievalLog>;

  /* --------------------- 治理 ------------------------- */
  listBindings?: (knowledgeBaseId: number, request: PagedRequest) => Promise<PagedResult<KnowledgeBinding>>;
  createBinding?: (knowledgeBaseId: number, request: KnowledgeBindingCreateRequest) => Promise<number>;
  removeBinding?: (knowledgeBaseId: number, bindingId: number) => Promise<void>;
  listAllBindings?: (request: PagedRequest) => Promise<PagedResult<KnowledgeBinding>>;

  listPermissions?: (knowledgeBaseId: number, request: PagedRequest) => Promise<PagedResult<KnowledgePermission>>;
  grantPermission?: (knowledgeBaseId: number, request: KnowledgePermissionGrantRequest) => Promise<number>;
  revokePermission?: (knowledgeBaseId: number, permissionId: number) => Promise<void>;

  listVersions?: (knowledgeBaseId: number, request: PagedRequest) => Promise<PagedResult<KnowledgeVersion>>;
  createVersionSnapshot?: (knowledgeBaseId: number, request: KnowledgeVersionCreateRequest) => Promise<number>;
  releaseVersion?: (knowledgeBaseId: number, versionId: number) => Promise<void>;
  rollbackToVersion?: (knowledgeBaseId: number, versionId: number) => Promise<void>;
  diffVersions?: (
    knowledgeBaseId: number,
    fromVersionId: number,
    toVersionId: number
  ) => Promise<KnowledgeVersionDiff>;

  listProviderConfigs?: () => Promise<KnowledgeProviderConfig[]>;

  /* --------------------- 旧 helpers ------------------- */
  getApplicationDetail?: (appId: number) => Promise<{ id: number; workflowId?: number | null }>;
  downloadDatabaseTemplate?: (databaseId: number) => Promise<void>;
  publishPlugin?: (pluginId: number) => Promise<void>;
}

/* -------------------------------------------------------------------------- */
/*                                  组件 props                                 */
/* -------------------------------------------------------------------------- */

export interface LibraryPageProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  appKey: string;
  spaceId: string;
  onNavigate: (path: string) => void;
}

export type KnowledgeDetailTabKey =
  | "overview"
  | "documents"
  | "slices"
  | "retrieval"
  | "bindings"
  | "jobs"
  | "permissions"
  | "versions";

export interface KnowledgeDetailPageProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  appKey: string;
  spaceId: string;
  knowledgeBaseId: number;
  /** 当前 tab，默认 overview。组件不直接消费 URL，由宿主负责。 */
  initialTab?: KnowledgeDetailTabKey;
  onTabChange?: (tab: KnowledgeDetailTabKey) => void;
  onNavigate: (path: string) => void;
  /** Studio 侧扩展：例如资源引用卡片，挂载在概览 tab 底部 */
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

/* -------------------------------------------------------------------------- */
/*                                 mock 类型导出                                */
/* -------------------------------------------------------------------------- */

export interface MockSeed {
  /** 是否额外种入失败示例任务 */
  withFailures?: boolean;
  /** scheduler tick 间隔（ms），默认 800；测试中可设为 0 走同步推进 */
  tickIntervalMs?: number;
  /** 立刻把所有种子任务跑到终态，方便单测 */
  flushSeedJobs?: boolean;
}
