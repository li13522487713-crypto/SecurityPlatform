import {
  DEFAULT_CHUNKING_PROFILE,
  DEFAULT_PARSING_STRATEGY,
  DEFAULT_RETRIEVAL_PROFILE
} from "../types";
import type {
  AiLibraryItem,
  ChunkCreateRequest,
  ChunkUpdateRequest,
  ChunkingProfile,
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
  LibraryKnowledgeApi,
  MockSeed,
  ParsingStrategy,
  RetrievalCallerContext,
  RetrievalCandidate,
  RetrievalLog,
  RetrievalLogQuery,
  RetrievalProfile,
  RetrievalRequest,
  RetrievalResponse,
  ResourceType
} from "../types";
import type { PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { mapKnowledgeBaseToLibraryItem } from "../utils";
import { seedDefault } from "./fixtures";
import { MockStore, deepClone } from "./store";
import { JobScheduler } from "./scheduler";

export interface MockLibraryApi extends LibraryKnowledgeApi {
  /** 直接访问 store，用于测试或调试面板 */
  __store: MockStore;
  __scheduler: JobScheduler;
  /** 单测专用：清空并重新种子 */
  __reset: (seed?: MockSeed) => void;
}

const NOW = (): string => new Date().toISOString();

function paginate<T>(items: T[], request: PagedRequest): PagedResult<T> {
  const pageIndex = Math.max(1, request.pageIndex ?? 1);
  const pageSize = Math.max(1, request.pageSize ?? 20);
  const start = (pageIndex - 1) * pageSize;
  const end = start + pageSize;
  return {
    items: items.slice(start, end),
    total: items.length,
    pageIndex,
    pageSize
  };
}

function matchesKeyword(value: string | undefined | null, keyword: string | undefined): boolean {
  if (!keyword) return true;
  if (!value) return false;
  return value.toLowerCase().includes(keyword.toLowerCase());
}

function ensureKb(store: MockStore, knowledgeBaseId: number): KnowledgeBaseDto {
  const kb = store.state.knowledgeBases.get(knowledgeBaseId);
  if (!kb) {
    throw new Error(`Knowledge base ${knowledgeBaseId} not found`);
  }
  return kb;
}

function ensureDocument(store: MockStore, documentId: number): KnowledgeDocumentDto {
  const doc = store.state.documents.get(documentId);
  if (!doc) {
    throw new Error(`Document ${documentId} not found`);
  }
  return doc;
}

function ensureChunk(store: MockStore, chunkId: number): DocumentChunkDto {
  const chunk = store.state.chunks.get(chunkId);
  if (!chunk) {
    throw new Error(`Chunk ${chunkId} not found`);
  }
  return chunk;
}

function lifecycleFromJobStatus(job: KnowledgeJob | undefined): KnowledgeDocumentLifecycleStatus {
  if (!job) return "Ready";
  switch (job.status) {
    case "Queued":
      return "Uploaded";
    case "Running":
    case "Retrying":
      return job.type === "parse"
        ? "Parsing"
        : job.type === "chunking"
          ? "Chunking"
          : job.type === "index"
            ? "Indexing"
            : "Chunking";
    case "Succeeded":
      return "Ready";
    case "Failed":
    case "DeadLetter":
      return "Failed";
    case "Canceled":
      return "Archived";
    default:
      return "Ready";
  }
}

/**
 * v5 §35 / 计划 G8：mock 在 parse 完成时为新上传文档生成 3-6 个 chunk，
 * 让 SlicesTab 对新上传 KB 也能看到预览（不再永远 0 chunk）。
 */
function generateMockChunksForDocument(
  store: MockStore,
  knowledgeBaseId: number,
  documentId: number,
  kind: "text" | "table" | "image"
): void {
  const existing = Array.from(store.state.chunks.values()).filter(c => c.documentId === documentId);
  if (existing.length > 0) return; // 已经有切片，避免重复生成
  const count = 3 + Math.floor(Math.random() * 4); // 3..6
  const now = NOW();
  for (let i = 0; i < count; i += 1) {
    const id = store.nextChunkId();
    const baseContent = kind === "text"
      ? `[mock] 文档 ${documentId} 的第 ${i + 1} 段内容（自动生成，方便 SlicesTab 预览）`
      : kind === "table"
        ? `row#${i + 1}: ${JSON.stringify({ name: `行${i + 1}`, value: i * 10 })}`
        : `[image-item ${i + 1}] caption=mock_caption_${i + 1}`;
    store.state.chunks.set(id, {
      id,
      knowledgeBaseId,
      documentId,
      chunkIndex: i,
      content: baseContent,
      startOffset: i * 200,
      endOffset: i * 200 + baseContent.length,
      hasEmbedding: true,
      createdAt: now,
      rowIndex: kind === "table" ? i + 1 : undefined,
      columnHeadersJson: kind === "table" ? JSON.stringify(["name", "value"]) : undefined
    });
  }
  const doc = store.state.documents.get(documentId);
  if (doc) {
    doc.chunkCount = count;
  }
}

export function createMockLibraryApi(seed?: MockSeed): MockLibraryApi {
  const store = new MockStore();
  const scheduler = new JobScheduler(store, seed?.tickIntervalMs ?? 800);
  seedDefault(store, seed?.withFailures ?? false);

  if (seed?.flushSeedJobs) {
    scheduler.advanceUntilStable();
  }

  /**
   * v5 §35 / 计划 G8：三段任务链 parse → chunking → index。
   * 解析完成后由 scheduler.subscribe 钩子 enqueue 下一阶段，
   * 同时在 parse 完成时按 KB kind 生成 3-6 个 mock chunk，让新上传文档也能在 SlicesTab 看到预览。
   */
  function dispatchPair(knowledgeBaseId: number, documentId: number): {
    parseJobId: number;
    chunkingJobId: number;
    indexJobId: number;
  } {
    const now = NOW();
    const parseJob: KnowledgeJob = {
      id: store.nextJobId(),
      knowledgeBaseId,
      documentId,
      type: "parse",
      status: "Queued",
      progress: 0,
      attempts: 0,
      maxAttempts: 3,
      enqueuedAt: now,
      logs: [{ ts: now, level: "info", message: "Parse job enqueued" }]
    };
    scheduler.enqueue(parseJob);

    const chunkingJob: KnowledgeJob = {
      id: store.nextJobId(),
      knowledgeBaseId,
      documentId,
      type: "chunking",
      status: "Queued",
      progress: 0,
      attempts: 0,
      maxAttempts: 3,
      enqueuedAt: now,
      logs: [{ ts: now, level: "info", message: "Chunking job enqueued (waits for parse)" }]
    };
    // 先入队，但 scheduler 推进时按 chunking_after_parse 顺序执行；通过 subscribe 钩子触发
    store.state.jobs.set(chunkingJob.id, chunkingJob);

    const indexJob: KnowledgeJob = {
      id: store.nextJobId(),
      knowledgeBaseId,
      documentId,
      type: "index",
      status: "Queued",
      progress: 0,
      attempts: 0,
      maxAttempts: 3,
      enqueuedAt: now,
      logs: [{ ts: now, level: "info", message: "Index job enqueued (waits for chunking)" }]
    };
    store.state.jobs.set(indexJob.id, indexJob);

    const doc = store.state.documents.get(documentId);
    if (doc) {
      doc.parseJobId = parseJob.id;
      doc.indexJobId = indexJob.id;
      doc.lifecycleStatus = "Uploaded";
      doc.status = 1;
    }

    return { parseJobId: parseJob.id, chunkingJobId: chunkingJob.id, indexJobId: indexJob.id };
  }

  // v5 §35 / 计划 G8：解析完成 → 入队切片任务 + 生成 3-6 个 mock chunk
  // 切片完成 → 入队索引任务
  scheduler.subscribe(job => {
    const doc = job.documentId ? store.state.documents.get(job.documentId) : undefined;
    if (!doc) return;
    const lifecycle = lifecycleFromJobStatus(job);
    doc.lifecycleStatus = lifecycle;
    doc.status = lifecycle === "Ready" ? 2 : lifecycle === "Failed" ? 3 : 1;
    if (job.status === "Succeeded" && job.type === "index") {
      doc.processedAt = NOW();
    }

    // 链路推进：parse 成功 → 找到对应 chunking job 启动；chunking 成功 → 启动 index
    if (job.status === "Succeeded" && (job.type === "parse" || job.type === "chunking")) {
      const followType: "chunking" | "index" = job.type === "parse" ? "chunking" : "index";
      const queued = Array.from(store.state.jobs.values()).find(
        next =>
          next.documentId === job.documentId &&
          next.knowledgeBaseId === job.knowledgeBaseId &&
          next.type === followType &&
          next.status === "Queued"
      );
      if (queued) {
        scheduler.enqueue(queued);
      }
      // parse 完成时为新文档生成 3-6 个 mock chunk，便于 SlicesTab 预览
      if (job.type === "parse" && job.documentId) {
        const kb = store.state.knowledgeBases.get(job.knowledgeBaseId);
        if (kb) {
          generateMockChunksForDocument(store, job.knowledgeBaseId, job.documentId, kb.kind ?? "text");
        }
      }
    }
  });

  const adapter: MockLibraryApi = {
    __store: store,
    __scheduler: scheduler,
    __reset(nextSeed?: MockSeed) {
      scheduler.stop();
      store.state.knowledgeBases.clear();
      store.state.documents.clear();
      store.state.chunks.clear();
      store.state.jobs.clear();
      store.state.bindings.clear();
      store.state.permissions.clear();
      store.state.versions.clear();
      store.state.retrievalLogs.clear();
      store.state.providerConfigs.clear();
      store.state.tableColumns.clear();
      store.state.tableRows.clear();
      store.state.imageItems.clear();
      store.state.chunkingProfiles.clear();
      store.state.retrievalProfiles.clear();
      store.state.parsingStrategies.clear();
      seedDefault(store, nextSeed?.withFailures ?? false);
    },

    async listLibrary(request: PagedRequest, resourceType?: ResourceType): Promise<PagedResult<AiLibraryItem>> {
      if (resourceType && resourceType !== "knowledge-base") {
        return { items: [], total: 0, pageIndex: request.pageIndex ?? 1, pageSize: request.pageSize ?? 20 };
      }
      const items = Array.from(store.state.knowledgeBases.values())
        .filter(kb => matchesKeyword(kb.name, request.keyword))
        .map(mapKnowledgeBaseToLibraryItem);
      return paginate(items, request);
    },

    async listKnowledgeBases(request: PagedRequest, keyword?: string): Promise<PagedResult<KnowledgeBaseDto>> {
      const trimmed = (keyword ?? request.keyword ?? "").trim();
      const all = Array.from(store.state.knowledgeBases.values())
        .filter(kb => matchesKeyword(kb.name, trimmed) || matchesKeyword(kb.description, trimmed))
        .map(deepClone);
      return paginate(all, request);
    },

    async getKnowledgeBase(id: number): Promise<KnowledgeBaseDto> {
      return deepClone(ensureKb(store, id));
    },

    async createKnowledgeBase(request: KnowledgeBaseCreateRequest): Promise<number> {
      const id = store.nextKnowledgeBaseId();
      const kind: KnowledgeBaseDto["kind"] =
        request.kind ?? (request.type === 1 ? "table" : request.type === 2 ? "image" : "text");
      const chunkingProfile: ChunkingProfile = request.chunkingProfile
        ? deepClone(request.chunkingProfile)
        : kind === "table"
          ? { mode: "table-row", size: 1, overlap: 0, separators: ["\n"] }
          : kind === "image"
            ? { mode: "image-item", size: 1, overlap: 0, separators: [] }
            : { ...DEFAULT_CHUNKING_PROFILE };
      const retrievalProfile: RetrievalProfile = request.retrievalProfile
        ? deepClone(request.retrievalProfile)
        : { ...DEFAULT_RETRIEVAL_PROFILE };
      const now = NOW();
      const dto: KnowledgeBaseDto = {
        id,
        name: request.name,
        description: request.description,
        type: request.type,
        kind,
        providerKind: request.providerKind ?? (kind === "image" ? "qdrant" : "builtin"),
        providerConfigId: request.providerConfigId ?? (kind === "image" ? "vector-qdrant-default" : "vector-sqlite-default"),
        documentCount: 0,
        chunkCount: 0,
        bindingCount: 0,
        pendingJobCount: 0,
        failedJobCount: 0,
        tags: request.tags ?? [],
        versionLabel: "v0",
        lifecycleStatus: "Ready",
        chunkingProfile,
        retrievalProfile,
        createdAt: now,
        updatedAt: now,
        ownerName: "admin",
        workspaceId: "ws-default"
      };
      store.state.knowledgeBases.set(id, dto);
      store.state.chunkingProfiles.set(id, chunkingProfile);
      store.state.retrievalProfiles.set(id, retrievalProfile);
      return id;
    },

    async updateKnowledgeBase(id: number, request: KnowledgeBaseCreateRequest): Promise<void> {
      const kb = ensureKb(store, id);
      kb.name = request.name;
      kb.description = request.description;
      kb.type = request.type;
      kb.kind = request.kind ?? kb.kind;
      kb.tags = request.tags ?? kb.tags;
      if (request.chunkingProfile) {
        const cloned = deepClone(request.chunkingProfile);
        kb.chunkingProfile = cloned;
        store.state.chunkingProfiles.set(id, cloned);
      }
      if (request.retrievalProfile) {
        const cloned = deepClone(request.retrievalProfile);
        kb.retrievalProfile = cloned;
        store.state.retrievalProfiles.set(id, cloned);
      }
      kb.updatedAt = NOW();
    },

    async deleteKnowledgeBase(id: number): Promise<void> {
      const blocking = Array.from(store.state.bindings.values()).filter(b => b.knowledgeBaseId === id);
      if (blocking.length > 0) {
        const callers = blocking.map(b => `${b.callerType}:${b.callerName}`).join(", ");
        throw new Error(`Knowledge base ${id} still bound by: ${callers}`);
      }
      store.state.knowledgeBases.delete(id);
      store.state.chunkingProfiles.delete(id);
      store.state.retrievalProfiles.delete(id);
      // 级联删除文档、切片、任务等
      Array.from(store.state.documents.values()).forEach(doc => {
        if (doc.knowledgeBaseId === id) {
          store.state.documents.delete(doc.id);
        }
      });
      Array.from(store.state.chunks.values()).forEach(chunk => {
        if (chunk.knowledgeBaseId === id) {
          store.state.chunks.delete(chunk.id);
        }
      });
      Array.from(store.state.jobs.values()).forEach(job => {
        if (job.knowledgeBaseId === id) {
          store.state.jobs.delete(job.id);
        }
      });
    },

    async listDocuments(knowledgeBaseId: number, request: PagedRequest): Promise<PagedResult<KnowledgeDocumentDto>> {
      ensureKb(store, knowledgeBaseId);
      const all = Array.from(store.state.documents.values())
        .filter(doc => doc.knowledgeBaseId === knowledgeBaseId)
        .filter(doc => matchesKeyword(doc.fileName, request.keyword))
        .map(deepClone);
      return paginate(all, request);
    },

    async uploadDocument(
      knowledgeBaseId: number,
      file: File,
      options?: { tagsJson?: string; imageMetadataJson?: string; parsingStrategy?: ParsingStrategy }
    ): Promise<number> {
      ensureKb(store, knowledgeBaseId);
      const id = store.nextDocumentId();
      const parsingStrategy = options?.parsingStrategy ?? DEFAULT_PARSING_STRATEGY;
      const dto: KnowledgeDocumentDto = {
        id,
        knowledgeBaseId,
        fileId: id + 100000,
        fileName: file.name,
        contentType: file.type || "application/octet-stream",
        fileSizeBytes: file.size || 0,
        status: 0,
        chunkCount: 0,
        createdAt: NOW(),
        tagsJson: options?.tagsJson ?? "[]",
        imageMetadataJson: options?.imageMetadataJson ?? "{}",
        lifecycleStatus: "Uploaded",
        parsingStrategy
      };
      store.state.documents.set(id, dto);
      store.state.parsingStrategies.set(id, parsingStrategy);
      dispatchPair(knowledgeBaseId, id);
      return id;
    },

    async deleteDocument(knowledgeBaseId: number, documentId: number): Promise<void> {
      ensureKb(store, knowledgeBaseId);
      const doc = ensureDocument(store, documentId);
      store.state.documents.delete(documentId);
      store.state.parsingStrategies.delete(documentId);
      Array.from(store.state.chunks.values()).forEach(chunk => {
        if (chunk.documentId === documentId) {
          store.state.chunks.delete(chunk.id);
        }
      });
      Array.from(store.state.jobs.values()).forEach(job => {
        if (job.documentId === documentId) {
          store.state.jobs.delete(job.id);
        }
      });
      // 重新汇总计数
      const kb = store.state.knowledgeBases.get(doc.knowledgeBaseId);
      if (kb) {
        let documentCount = 0;
        let chunkCount = 0;
        store.state.documents.forEach(other => {
          if (other.knowledgeBaseId === doc.knowledgeBaseId) {
            documentCount += 1;
            chunkCount += other.chunkCount;
          }
        });
        kb.documentCount = documentCount;
        kb.chunkCount = chunkCount;
      }
    },

    async getDocumentProgress(knowledgeBaseId: number, documentId: number) {
      ensureKb(store, knowledgeBaseId);
      const doc = ensureDocument(store, documentId);
      return {
        id: doc.id,
        status: doc.status,
        chunkCount: doc.chunkCount,
        errorMessage: doc.errorMessage,
        processedAt: doc.processedAt,
        lifecycleStatus: doc.lifecycleStatus,
        parseJobId: doc.parseJobId ?? null,
        indexJobId: doc.indexJobId ?? null
      };
    },

    async resegmentDocument(
      knowledgeBaseId: number,
      documentId: number,
      request: DocumentResegmentRequest
    ): Promise<void> {
      ensureKb(store, knowledgeBaseId);
      const doc = ensureDocument(store, documentId);
      // 删除现有切片
      Array.from(store.state.chunks.values()).forEach(chunk => {
        if (chunk.documentId === documentId) {
          store.state.chunks.delete(chunk.id);
        }
      });
      doc.chunkCount = 0;
      if (request.parsingStrategy) {
        doc.parsingStrategy = request.parsingStrategy;
        store.state.parsingStrategies.set(documentId, request.parsingStrategy);
      }
      if (request.chunkingProfile) {
        const kb = store.state.knowledgeBases.get(doc.knowledgeBaseId);
        if (kb) {
          kb.chunkingProfile = request.chunkingProfile;
          store.state.chunkingProfiles.set(kb.id, request.chunkingProfile);
        }
      }
      dispatchPair(doc.knowledgeBaseId, documentId);
    },

    async listChunks(
      knowledgeBaseId: number,
      documentId: number,
      request: PagedRequest
    ): Promise<PagedResult<DocumentChunkDto>> {
      ensureKb(store, knowledgeBaseId);
      ensureDocument(store, documentId);
      const items = Array.from(store.state.chunks.values())
        .filter(chunk => chunk.documentId === documentId)
        .sort((a, b) => a.chunkIndex - b.chunkIndex)
        .map(deepClone);
      return paginate(items, request);
    },

    async createChunk(knowledgeBaseId: number, request: ChunkCreateRequest): Promise<number> {
      ensureKb(store, knowledgeBaseId);
      ensureDocument(store, request.documentId);
      const id = store.nextChunkId();
      store.state.chunks.set(id, {
        id,
        knowledgeBaseId,
        documentId: request.documentId,
        chunkIndex: request.chunkIndex,
        content: request.content,
        startOffset: request.startOffset,
        endOffset: request.endOffset,
        hasEmbedding: false,
        createdAt: NOW()
      });
      const doc = store.state.documents.get(request.documentId);
      if (doc) {
        doc.chunkCount += 1;
      }
      const kb = store.state.knowledgeBases.get(knowledgeBaseId);
      if (kb) {
        kb.chunkCount += 1;
      }
      return id;
    },

    async updateChunk(knowledgeBaseId: number, chunkId: number, request: ChunkUpdateRequest): Promise<void> {
      ensureKb(store, knowledgeBaseId);
      const chunk = ensureChunk(store, chunkId);
      chunk.content = request.content;
      chunk.startOffset = request.startOffset;
      chunk.endOffset = request.endOffset;
    },

    async deleteChunk(knowledgeBaseId: number, chunkId: number): Promise<void> {
      ensureKb(store, knowledgeBaseId);
      const chunk = ensureChunk(store, chunkId);
      store.state.chunks.delete(chunkId);
      const doc = store.state.documents.get(chunk.documentId);
      if (doc) {
        doc.chunkCount = Math.max(0, doc.chunkCount - 1);
      }
      const kb = store.state.knowledgeBases.get(knowledgeBaseId);
      if (kb) {
        kb.chunkCount = Math.max(0, kb.chunkCount - 1);
      }
    },

    async listTableRows(
      knowledgeBaseId: number,
      documentId: number,
      request: PagedRequest
    ): Promise<PagedResult<KnowledgeTableRow>> {
      ensureKb(store, knowledgeBaseId);
      ensureDocument(store, documentId);
      const items = Array.from(store.state.tableRows.values())
        .filter(row => row.documentId === documentId)
        .sort((a, b) => a.rowIndex - b.rowIndex)
        .map(deepClone);
      return paginate(items, request);
    },

    async listTableColumns(knowledgeBaseId: number, documentId: number): Promise<KnowledgeTableColumn[]> {
      ensureKb(store, knowledgeBaseId);
      ensureDocument(store, documentId);
      return Array.from(store.state.tableColumns.values())
        .filter(col => col.documentId === documentId)
        .sort((a, b) => a.ordinal - b.ordinal)
        .map(deepClone);
    },

    async listImageItems(
      knowledgeBaseId: number,
      documentId: number,
      request: PagedRequest
    ): Promise<PagedResult<KnowledgeImageItem>> {
      ensureKb(store, knowledgeBaseId);
      ensureDocument(store, documentId);
      const items = Array.from(store.state.imageItems.values())
        .filter(item => item.documentId === documentId)
        .map(deepClone);
      return paginate(items, request);
    },

    async updateChunkingProfile(knowledgeBaseId: number, profile: ChunkingProfile): Promise<void> {
      const kb = ensureKb(store, knowledgeBaseId);
      const cloned = deepClone(profile);
      kb.chunkingProfile = cloned;
      store.state.chunkingProfiles.set(knowledgeBaseId, cloned);
      kb.updatedAt = NOW();
    },

    async updateRetrievalProfile(knowledgeBaseId: number, profile: RetrievalProfile): Promise<void> {
      const kb = ensureKb(store, knowledgeBaseId);
      const cloned = deepClone(profile);
      kb.retrievalProfile = cloned;
      store.state.retrievalProfiles.set(knowledgeBaseId, cloned);
      kb.updatedAt = NOW();
    },

    async listJobs(knowledgeBaseId: number, request: KnowledgeJobsListRequest): Promise<PagedResult<KnowledgeJob>> {
      ensureKb(store, knowledgeBaseId);
      const items = Array.from(store.state.jobs.values())
        .filter(job => job.knowledgeBaseId === knowledgeBaseId)
        .filter(job => (request.status ? job.status === request.status : true))
        .filter(job => (request.type ? job.type === request.type : true))
        .sort((a, b) => b.id - a.id)
        .map(deepClone);
      return paginate(items, request);
    },

    async listJobsAcrossKnowledgeBases(request: KnowledgeJobsListRequest): Promise<PagedResult<KnowledgeJob>> {
      const items = Array.from(store.state.jobs.values())
        .filter(job => (request.status ? job.status === request.status : true))
        .filter(job => (request.type ? job.type === request.type : true))
        .sort((a, b) => b.id - a.id)
        .map(deepClone);
      return paginate(items, request);
    },

    async getJob(knowledgeBaseId: number, jobId: number): Promise<KnowledgeJob> {
      ensureKb(store, knowledgeBaseId);
      const job = store.state.jobs.get(jobId);
      if (!job) throw new Error(`Job ${jobId} not found`);
      return deepClone(job);
    },

    async rerunParseJob(
      knowledgeBaseId: number,
      documentId: number,
      parsingStrategy?: ParsingStrategy
    ): Promise<number> {
      ensureKb(store, knowledgeBaseId);
      const doc = ensureDocument(store, documentId);
      if (parsingStrategy) {
        doc.parsingStrategy = parsingStrategy;
        store.state.parsingStrategies.set(documentId, parsingStrategy);
      }
      const ids = dispatchPair(knowledgeBaseId, documentId);
      return ids.parseJobId;
    },

    async rebuildIndex(knowledgeBaseId: number, documentId?: number): Promise<number> {
      ensureKb(store, knowledgeBaseId);
      const id = store.nextJobId();
      const job: KnowledgeJob = {
        id,
        knowledgeBaseId,
        documentId: documentId ?? null,
        type: "rebuild",
        status: "Queued",
        progress: 0,
        attempts: 0,
        maxAttempts: 3,
        enqueuedAt: NOW(),
        logs: [{ ts: NOW(), level: "info", message: "Rebuild index job enqueued" }]
      };
      scheduler.enqueue(job);
      return id;
    },

    async retryDeadLetter(knowledgeBaseId: number, jobId: number): Promise<void> {
      ensureKb(store, knowledgeBaseId);
      scheduler.retry(jobId);
    },

    async cancelJob(knowledgeBaseId: number, jobId: number): Promise<void> {
      ensureKb(store, knowledgeBaseId);
      scheduler.cancel(jobId);
    },

    subscribeJobs(knowledgeBaseId: number, listener: (job: KnowledgeJob) => void): () => void {
      return scheduler.subscribeKb(knowledgeBaseId, listener);
    },

    async runRetrievalTest(
      knowledgeBaseId: number,
      request: KnowledgeRetrievalTestRequest
    ): Promise<KnowledgeRetrievalTestItem[]> {
      ensureKb(store, knowledgeBaseId);
      const callerContext: RetrievalCallerContext = request.callerContext ?? {
        callerType: "studio",
        tenantId: "00000000-0000-0000-0000-000000000001",
        userId: "admin"
      };
      const profile: RetrievalProfile =
        request.retrievalProfile ?? store.state.retrievalProfiles.get(knowledgeBaseId) ?? DEFAULT_RETRIEVAL_PROFILE;

      const response = await adapter.runRetrieval!({
        query: request.query,
        knowledgeBaseIds: request.knowledgeBaseIds && request.knowledgeBaseIds.length > 0
          ? request.knowledgeBaseIds
          : [knowledgeBaseId],
        topK: request.topK ?? profile.topK,
        minScore: request.minScore ?? profile.minScore,
        filters: request.metadataFilter,
        retrievalProfile: profile,
        callerContext,
        debug: request.debug ?? true
      });

      return response.log.reranked.map(item => ({
        knowledgeBaseId: item.knowledgeBaseId,
        documentId: item.documentId,
        chunkId: item.chunkId,
        content: item.content,
        score: item.score,
        rerankScore: item.rerankScore,
        documentName: item.documentName,
        startOffset: item.startOffset,
        endOffset: item.endOffset,
        source: item.source
      }));
    },

    async runRetrieval(request: RetrievalRequest): Promise<RetrievalResponse> {
      const profile = request.retrievalProfile ?? DEFAULT_RETRIEVAL_PROFILE;
      const matchedKbIds = request.knowledgeBaseIds.length > 0
        ? request.knowledgeBaseIds
        : Array.from(store.state.knowledgeBases.keys());
      const tokens = tokenize(request.query);

      const candidates: RetrievalCandidate[] = [];
      store.state.chunks.forEach(chunk => {
        if (!matchedKbIds.includes(chunk.knowledgeBaseId)) {
          return;
        }
        const score = scoreChunk(chunk.content, tokens);
        if (score <= 0) return;

        const kb = store.state.knowledgeBases.get(chunk.knowledgeBaseId);
        const doc = store.state.documents.get(chunk.documentId);
        const source: RetrievalCandidate["source"] =
          kb?.kind === "table" ? "table" : kb?.kind === "image" ? "image" : "vector";
        candidates.push({
          knowledgeBaseId: chunk.knowledgeBaseId,
          documentId: chunk.documentId,
          chunkId: chunk.id,
          source,
          score,
          content: chunk.content,
          documentName: doc?.fileName,
          startOffset: chunk.startOffset,
          endOffset: chunk.endOffset,
          rowIndex: chunk.rowIndex ?? null,
          metadata: doc ? { fileName: doc.fileName } : undefined
        });
      });

      candidates.sort((a, b) => b.score - a.score);
      const topK = request.topK ?? profile.topK ?? 5;
      const reranked = profile.enableRerank
        ? candidates.slice(0, topK).map(item => ({ ...item, rerankScore: Math.min(1, item.score * 1.1) }))
        : candidates.slice(0, topK);

      const finalContext = reranked.map(item => `[${item.documentName ?? item.documentId}] ${item.content}`).join("\n\n");

      const log: RetrievalLog = {
        traceId: store.nextTraceId(),
        knowledgeBaseId: matchedKbIds[0] ?? 0,
        rawQuery: request.query,
        rewrittenQuery: profile.enableQueryRewrite ? `${request.query} (rewritten by mock)` : undefined,
        filters: request.filters,
        callerContext: request.callerContext,
        candidates: deepClone(candidates.slice(0, topK * 2)),
        reranked: deepClone(reranked),
        finalContext,
        embeddingModel: "mock-embedding",
        vectorStore: "mock-store",
        latencyMs: 30 + Math.floor(Math.random() * 50),
        createdAt: NOW()
      };
      store.state.retrievalLogs.set(log.traceId, log);
      return { log: deepClone(log) };
    },

    async listRetrievalLogs(knowledgeBaseId: number, request: RetrievalLogQuery): Promise<PagedResult<RetrievalLog>> {
      ensureKb(store, knowledgeBaseId);
      const items = Array.from(store.state.retrievalLogs.values())
        .filter(log => log.knowledgeBaseId === knowledgeBaseId)
        .filter(log => (request.callerType ? log.callerContext.callerType === request.callerType : true))
        .filter(log => (request.fromTs ? log.createdAt >= request.fromTs : true))
        .filter(log => (request.toTs ? log.createdAt <= request.toTs : true))
        .sort((a, b) => (a.createdAt < b.createdAt ? 1 : -1))
        .map(deepClone);
      return paginate(items, request);
    },

    async getRetrievalLog(traceId: string): Promise<RetrievalLog> {
      const log = store.state.retrievalLogs.get(traceId);
      if (!log) throw new Error(`Retrieval log ${traceId} not found`);
      return deepClone(log);
    },

    async listBindings(knowledgeBaseId: number, request: PagedRequest): Promise<PagedResult<KnowledgeBinding>> {
      ensureKb(store, knowledgeBaseId);
      const items = Array.from(store.state.bindings.values())
        .filter(binding => binding.knowledgeBaseId === knowledgeBaseId)
        .map(deepClone);
      return paginate(items, request);
    },

    async createBinding(knowledgeBaseId: number, request: KnowledgeBindingCreateRequest): Promise<number> {
      const kb = ensureKb(store, knowledgeBaseId);
      const id = store.nextBindingId();
      const dto: KnowledgeBinding = {
        id,
        knowledgeBaseId,
        callerType: request.callerType,
        callerId: request.callerId,
        callerName: request.callerName,
        retrievalProfileOverrideJson: request.retrievalProfileOverride
          ? JSON.stringify(request.retrievalProfileOverride)
          : undefined,
        createdAt: NOW(),
        updatedAt: NOW()
      };
      store.state.bindings.set(id, dto);
      kb.bindingCount = (kb.bindingCount ?? 0) + 1;
      return id;
    },

    async removeBinding(knowledgeBaseId: number, bindingId: number): Promise<void> {
      const kb = ensureKb(store, knowledgeBaseId);
      const existed = store.state.bindings.delete(bindingId);
      if (existed) {
        kb.bindingCount = Math.max(0, (kb.bindingCount ?? 0) - 1);
      }
    },

    async listAllBindings(request: PagedRequest): Promise<PagedResult<KnowledgeBinding>> {
      const items = Array.from(store.state.bindings.values()).map(deepClone);
      return paginate(items, request);
    },

    async listPermissions(knowledgeBaseId: number, request: PagedRequest): Promise<PagedResult<KnowledgePermission>> {
      ensureKb(store, knowledgeBaseId);
      const items = Array.from(store.state.permissions.values())
        .filter(p => p.knowledgeBaseId === knowledgeBaseId || p.scope !== "kb")
        .map(deepClone);
      return paginate(items, request);
    },

    async grantPermission(
      knowledgeBaseId: number,
      request: KnowledgePermissionGrantRequest
    ): Promise<number> {
      ensureKb(store, knowledgeBaseId);
      const id = store.nextPermissionId();
      const dto: KnowledgePermission = {
        id,
        scope: request.scope,
        scopeId: request.scopeId,
        knowledgeBaseId: request.knowledgeBaseId ?? knowledgeBaseId,
        documentId: request.documentId ?? null,
        subjectType: request.subjectType,
        subjectId: request.subjectId,
        subjectName: request.subjectName,
        actions: [...request.actions],
        grantedBy: "admin",
        grantedAt: NOW()
      };
      store.state.permissions.set(id, dto);
      return id;
    },

    async revokePermission(knowledgeBaseId: number, permissionId: number): Promise<void> {
      ensureKb(store, knowledgeBaseId);
      store.state.permissions.delete(permissionId);
    },

    async listVersions(knowledgeBaseId: number, request: PagedRequest): Promise<PagedResult<KnowledgeVersion>> {
      ensureKb(store, knowledgeBaseId);
      const items = Array.from(store.state.versions.values())
        .filter(v => v.knowledgeBaseId === knowledgeBaseId)
        .sort((a, b) => b.id - a.id)
        .map(deepClone);
      return paginate(items, request);
    },

    async createVersionSnapshot(
      knowledgeBaseId: number,
      request: KnowledgeVersionCreateRequest
    ): Promise<number> {
      const kb = ensureKb(store, knowledgeBaseId);
      const id = store.nextVersionId();
      const dto: KnowledgeVersion = {
        id,
        knowledgeBaseId,
        label: request.label,
        note: request.note,
        status: "draft",
        snapshotRef: `snapshot-${knowledgeBaseId}-${id}`,
        documentCount: kb.documentCount,
        chunkCount: kb.chunkCount,
        createdBy: "admin",
        createdAt: NOW()
      };
      store.state.versions.set(id, dto);
      return id;
    },

    async releaseVersion(knowledgeBaseId: number, versionId: number): Promise<void> {
      ensureKb(store, knowledgeBaseId);
      const version = store.state.versions.get(versionId);
      if (!version) throw new Error(`Version ${versionId} not found`);
      version.status = "released";
      version.releasedAt = NOW();
      const kb = store.state.knowledgeBases.get(knowledgeBaseId);
      if (kb) {
        kb.versionLabel = version.label;
      }
    },

    async rollbackToVersion(knowledgeBaseId: number, versionId: number): Promise<void> {
      const kb = ensureKb(store, knowledgeBaseId);
      const version = store.state.versions.get(versionId);
      if (!version) throw new Error(`Version ${versionId} not found`);
      kb.versionLabel = `${version.label} (rolled back)`;
      kb.updatedAt = NOW();
    },

    async diffVersions(
      knowledgeBaseId: number,
      fromVersionId: number,
      toVersionId: number
    ): Promise<KnowledgeVersionDiff> {
      ensureKb(store, knowledgeBaseId);
      const from = store.state.versions.get(fromVersionId);
      const to = store.state.versions.get(toVersionId);
      if (!from || !to) {
        throw new Error("Version not found");
      }
      // v5 §40 / 计划 G8：真 deepDiff（字段级），不再仅返回合成 delta
      const entries: KnowledgeVersionDiff["entries"] = [];
      const fields: Array<keyof typeof from> = [
        "label",
        "note",
        "snapshotRef",
        "documentCount",
        "chunkCount",
        "createdBy",
        "status"
      ];
      for (const field of fields) {
        const fromValue = from[field];
        const toValue = to[field];
        const equal = JSON.stringify(fromValue) === JSON.stringify(toValue);
        if (equal) continue;
        entries.push({
          kind: field as string,
          changeType:
            fromValue === undefined || fromValue === null
              ? "added"
              : toValue === undefined || toValue === null
                ? "removed"
                : "modified",
          ref: `${from.label} → ${to.label}`,
          summary: `${field as string}: ${JSON.stringify(fromValue)} → ${JSON.stringify(toValue)}`
        });
      }
      // 兼容老语义：若没有任何字段差异，仍输出 document/chunk delta 摘要
      if (entries.length === 0) {
        entries.push({
          kind: "summary",
          changeType: "modified",
          ref: `${from.label} → ${to.label}`,
          summary: "no field differences detected"
        });
      }
      return { fromVersionId, toVersionId, entries };
    },

    async listProviderConfigs(): Promise<KnowledgeProviderConfig[]> {
      return Array.from(store.state.providerConfigs.values()).map(deepClone);
    }
  };

  return adapter;
}

function tokenize(query: string): string[] {
  return Array.from(
    new Set(
      query
        .toLowerCase()
        .split(/[\s,，.。;；!！?？/\\()（）\[\]【】{}<>|"'`、]+/u)
        .map(token => token.trim())
        .filter(token => token.length >= 1 && token.length < 32)
    )
  );
}

function scoreChunk(content: string, tokens: string[]): number {
  if (!content || tokens.length === 0) {
    return 0;
  }
  let hits = 0;
  const lower = content.toLowerCase();
  tokens.forEach(token => {
    if (!token) return;
    if (lower.includes(token)) {
      hits += 1;
    }
  });
  return hits / tokens.length;
}
