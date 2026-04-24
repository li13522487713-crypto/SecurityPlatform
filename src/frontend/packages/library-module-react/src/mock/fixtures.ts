import {
  DEFAULT_CHUNKING_PROFILE,
  DEFAULT_PARSING_STRATEGY,
  DEFAULT_RETRIEVAL_PROFILE
} from "../types";
import type {
  ChunkingProfile,
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
  RetrievalProfile
} from "../types";
import type { MockStore } from "./store";

const FIXED_NOW = new Date("2026-04-18T08:00:00.000Z").toISOString();

export interface SeedResult {
  textKbId: number;
  tableKbId: number;
  imageKbId: number;
}

/**
 * 种入三套真实示例：文本 / 表格 / 图片，用于打开页面立即可视、且单测可断言。
 * - 文本 KB：2 个文档 + 6 个 chunk + 1 个绑定（agent）+ 1 个 release 版本
 * - 表格 KB：1 个文档 + 5 列 + 8 行 + 1 个 workflow 绑定
 * - 图片 KB：1 个文档 + 4 张图（带 caption / ocr / tag 标注）
 *
 * 任务种子：每个 KB 种 1 个 Succeeded parse + 1 个 Succeeded index；如果
 * `withFailures=true`，再额外种 1 个 DeadLetter 用于死信演示。
 */
export function seedDefault(store: MockStore, withFailures: boolean): SeedResult {
  seedProviderConfigs(store);

  const text = seedTextKb(store);
  const table = seedTableKb(store);
  const image = seedImageKb(store);

  if (withFailures) {
    seedDeadLetterJob(store, text);
    seedDeadLetterJob(store, table);
  }

  return { textKbId: text, tableKbId: table, imageKbId: image };
}

function buildBaseKb(
  store: MockStore,
  init: {
    name: string;
    description: string;
    type: 0 | 1 | 2;
    kind: KnowledgeBaseDto["kind"];
    tags: string[];
  }
): number {
  const id = store.nextKnowledgeBaseId();
  const now = FIXED_NOW;
  const chunkingProfile: ChunkingProfile =
    init.kind === "table"
      ? { mode: "table-row", size: 1, overlap: 0, separators: ["\n"] }
      : init.kind === "image"
        ? { mode: "image-item", size: 1, overlap: 0, separators: [] }
        : { ...DEFAULT_CHUNKING_PROFILE };
  const retrievalProfile: RetrievalProfile = {
    ...DEFAULT_RETRIEVAL_PROFILE,
    weights: {
      ...DEFAULT_RETRIEVAL_PROFILE.weights,
      table: init.kind === "table" ? 0.6 : 0,
      image: init.kind === "image" ? 0.6 : 0
    }
  };

  store.state.chunkingProfiles.set(id, chunkingProfile);
  store.state.retrievalProfiles.set(id, retrievalProfile);

  const dto: KnowledgeBaseDto = {
    id,
    name: init.name,
    description: init.description,
    type: init.type,
    kind: init.kind,
    providerKind: init.kind === "image" ? "qdrant" : "builtin",
    providerConfigId: init.kind === "image" ? "vector-qdrant-default" : "vector-sqlite-default",
    documentCount: 0,
    chunkCount: 0,
    bindingCount: 0,
    pendingJobCount: 0,
    failedJobCount: 0,
    tags: init.tags,
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
  return id;
}

function pushChunk(
  store: MockStore,
  knowledgeBaseId: number,
  documentId: number,
  index: number,
  content: string,
  rowIndex?: number,
  columnHeadersJson?: string
): number {
  const id = store.nextChunkId();
  store.state.chunks.set(id, {
    id,
    knowledgeBaseId,
    documentId,
    chunkIndex: index,
    content,
    startOffset: index * 256,
    endOffset: index * 256 + content.length,
    hasEmbedding: true,
    createdAt: FIXED_NOW,
    rowIndex: rowIndex ?? null,
    columnHeadersJson: columnHeadersJson ?? null
  });
  return id;
}

function pushDocument(
  store: MockStore,
  knowledgeBaseId: number,
  init: {
    fileName: string;
    contentType: string;
    fileSizeBytes: number;
    chunkCount: number;
    tagsJson?: string;
    imageMetadataJson?: string;
    parsingStrategy?: ParsingStrategy;
  }
): number {
  const id = store.nextDocumentId();
  const parsing = init.parsingStrategy ?? DEFAULT_PARSING_STRATEGY;
  store.state.parsingStrategies.set(id, parsing);
  const dto: KnowledgeDocumentDto = {
    id,
    knowledgeBaseId,
    fileId: id + 100000,
    fileName: init.fileName,
    contentType: init.contentType,
    fileSizeBytes: init.fileSizeBytes,
    status: 2,
    chunkCount: init.chunkCount,
    createdAt: FIXED_NOW,
    processedAt: FIXED_NOW,
    tagsJson: init.tagsJson ?? "[]",
    imageMetadataJson: init.imageMetadataJson ?? "{}",
    lifecycleStatus: "Ready",
    parsingStrategy: parsing,
    parseJobId: null,
    indexJobId: null,
    versionLabel: "v0"
  };
  store.state.documents.set(id, dto);
  return id;
}

function pushSucceededJobPair(
  store: MockStore,
  knowledgeBaseId: number,
  documentId: number
): { parseJobId: number; indexJobId: number } {
  const parseId = store.nextJobId();
  const parseJob: KnowledgeJob = {
    id: parseId,
    knowledgeBaseId,
    documentId,
    type: "parse",
    status: "Succeeded",
    progress: 100,
    attempts: 1,
    maxAttempts: 3,
    enqueuedAt: FIXED_NOW,
    startedAt: FIXED_NOW,
    finishedAt: FIXED_NOW,
    logs: [
      { ts: FIXED_NOW, level: "info", message: "Parse job started" },
      { ts: FIXED_NOW, level: "info", message: "Parse job completed" }
    ]
  };
  store.state.jobs.set(parseId, parseJob);

  const indexId = store.nextJobId();
  const indexJob: KnowledgeJob = {
    id: indexId,
    knowledgeBaseId,
    documentId,
    type: "index",
    status: "Succeeded",
    progress: 100,
    attempts: 1,
    maxAttempts: 3,
    enqueuedAt: FIXED_NOW,
    startedAt: FIXED_NOW,
    finishedAt: FIXED_NOW,
    logs: [
      { ts: FIXED_NOW, level: "info", message: "Index job started" },
      { ts: FIXED_NOW, level: "info", message: "Index job completed" }
    ]
  };
  store.state.jobs.set(indexId, indexJob);

  const doc = store.state.documents.get(documentId);
  if (doc) {
    doc.parseJobId = parseId;
    doc.indexJobId = indexId;
  }

  return { parseJobId: parseId, indexJobId: indexId };
}

function pushBinding(
  store: MockStore,
  knowledgeBaseId: number,
  init: Pick<KnowledgeBinding, "callerType" | "callerId" | "callerName">
): number {
  const id = store.nextBindingId();
  const dto: KnowledgeBinding = {
    id,
    knowledgeBaseId,
    ...init,
    createdAt: FIXED_NOW,
    updatedAt: FIXED_NOW
  };
  store.state.bindings.set(id, dto);
  const kb = store.state.knowledgeBases.get(knowledgeBaseId);
  if (kb) {
    kb.bindingCount = (kb.bindingCount ?? 0) + 1;
  }
  return id;
}

function pushPermission(
  store: MockStore,
  knowledgeBaseId: number,
  init: Omit<KnowledgePermission, "id" | "grantedAt" | "grantedBy" | "knowledgeBaseId">
): number {
  const id = store.nextPermissionId();
  const dto: KnowledgePermission = {
    id,
    knowledgeBaseId,
    ...init,
    grantedBy: "admin",
    grantedAt: FIXED_NOW
  };
  store.state.permissions.set(id, dto);
  return id;
}

function pushVersion(
  store: MockStore,
  knowledgeBaseId: number,
  init: Omit<KnowledgeVersion, "id" | "knowledgeBaseId" | "createdAt" | "createdBy" | "snapshotRef">
): number {
  const id = store.nextVersionId();
  const dto: KnowledgeVersion = {
    id,
    knowledgeBaseId,
    snapshotRef: `snapshot-${knowledgeBaseId}-${id}`,
    createdBy: "admin",
    createdAt: FIXED_NOW,
    ...init
  };
  store.state.versions.set(id, dto);
  return id;
}

function pushTableColumn(
  store: MockStore,
  knowledgeBaseId: number,
  documentId: number,
  init: Omit<KnowledgeTableColumn, "id" | "knowledgeBaseId" | "documentId">
): number {
  const id = store.nextColumnId();
  store.state.tableColumns.set(id, { id, knowledgeBaseId, documentId, ...init });
  return id;
}

function pushTableRow(
  store: MockStore,
  knowledgeBaseId: number,
  documentId: number,
  rowIndex: number,
  cells: Record<string, string | number | boolean>,
  chunkId?: number | null
): number {
  const id = store.nextRowId();
  store.state.tableRows.set(id, {
    id,
    knowledgeBaseId,
    documentId,
    rowIndex,
    cellsJson: JSON.stringify(cells),
    chunkId: chunkId ?? null
  });
  return id;
}

function pushImageItem(
  store: MockStore,
  knowledgeBaseId: number,
  documentId: number,
  init: Omit<KnowledgeImageItem, "id" | "knowledgeBaseId" | "documentId">
): number {
  const id = store.nextImageItemId();
  store.state.imageItems.set(id, { id, knowledgeBaseId, documentId, ...init });
  return id;
}

function seedTextKb(store: MockStore): number {
  const id = buildBaseKb(store, {
    name: "等保 2.0 平台知识库",
    description: "覆盖等保 2.0 控制项、Atlas 平台架构与运行手册的文本知识库。",
    type: 0,
    kind: "text",
    tags: ["平台", "等保2.0"]
  });

  const docPlatformId = pushDocument(store, id, {
    fileName: "等保2.0要求清单.md",
    contentType: "text/markdown",
    fileSizeBytes: 152_400,
    chunkCount: 4
  });
  pushChunk(store, id, docPlatformId, 0, "应用建立统一的安全控制基线，覆盖身份认证、访问控制与审计。");
  pushChunk(store, id, docPlatformId, 1, "平台执行最小权限原则，并按租户与项目维度隔离数据访问范围。");
  pushChunk(store, id, docPlatformId, 2, "审计日志需保留 6 个月以上，并支持按租户、用户、资源进行检索。");
  pushChunk(store, id, docPlatformId, 3, "敏感字段加密存储，密钥需走集中密钥管理系统并可轮换。");
  pushSucceededJobPair(store, id, docPlatformId);

  const docOpsId = pushDocument(store, id, {
    fileName: "Atlas 平台运维手册.docx",
    contentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    fileSizeBytes: 320_888,
    chunkCount: 2,
    parsingStrategy: { ...DEFAULT_PARSING_STRATEGY, parsingType: "precise", extractTable: true }
  });
  pushChunk(store, id, docOpsId, 0, "AppHost 通过 SqlSugar 连接 atlas.app.e2e.db；Hangfire 任务由应用运行时统一调度。");
  pushChunk(store, id, docOpsId, 1, "部署时需先初始化 BootstrapAdmin 账号，并在 Setup Console 完成租户/工作空间设置。");
  pushSucceededJobPair(store, id, docOpsId);

  pushBinding(store, id, {
    callerType: "agent",
    callerId: "agent_security_assistant",
    callerName: "安全助手 Agent"
  });
  pushBinding(store, id, {
    callerType: "workflow",
    callerId: "wf_security_audit",
    callerName: "安全审计工作流"
  });

  pushPermission(store, id, {
    scope: "kb",
    scopeId: String(id),
    knowledgeBaseId: id,
    documentId: null,
    subjectType: "role",
    subjectId: "role_security_owner",
    subjectName: "安全负责人",
    actions: ["view", "edit", "publish", "manage", "retrieve"]
  });
  pushPermission(store, id, {
    scope: "space",
    scopeId: "ws-default",
    knowledgeBaseId: null,
    documentId: null,
    subjectType: "group",
    subjectId: "group_security_team",
    subjectName: "安全运营组",
    actions: ["view", "retrieve"]
  });

  pushVersion(store, id, {
    label: "v1.0.0",
    note: "首次发布：覆盖等保2.0 控制项摘要与运维手册。",
    status: "released",
    documentCount: 2,
    chunkCount: 6,
    releasedAt: FIXED_NOW
  });
  pushVersion(store, id, {
    label: "v1.1.0-draft",
    note: "草稿：补充密钥管理与日志归档实践。",
    status: "draft",
    documentCount: 2,
    chunkCount: 6
  });

  syncKbCounters(store, id);
  return id;
}

function seedTableKb(store: MockStore): number {
  const id = buildBaseKb(store, {
    name: "员工与项目台账",
    description: "结构化表格知识库：员工信息、项目归属、岗位级别。",
    type: 1,
    kind: "table",
    tags: ["人事", "项目"]
  });

  const docId = pushDocument(store, id, {
    fileName: "员工台账.csv",
    contentType: "text/csv",
    fileSizeBytes: 24_512,
    chunkCount: 8,
    parsingStrategy: {
      ...DEFAULT_PARSING_STRATEGY,
      parsingType: "precise",
      extractTable: true,
      sheetId: "Sheet1",
      headerLine: 1,
      dataStartLine: 2,
      rowsCount: 8
    }
  });

  const headers = ["姓名", "工号", "部门", "岗位", "项目"];
  const headersJson = JSON.stringify(headers);

  pushTableColumn(store, id, docId, { ordinal: 0, name: "姓名", isIndexColumn: true, dataType: "string" });
  pushTableColumn(store, id, docId, { ordinal: 1, name: "工号", isIndexColumn: true, dataType: "string" });
  pushTableColumn(store, id, docId, { ordinal: 2, name: "部门", isIndexColumn: true, dataType: "string" });
  pushTableColumn(store, id, docId, { ordinal: 3, name: "岗位", isIndexColumn: false, dataType: "string" });
  pushTableColumn(store, id, docId, { ordinal: 4, name: "项目", isIndexColumn: false, dataType: "string" });

  const sampleRows: Array<Record<string, string>> = [
    { 姓名: "李明", 工号: "E0001", 部门: "安全部", 岗位: "高级工程师", 项目: "Atlas 平台" },
    { 姓名: "王芳", 工号: "E0002", 部门: "运维部", 岗位: "工程师", 项目: "Atlas 平台" },
    { 姓名: "张伟", 工号: "E0003", 部门: "研发部", 岗位: "架构师", 项目: "知识库专题" },
    { 姓名: "赵敏", 工号: "E0004", 部门: "研发部", 岗位: "工程师", 项目: "知识库专题" },
    { 姓名: "周亮", 工号: "E0005", 部门: "数据部", 岗位: "数据分析师", 项目: "运营数据中台" },
    { 姓名: "孙丽", 工号: "E0006", 部门: "安全部", 岗位: "工程师", 项目: "等保2.0 改造" },
    { 姓名: "钱进", 工号: "E0007", 部门: "运维部", 岗位: "高级工程师", 项目: "等保2.0 改造" },
    { 姓名: "吴霞", 工号: "E0008", 部门: "产品部", 岗位: "产品经理", 项目: "Atlas 平台" }
  ];

  sampleRows.forEach((row, idx) => {
    const summary = headers.map(header => `${header}=${row[header]}`).join(" | ");
    const chunkId = pushChunk(store, id, docId, idx, summary, idx + 1, headersJson);
    pushTableRow(store, id, docId, idx + 1, row, chunkId);
  });

  pushSucceededJobPair(store, id, docId);

  pushBinding(store, id, {
    callerType: "workflow",
    callerId: "wf_employee_search",
    callerName: "员工查询工作流"
  });

  pushPermission(store, id, {
    scope: "kb",
    scopeId: String(id),
    knowledgeBaseId: id,
    documentId: null,
    subjectType: "role",
    subjectId: "role_hr_manager",
    subjectName: "人事经理",
    actions: ["view", "edit", "retrieve"]
  });

  pushVersion(store, id, {
    label: "v1.0.0",
    note: "员工台账初始版本",
    status: "released",
    documentCount: 1,
    chunkCount: 8,
    releasedAt: FIXED_NOW
  });

  syncKbCounters(store, id);
  return id;
}

function seedImageKb(store: MockStore): number {
  const id = buildBaseKb(store, {
    name: "运维巡检图集",
    description: "图片知识库：机房巡检、应急演练、安全提示等图片素材。",
    type: 2,
    kind: "image",
    tags: ["巡检", "应急演练"]
  });

  const docId = pushDocument(store, id, {
    fileName: "巡检图集 2026Q1.zip",
    contentType: "application/zip",
    fileSizeBytes: 8_421_504,
    chunkCount: 4,
    imageMetadataJson: JSON.stringify({ batch: "2026Q1", reviewer: "孙丽" }),
    parsingStrategy: {
      ...DEFAULT_PARSING_STRATEGY,
      parsingType: "precise",
      extractImage: true,
      imageOcr: true,
      captionType: "auto-vlm"
    }
  });

  const items: Array<{
    fileName: string;
    caption: string;
    ocr?: string;
    tags: string[];
  }> = [
    { fileName: "rack-01.jpg", caption: "1 号机柜整体外观", ocr: "Atlas-Rack-01", tags: ["机柜", "整体"] },
    {
      fileName: "rack-01-cable.jpg",
      caption: "1 号机柜走线特写",
      ocr: "Cable-Map",
      tags: ["机柜", "走线"]
    },
    {
      fileName: "fire-drill-01.jpg",
      caption: "消防应急演练现场",
      tags: ["应急演练", "消防"]
    },
    {
      fileName: "warning-sign.jpg",
      caption: "高压设备警示牌",
      ocr: "DANGER HIGH VOLTAGE",
      tags: ["安全提示", "警示牌"]
    }
  ];

  items.forEach((item, idx) => {
    pushChunk(store, id, docId, idx, item.caption);
    const annotations: KnowledgeImageItem["annotations"] = [];
    annotations.push({ id: store.nextImageItemId(), imageItemId: 0, type: "caption", text: item.caption, confidence: 0.92 });
    if (item.ocr) {
      annotations.push({ id: store.nextImageItemId(), imageItemId: 0, type: "ocr", text: item.ocr, confidence: 0.85 });
    }
    item.tags.forEach(tag => {
      annotations.push({ id: store.nextImageItemId(), imageItemId: 0, type: "tag", text: tag });
    });

    const itemId = pushImageItem(store, id, docId, {
      fileName: item.fileName,
      width: 1280,
      height: 720,
      thumbnailUrl: `/mock-assets/${item.fileName}`,
      annotations: annotations.map(annotation => ({ ...annotation, imageItemId: 0 }))
    });
    const stored = store.state.imageItems.get(itemId);
    if (stored) {
      stored.annotations = stored.annotations.map(annotation => ({ ...annotation, imageItemId: itemId }));
    }
  });

  pushSucceededJobPair(store, id, docId);

  pushBinding(store, id, {
    callerType: "app",
    callerId: "app_inspection_dashboard",
    callerName: "巡检运营看板"
  });

  pushPermission(store, id, {
    scope: "kb",
    scopeId: String(id),
    knowledgeBaseId: id,
    documentId: null,
    subjectType: "role",
    subjectId: "role_ops_lead",
    subjectName: "运维负责人",
    actions: ["view", "edit", "delete", "retrieve"]
  });

  pushVersion(store, id, {
    label: "v1.0.0",
    note: "2026Q1 巡检图集上线",
    status: "released",
    documentCount: 1,
    chunkCount: 4,
    releasedAt: FIXED_NOW
  });

  syncKbCounters(store, id);
  return id;
}

function seedDeadLetterJob(store: MockStore, knowledgeBaseId: number): number {
  const id = store.nextJobId();
  const job: KnowledgeJob = {
    id,
    knowledgeBaseId,
    documentId: null,
    type: "rebuild",
    status: "DeadLetter",
    progress: 65,
    attempts: 3,
    maxAttempts: 3,
    enqueuedAt: FIXED_NOW,
    startedAt: FIXED_NOW,
    finishedAt: FIXED_NOW,
    errorMessage: "向量库维度不一致：当前模型 1536 维，目标索引 768 维。",
    logs: [
      { ts: FIXED_NOW, level: "warn", message: "Vector dim mismatch detected on retry #1" },
      { ts: FIXED_NOW, level: "warn", message: "Vector dim mismatch detected on retry #2" },
      { ts: FIXED_NOW, level: "error", message: "Job moved to dead-letter after 3 attempts" }
    ]
  };
  store.state.jobs.set(id, job);
  const kb = store.state.knowledgeBases.get(knowledgeBaseId);
  if (kb) {
    kb.failedJobCount = (kb.failedJobCount ?? 0) + 1;
  }
  return id;
}

export function syncKbCounters(store: MockStore, knowledgeBaseId: number): void {
  const kb = store.state.knowledgeBases.get(knowledgeBaseId);
  if (!kb) return;

  let documentCount = 0;
  let chunkCount = 0;
  store.state.documents.forEach(doc => {
    if (doc.knowledgeBaseId === knowledgeBaseId) {
      documentCount += 1;
      chunkCount += doc.chunkCount;
    }
  });

  let bindingCount = 0;
  store.state.bindings.forEach(binding => {
    if (binding.knowledgeBaseId === knowledgeBaseId) bindingCount += 1;
  });

  let pendingJobCount = 0;
  let failedJobCount = 0;
  store.state.jobs.forEach(job => {
    if (job.knowledgeBaseId !== knowledgeBaseId) return;
    if (job.status === "Queued" || job.status === "Running" || job.status === "Retrying") {
      pendingJobCount += 1;
    }
    if (job.status === "Failed" || job.status === "DeadLetter") {
      failedJobCount += 1;
    }
  });

  kb.documentCount = documentCount;
  kb.chunkCount = chunkCount;
  kb.bindingCount = bindingCount;
  kb.pendingJobCount = pendingJobCount;
  kb.failedJobCount = failedJobCount;
  kb.updatedAt = FIXED_NOW;
}

function seedProviderConfigs(store: MockStore): void {
  const now = FIXED_NOW;
  const configs: KnowledgeProviderConfig[] = [
    {
      id: "upload-minio-default",
      role: "upload",
      providerName: "minio",
      displayName: "MinIO 上传",
      endpoint: "http://127.0.0.1:9000",
      bucketOrIndex: "atlas-files",
      isDefault: true,
      status: "active",
      updatedAt: now
    },
    {
      id: "storage-minio-default",
      role: "storage",
      providerName: "minio",
      displayName: "MinIO 对象存储",
      endpoint: "http://127.0.0.1:9000",
      bucketOrIndex: "atlas-files",
      isDefault: true,
      status: "active",
      updatedAt: now
    },
    {
      id: "vector-sqlite-default",
      role: "vector",
      providerName: "sqlite",
      displayName: "SQLite 向量索引",
      bucketOrIndex: "atlas.db",
      isDefault: true,
      status: "active",
      updatedAt: now
    },
    {
      id: "vector-qdrant-default",
      role: "vector",
      providerName: "qdrant",
      displayName: "Qdrant 向量索引",
      endpoint: "http://localhost:6333",
      bucketOrIndex: "atlas-knowledge",
      isDefault: false,
      status: "active",
      updatedAt: now
    },
    {
      id: "embedding-openai-default",
      role: "embedding",
      providerName: "openai",
      displayName: "OpenAI text-embedding-3-small",
      bucketOrIndex: "text-embedding-3-small",
      isDefault: true,
      status: "active",
      updatedAt: now
    },
    {
      id: "generation-default",
      role: "generation",
      providerName: "openai",
      displayName: "OpenAI gpt-4o-mini",
      bucketOrIndex: "gpt-4o-mini",
      isDefault: true,
      status: "active",
      updatedAt: now
    }
  ];

  configs.forEach(config => store.state.providerConfigs.set(config.id, config));
}
