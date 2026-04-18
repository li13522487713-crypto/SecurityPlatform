import type {
  ChunkingProfile,
  DocumentChunkDto,
  KnowledgeBaseDto,
  KnowledgeBinding,
  KnowledgeDocumentDto,
  KnowledgeImageItem,
  KnowledgeJob,
  KnowledgePermission,
  KnowledgeProviderConfig,
  KnowledgeTableColumn,
  KnowledgeTableRow,
  KnowledgeVersion,
  ParsingStrategy,
  RetrievalLog,
  RetrievalProfile
} from "../types";

/**
 * In-memory mock store. 单例存活在 page 生命周期内；测试时由调用方手动 reset。
 *
 * 设计要点：
 * - 用 Map<id, entity> 模拟仓储，避免数组遍历 O(n)
 * - id 由内部 sequence 分配，不暴露 Math.random，方便单测断言
 * - parsingStrategy/chunkingProfile/retrievalProfile 默认值由 fixtures 写入，单测可覆盖
 */
export interface MockStoreState {
  knowledgeBases: Map<number, KnowledgeBaseDto>;
  documents: Map<number, KnowledgeDocumentDto>;
  chunks: Map<number, DocumentChunkDto>;
  jobs: Map<number, KnowledgeJob>;
  bindings: Map<number, KnowledgeBinding>;
  permissions: Map<number, KnowledgePermission>;
  versions: Map<number, KnowledgeVersion>;
  retrievalLogs: Map<string, RetrievalLog>;
  providerConfigs: Map<string, KnowledgeProviderConfig>;
  tableColumns: Map<number, KnowledgeTableColumn>;
  tableRows: Map<number, KnowledgeTableRow>;
  imageItems: Map<number, KnowledgeImageItem>;
  /** kbId → ChunkingProfile/RetrievalProfile */
  chunkingProfiles: Map<number, ChunkingProfile>;
  retrievalProfiles: Map<number, RetrievalProfile>;
  /** docId → ParsingStrategy */
  parsingStrategies: Map<number, ParsingStrategy>;
}

export class MockStore {
  state: MockStoreState;
  private kbSeq = 0;
  private docSeq = 0;
  private chunkSeq = 0;
  private jobSeq = 0;
  private bindingSeq = 0;
  private permissionSeq = 0;
  private versionSeq = 0;
  private columnSeq = 0;
  private rowSeq = 0;
  private imageSeq = 0;
  private logSeq = 0;

  constructor() {
    this.state = {
      knowledgeBases: new Map(),
      documents: new Map(),
      chunks: new Map(),
      jobs: new Map(),
      bindings: new Map(),
      permissions: new Map(),
      versions: new Map(),
      retrievalLogs: new Map(),
      providerConfigs: new Map(),
      tableColumns: new Map(),
      tableRows: new Map(),
      imageItems: new Map(),
      chunkingProfiles: new Map(),
      retrievalProfiles: new Map(),
      parsingStrategies: new Map()
    };
  }

  nextKnowledgeBaseId(): number {
    this.kbSeq += 1;
    return this.kbSeq;
  }
  nextDocumentId(): number {
    this.docSeq += 1;
    return this.docSeq;
  }
  nextChunkId(): number {
    this.chunkSeq += 1;
    return this.chunkSeq;
  }
  nextJobId(): number {
    this.jobSeq += 1;
    return this.jobSeq;
  }
  nextBindingId(): number {
    this.bindingSeq += 1;
    return this.bindingSeq;
  }
  nextPermissionId(): number {
    this.permissionSeq += 1;
    return this.permissionSeq;
  }
  nextVersionId(): number {
    this.versionSeq += 1;
    return this.versionSeq;
  }
  nextColumnId(): number {
    this.columnSeq += 1;
    return this.columnSeq;
  }
  nextRowId(): number {
    this.rowSeq += 1;
    return this.rowSeq;
  }
  nextImageItemId(): number {
    this.imageSeq += 1;
    return this.imageSeq;
  }
  nextTraceId(): string {
    this.logSeq += 1;
    return `trace_${String(this.logSeq).padStart(6, "0")}`;
  }
}

export function deepClone<T>(value: T): T {
  // 优先使用宿主提供的 structuredClone，单测/SSR 环境则回退 JSON 拷贝。
  // 使用 typeof 检查避免在没有 structuredClone 的旧环境抛 ReferenceError。
  const sc = (globalThis as { structuredClone?: <U>(input: U) => U }).structuredClone;
  if (typeof sc === "function") {
    return sc(value);
  }
  return JSON.parse(JSON.stringify(value)) as T;
}
