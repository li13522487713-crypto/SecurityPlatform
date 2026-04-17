import type { ApiResponse } from "@atlas/shared-react-core/types";
import type {
  DataMigrationBatchDto,
  DataMigrationCutoverRequest,
  DataMigrationJobCreateRequest,
  DataMigrationJobDto,
  DataMigrationLogItemDto,
  DataMigrationLogPagedResponse,
  DataMigrationProgressDto,
  DataMigrationReportDto,
  MigrationTestConnectionRequest,
  MigrationTestConnectionResponse
} from "../api-setup-console";
import { mockApiResponse, mockReject, MOCK_DELAY_MS } from "./mock-utils";
import {
  appendMigrationBatch,
  appendMigrationLog,
  getMigration,
  listMigrationJobs,
  setActiveMigration,
  setMigrationReport,
  upsertMigration
} from "./setup-console-store";
import type { DataMigrationState } from "../../app/setup-console-state-machine";

/**
 * 数据迁移 mock：模拟 ORM 跨库迁移完整生命周期。
 *
 * 关键约束（与 docs/contracts.md 12.4 一致）：
 * - 防重复指纹：相同 source/target 指纹的"已 cutover-completed"任务，必须显式 `allowReExecute=true` 才能再次创建。
 * - 断点续跑：retry 后从最后失败的实体的下一批次继续；mock 简化为重置 progress 为 70% 后再跑完。
 * - 切主：cutover 成功后 active migration 清空；UI 总览不再展示。
 */

const MOCK_TOTAL_ENTITIES = 211;
const MOCK_TOTAL_ROWS = 124680;

function fingerprint(prefix: string, raw: string): string {
  // mock 简化指纹算法：截取前 32 字节。
  return `${prefix}-${raw.slice(0, 32)}`;
}

function nowIso(): string {
  return new Date().toISOString();
}

function appendLog(jobId: string, level: "info" | "warn" | "error", message: string, entityName?: string): DataMigrationLogItemDto {
  const log: DataMigrationLogItemDto = {
    id: `log-${Math.random().toString(36).slice(2, 10)}`,
    jobId,
    level,
    module: "OrmDataMigrationService",
    entityName: entityName ?? null,
    message,
    occurredAt: nowIso()
  };
  appendMigrationLog(jobId, log);
  return log;
}

export async function testMigrationConnection(
  request: MigrationTestConnectionRequest
): Promise<ApiResponse<MigrationTestConnectionResponse>> {
  if (!request.connection) {
    return mockReject("VALIDATION_ERROR", "connection is required", MOCK_DELAY_MS);
  }
  // 简化：raw 模式有非空 connectionString 就认为连接成功；visual 模式有 host/port 就成功。
  const isRawOk = request.connection.mode === "raw" && (request.connection.connectionString ?? "").trim().length > 0;
  const isVisualOk =
    request.connection.mode === "visual" &&
    Object.keys(request.connection.visualConfig ?? {}).length > 0;
  if (!isRawOk && !isVisualOk) {
    return mockApiResponse<MigrationTestConnectionResponse>({
      connected: false,
      message: "connection string is empty",
      detectedDbType: null,
      detectedTableCount: 0
    });
  }
  return mockApiResponse<MigrationTestConnectionResponse>({
    connected: true,
    message: "connection successful",
    detectedDbType: request.connection.dbType,
    detectedTableCount: 280
  });
}

export async function createMigrationJob(
  request: DataMigrationJobCreateRequest
): Promise<ApiResponse<DataMigrationJobDto>> {
  if (!request.source || !request.target) {
    return mockReject("VALIDATION_ERROR", "source/target are required", MOCK_DELAY_MS);
  }

  const sourceFingerprint = fingerprint(
    request.source.dbType,
    request.source.connectionString ?? JSON.stringify(request.source.visualConfig ?? {})
  );
  const targetFingerprint = fingerprint(
    request.target.dbType,
    request.target.connectionString ?? JSON.stringify(request.target.visualConfig ?? {})
  );

  // 防重复指纹：同 source+target 已有 cutover-completed 任务且未显式 allowReExecute 的，拒绝。
  const existing = listMigrationJobs().find(
    (job) => job.sourceFingerprint === sourceFingerprint && job.targetFingerprint === targetFingerprint
  );
  if (existing && existing.state === "cutover-completed" && !request.allowReExecute) {
    return mockReject(
      "VALIDATION_ERROR",
      "an identical migration has already completed; set allowReExecute=true to re-run",
      MOCK_DELAY_MS
    );
  }

  const jobId = `migration-${Math.random().toString(36).slice(2, 10)}`;
  const job: DataMigrationJobDto = {
    id: jobId,
    state: "pending",
    mode: request.mode,
    source: request.source,
    target: request.target,
    sourceFingerprint,
    targetFingerprint,
    moduleScope: request.moduleScope,
    totalEntities: MOCK_TOTAL_ENTITIES,
    completedEntities: 0,
    failedEntities: 0,
    totalRows: MOCK_TOTAL_ROWS,
    copiedRows: 0,
    progressPercent: 0,
    currentEntityName: null,
    currentBatchNo: null,
    startedAt: null,
    finishedAt: null,
    errorSummary: null,
    createdAt: nowIso(),
    updatedAt: nowIso()
  };
  upsertMigration({ job, batches: [], logs: [], report: null });
  setActiveMigration(jobId);
  appendLog(jobId, "info", `migration job created: mode=${request.mode}`);
  return mockApiResponse<DataMigrationJobDto>(job);
}

export async function precheckMigrationJob(jobId: string): Promise<ApiResponse<DataMigrationJobDto>> {
  const entry = getMigration(jobId);
  if (!entry) {
    return mockReject("NOT_FOUND", "migration job not found", MOCK_DELAY_MS);
  }
  entry.job = { ...entry.job, state: "prechecking", updatedAt: nowIso() };
  appendLog(jobId, "info", "precheck started");
  upsertMigration(entry);

  // 直接推进到 ready
  entry.job = { ...entry.job, state: "ready", updatedAt: nowIso() };
  appendLog(jobId, "info", "precheck passed: connectivity, DDL grants, write permissions all OK");
  upsertMigration(entry);
  return mockApiResponse<DataMigrationJobDto>(entry.job);
}

export async function startMigrationJob(jobId: string): Promise<ApiResponse<DataMigrationJobDto>> {
  const entry = getMigration(jobId);
  if (!entry) {
    return mockReject("NOT_FOUND", "migration job not found", MOCK_DELAY_MS);
  }
  if (entry.job.state !== "ready" && entry.job.state !== "failed") {
    return mockReject(
      "VALIDATION_ERROR",
      `cannot start migration from state ${entry.job.state}`,
      MOCK_DELAY_MS
    );
  }

  const startedAt = nowIso();
  entry.job = {
    ...entry.job,
    state: "running",
    startedAt,
    completedEntities: 0,
    copiedRows: 0,
    progressPercent: 0,
    currentEntityName: "Tenant",
    currentBatchNo: 1,
    errorSummary: null,
    updatedAt: startedAt
  };
  appendLog(jobId, "info", "migration started: copying entities in topological order");
  appendBatchSnapshot(jobId, "Tenant", 1, 1);
  upsertMigration(entry);

  return mockApiResponse<DataMigrationJobDto>(entry.job);
}

export async function getMigrationProgress(jobId: string): Promise<ApiResponse<DataMigrationProgressDto>> {
  const entry = getMigration(jobId);
  if (!entry) {
    return mockReject("NOT_FOUND", "migration job not found", MOCK_DELAY_MS);
  }

  // 每次轮询自动推进 5%，模拟后台进度
  const next = Math.min(entry.job.progressPercent + 5, 100);
  const completedEntities = Math.floor((next / 100) * MOCK_TOTAL_ENTITIES);
  const copiedRows = Math.floor((next / 100) * MOCK_TOTAL_ROWS);
  const isDone = next >= 100;
  const nextState: DataMigrationState = isDone ? "validating" : "running";

  const entityRotation = ["Tenant", "UserAccount", "Role", "Workspace", "Agent", "WorkflowMeta", "AuditRecord"];
  const currentEntity = entityRotation[Math.floor(next / 15) % entityRotation.length];
  const currentBatch = Math.floor(next / 5);

  entry.job = {
    ...entry.job,
    state: nextState,
    completedEntities,
    copiedRows,
    progressPercent: next,
    currentEntityName: currentEntity,
    currentBatchNo: currentBatch,
    updatedAt: nowIso()
  };
  if (next % 15 === 0) {
    appendBatchSnapshot(jobId, currentEntity, currentBatch, Math.floor(MOCK_TOTAL_ROWS / 50));
  }
  upsertMigration(entry);

  return mockApiResponse<DataMigrationProgressDto>({
    jobId,
    state: entry.job.state,
    totalEntities: entry.job.totalEntities,
    completedEntities: entry.job.completedEntities,
    failedEntities: entry.job.failedEntities,
    totalRows: entry.job.totalRows,
    copiedRows: entry.job.copiedRows,
    progressPercent: entry.job.progressPercent,
    currentEntityName: entry.job.currentEntityName,
    currentBatchNo: entry.job.currentBatchNo,
    updatedAt: entry.job.updatedAt,
    recentBatches: entry.batches.slice(-5)
  });
}

export async function validateMigrationJob(jobId: string): Promise<ApiResponse<DataMigrationReportDto>> {
  const entry = getMigration(jobId);
  if (!entry) {
    return mockReject("NOT_FOUND", "migration job not found", MOCK_DELAY_MS);
  }
  if (entry.job.state !== "validating" && entry.job.state !== "running") {
    return mockReject(
      "VALIDATION_ERROR",
      `cannot validate migration from state ${entry.job.state}`,
      MOCK_DELAY_MS
    );
  }

  const report: DataMigrationReportDto = {
    jobId,
    totalEntities: MOCK_TOTAL_ENTITIES,
    passedEntities: MOCK_TOTAL_ENTITIES,
    failedEntities: 0,
    rowDiff: [
      { entityName: "Tenant", sourceRowCount: 1, targetRowCount: 1, diff: 0 },
      { entityName: "UserAccount", sourceRowCount: 312, targetRowCount: 312, diff: 0 },
      { entityName: "WorkflowMeta", sourceRowCount: 87, targetRowCount: 87, diff: 0 }
    ],
    samplingDiff: [
      { entityName: "UserAccount", sampledRows: 16, mismatched: 0, mismatchedExamples: [] },
      { entityName: "Agent", sampledRows: 24, mismatched: 0, mismatchedExamples: [] }
    ],
    overallPassed: true,
    generatedAt: nowIso()
  };
  setMigrationReport(jobId, report);
  entry.job = { ...entry.job, state: "cutover-ready", updatedAt: nowIso() };
  appendLog(jobId, "info", `validation passed: ${MOCK_TOTAL_ENTITIES} entities OK`);
  upsertMigration(entry);
  return mockApiResponse<DataMigrationReportDto>(report);
}

export async function cutoverMigrationJob(
  jobId: string,
  request: DataMigrationCutoverRequest
): Promise<ApiResponse<DataMigrationJobDto>> {
  const entry = getMigration(jobId);
  if (!entry) {
    return mockReject("NOT_FOUND", "migration job not found", MOCK_DELAY_MS);
  }
  if (entry.job.state !== "cutover-ready" && entry.job.state !== "validating") {
    return mockReject(
      "VALIDATION_ERROR",
      `cannot cutover from state ${entry.job.state}`,
      MOCK_DELAY_MS
    );
  }

  const finishedAt = nowIso();
  entry.job = {
    ...entry.job,
    state: "cutover-completed",
    finishedAt,
    progressPercent: 100,
    updatedAt: finishedAt
  };
  appendLog(
    jobId,
    "info",
    `cutover completed; source kept readonly for ${request.keepSourceReadonlyForDays} days`
  );
  upsertMigration(entry);
  setActiveMigration(null);
  return mockApiResponse<DataMigrationJobDto>(entry.job);
}

export async function rollbackMigrationJob(jobId: string): Promise<ApiResponse<DataMigrationJobDto>> {
  const entry = getMigration(jobId);
  if (!entry) {
    return mockReject("NOT_FOUND", "migration job not found", MOCK_DELAY_MS);
  }
  if (entry.job.state === "cutover-completed") {
    return mockReject(
      "VALIDATION_ERROR",
      "cannot rollback an already cutover-completed migration; create a new reverse job instead",
      MOCK_DELAY_MS
    );
  }

  entry.job = {
    ...entry.job,
    state: "rolled-back",
    finishedAt: nowIso(),
    updatedAt: nowIso()
  };
  appendLog(jobId, "warn", "migration rolled back by user request");
  upsertMigration(entry);
  setActiveMigration(null);
  return mockApiResponse<DataMigrationJobDto>(entry.job);
}

export async function retryMigrationJob(jobId: string): Promise<ApiResponse<DataMigrationJobDto>> {
  const entry = getMigration(jobId);
  if (!entry) {
    return mockReject("NOT_FOUND", "migration job not found", MOCK_DELAY_MS);
  }
  if (entry.job.state !== "failed" && entry.job.state !== "rolled-back") {
    return mockReject(
      "VALIDATION_ERROR",
      `cannot retry migration from state ${entry.job.state}`,
      MOCK_DELAY_MS
    );
  }
  entry.job = {
    ...entry.job,
    state: "ready",
    errorSummary: null,
    updatedAt: nowIso()
  };
  appendLog(jobId, "info", "retry triggered: ready to resume from last checkpoint");
  upsertMigration(entry);
  setActiveMigration(jobId);
  return mockApiResponse<DataMigrationJobDto>(entry.job);
}

export async function getMigrationReport(jobId: string): Promise<ApiResponse<DataMigrationReportDto>> {
  const entry = getMigration(jobId);
  if (!entry) {
    return mockReject("NOT_FOUND", "migration job not found", MOCK_DELAY_MS);
  }
  if (!entry.report) {
    return mockReject("NOT_FOUND", "report not generated yet; call validate first", MOCK_DELAY_MS);
  }
  return mockApiResponse<DataMigrationReportDto>(entry.report);
}

export async function getMigrationLogs(
  jobId: string,
  query?: { level?: "info" | "warn" | "error"; pageIndex?: number; pageSize?: number }
): Promise<ApiResponse<DataMigrationLogPagedResponse>> {
  const entry = getMigration(jobId);
  if (!entry) {
    return mockReject("NOT_FOUND", "migration job not found", MOCK_DELAY_MS);
  }
  const filtered = query?.level ? entry.logs.filter((log) => log.level === query.level) : entry.logs;
  const pageIndex = query?.pageIndex ?? 1;
  const pageSize = query?.pageSize ?? 20;
  const start = (pageIndex - 1) * pageSize;
  return mockApiResponse<DataMigrationLogPagedResponse>({
    items: filtered.slice(start, start + pageSize),
    total: filtered.length,
    pageIndex,
    pageSize
  });
}

export async function listMigrationJobsMock(): Promise<ApiResponse<DataMigrationJobDto[]>> {
  return mockApiResponse<DataMigrationJobDto[]>(listMigrationJobs());
}

function appendBatchSnapshot(jobId: string, entityName: string, batchNo: number, rowsCopied: number): void {
  const batch: DataMigrationBatchDto = {
    batchNo,
    entityName,
    rowsCopied,
    state: "succeeded",
    startedAt: nowIso(),
    endedAt: nowIso(),
    checksum: `mock-${Math.random().toString(36).slice(2, 10)}`
  };
  appendMigrationBatch(jobId, batch);
}
