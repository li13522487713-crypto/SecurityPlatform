import type { ApiResponse } from "@atlas/shared-react-core/types";
import type {
  DataMigrationMode,
  SetupConsoleStep,
  SystemSetupState,
  WorkspaceSetupState
} from "../app/setup-console-state-machine";

/**
 * 系统初始化与迁移控制台真接口 client。
 *
 * - 数据迁移主链路使用真实 HTTP 端点，不再通过 mock 推进状态或进度。
 * - DTO 类型与真实接口共用，便于 setup-console 页面和资源库迁移向导复用。
 *
 * 全部协议详见 `docs/contracts.md` "12. 系统初始化与迁移控制台"。
 */

// ============================================================================
// 控制台总览
// ============================================================================

export interface SetupConsoleOverviewDto {
  system: SystemSetupStateDto;
  workspaces: WorkspaceSetupStateDto[];
  activeMigration: DataMigrationJobDto | null;
  catalogSummary: SetupConsoleCatalogSummaryDto;
}

export interface SystemSetupStateDto {
  state: SystemSetupState;
  version: string;
  lastUpdatedAt: string;
  failureMessage: string | null;
  recoveryKeyConfigured: boolean;
  steps: SetupStepRecordDto[];
}

export interface SetupStepRecordDto {
  step: SetupConsoleStep;
  state: "running" | "succeeded" | "failed" | "skipped";
  startedAt: string | null;
  endedAt: string | null;
  attemptCount: number;
  errorMessage: string | null;
}

export interface WorkspaceSetupStateDto {
  workspaceId: string;
  workspaceName: string;
  state: WorkspaceSetupState;
  seedBundleVersion: string;
  lastUpdatedAt: string;
}

export interface SetupConsoleCatalogSummaryDto {
  totalEntities: number;
  totalCategories: number;
  missingCriticalTables: string[];
  categories: SetupConsoleCatalogCategoryDto[];
}

export interface SetupConsoleCatalogCategoryDto {
  category:
    | "system-foundation"
    | "identity-permission"
    | "workspace"
    | "business-domain"
    | "resource-runtime"
    | "audit-log";
  displayKey: string;
  entityCount: number;
  hasSeed: boolean;
}

// ============================================================================
// 二次认证
// ============================================================================

export interface ConsoleAuthChallengeRequest {
  recoveryKey?: string;
  bootstrapAdminUsername?: string;
  bootstrapAdminPassword?: string;
}

export interface ConsoleAuthTokenDto {
  consoleToken: string;
  expiresAt: string;
  issuedAt: string;
  permissions: ReadonlyArray<"system" | "workspace" | "migration">;
}

// ============================================================================
// 系统级初始化
// ============================================================================

export interface SetupStepResultDto {
  step: SetupConsoleStep;
  state: "running" | "succeeded" | "failed" | "skipped";
  message: string;
  systemState: SystemSetupState;
  startedAt: string | null;
  endedAt: string | null;
  payload?: Record<string, unknown>;
}

export interface SystemPrecheckRequest {
  expectedDbType?: string;
  expectedConnectionString?: string;
}

export interface SystemSchemaRequest {
  /** 仅做校验、不真正建表（默认 false）。 */
  dryRun?: boolean;
}

export interface SystemSeedRequest {
  bundleVersion?: string;
  /** 已存在时强制重跑（仅审计标记，不会重复插数据）。 */
  forceReapply?: boolean;
}

export interface SystemBootstrapUserRequest {
  username: string;
  password: string;
  tenantId: string;
  isPlatformAdmin: boolean;
  optionalRoleCodes: string[];
  generateRecoveryKey: boolean;
}

export interface SystemBootstrapUserResponse extends SetupStepResultDto {
  /** 仅在 generateRecoveryKey=true 时一次性返回明文恢复密钥，调用方必须立刻保存。 */
  recoveryKey: string | null;
}

export interface SystemDefaultWorkspaceRequest {
  workspaceName: string;
  ownerUsername: string;
  applyDefaultPublishChannels: boolean;
  applyDefaultModelStub: boolean;
}

// ============================================================================
// 工作空间级初始化
// ============================================================================

export interface WorkspaceInitRequest {
  workspaceName: string;
  seedBundleVersion: string;
  applyDefaultRoles: boolean;
  applyDefaultPublishChannels: boolean;
}

export interface WorkspaceSeedBundleRequest {
  bundleVersion: string;
  forceReapply?: boolean;
}

// ============================================================================
// 数据迁移
// ============================================================================

export interface DbConnectionConfig {
  driverCode: string;
  dbType: string;
  mode: "CurrentSystem" | "CurrentSystemAiDatabase" | "SavedDataSource" | "ConnectionString" | "VisualConfig";
  connectionString?: string;
  visualConfig?: Record<string, string> | null;
  displayName?: string | null;
  dataSourceId?: number | null;
  aiDatabaseId?: number | null;
}

export interface MigrationTestConnectionRequest {
  connection: DbConnectionConfig;
}

export interface MigrationTestConnectionResponse {
  connected: boolean;
  message: string;
  detectedDbType: string | null;
  detectedTableCount: number;
}

export interface DataMigrationJobCreateRequest {
  source: DbConnectionConfig;
  target: DbConnectionConfig;
  mode: DataMigrationMode;
  moduleScope: DataMigrationModuleScope;
  /** 同源/同目标已存在 Completed 任务时，必须显式标记 true 才允许新建。 */
  allowReExecute: boolean;
  selectedEntities?: string[] | null;
  selectedTables?: string[] | null;
  excludedEntities?: string[] | null;
  excludedTables?: string[] | null;
  batchSize?: number;
  writeMode?: "InsertOnly" | "TruncateThenInsert" | "Upsert";
  createSchema?: boolean;
  migrateSystemTables?: boolean;
  migrateFiles?: boolean;
  validateAfterCopy?: boolean;
}

export interface DataMigrationModuleScope {
  /** "all" 或显式六大类组合，与 SetupConsoleCatalogCategoryDto.category 对齐。 */
  categories: ReadonlyArray<"all" | SetupConsoleCatalogCategoryDto["category"]>;
  /** 可选实体名级精细过滤（不传 = 取 categories 全集）。 */
  entityNames?: string[];
}

export interface DataMigrationJobDto {
  id: string;
  state: string;
  mode: DataMigrationMode;
  source: DbConnectionConfig;
  target: DbConnectionConfig;
  sourceFingerprint: string;
  targetFingerprint: string;
  moduleScope: DataMigrationModuleScope;
  totalEntities: number;
  completedEntities: number;
  failedEntities: number;
  totalRows: number;
  copiedRows: number;
  progressPercent: number;
  currentEntityName: string | null;
  currentTableName: string | null;
  currentBatchNo: number | null;
  startedAt: string | null;
  finishedAt: string | null;
  errorSummary: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface DataMigrationPrecheckResultDto {
  job: DataMigrationJobDto;
  state: string;
  tableCount: number;
  totalRows: number;
  estimatedBatches: number;
  unsupportedTables: string[];
  targetNonEmptyTables: string[];
  missingTargetTables: string[];
  warnings: string[];
  tables: DataMigrationTableProgressDto[];
}

export interface DataMigrationProgressDto {
  jobId: string;
  state: string;
  totalEntities: number;
  completedEntities: number;
  failedEntities: number;
  totalRows: number;
  copiedRows: number;
  progressPercent: number;
  currentEntityName: string | null;
  currentTableName: string | null;
  currentBatchNo: number | null;
  startedAt: string | null;
  finishedAt: string | null;
  elapsedSeconds: number;
  updatedAt: string;
  tables: DataMigrationTableProgressDto[];
  recentLogs: DataMigrationLogItemDto[];
  recentBatches: DataMigrationBatchDto[];
}

export interface DataMigrationTableProgressDto {
  entityName: string;
  tableName: string;
  state: string;
  sourceRows: number;
  targetRowsBefore: number;
  targetRowsAfter: number;
  copiedRows: number;
  failedRows: number;
  batchSize: number;
  currentBatchNo: number;
  totalBatchCount: number;
  progressPercent: number;
  startedAt: string | null;
  finishedAt: string | null;
  errorMessage: string | null;
}

export interface DataMigrationBatchDto {
  batchNo: number;
  entityName: string;
  rowsCopied: number;
  state: string;
  startedAt: string | null;
  endedAt: string | null;
  checksum: string | null;
}

export interface DataMigrationReportDto {
  jobId: string;
  totalEntities: number;
  passedEntities: number;
  failedEntities: number;
  rowDiff: ReadonlyArray<{
    entityName: string;
    tableName: string;
    sourceRowCount: number;
    targetRowCount: number;
    diff: number;
    state: string;
    errorMessage?: string | null;
  }>;
  samplingDiff: ReadonlyArray<{
    entityName: string;
    tableName: string;
    sampledRows: number;
    mismatched: number;
    mismatchedExamples: string[];
  }>;
  overallPassed: boolean;
  generatedAt: string;
}

export interface DataMigrationLogItemDto {
  id: string;
  jobId: string;
  level: "info" | "warn" | "error";
  module: string;
  entityName: string | null;
  message: string;
  occurredAt: string;
}

export interface DataMigrationLogPagedResponse {
  items: DataMigrationLogItemDto[];
  total: number;
  pageIndex: number;
  pageSize: number;
}

export interface DataMigrationCutoverRequest {
  /** 切主后是否保留源库 7 天只读。 */
  keepSourceReadonlyForDays: number;
  confirmBackup: boolean;
  confirmRestartRequired: boolean;
}

// ============================================================================
// 真接口实现（M5 已落地后端：SetupConsoleAuthController + SetupConsoleController）
// ============================================================================

import { resolveApiUrl } from "./api-core";

const CONSOLE_TOKEN_HEADER = "X-Setup-Console-Token";
const SESSION_TOKEN_KEY = "atlas_setup_console_token";

function readSessionToken(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  try {
    return window.sessionStorage.getItem(SESSION_TOKEN_KEY);
  } catch {
    return null;
  }
}

async function fetchConsoleJson<T>(url: string, options?: RequestInit): Promise<ApiResponse<T>> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json"
  };
  const token = readSessionToken();
  if (token) {
    headers[CONSOLE_TOKEN_HEADER] = token;
  }

  const response = await fetch(resolveApiUrl(url), {
    ...options,
    headers: {
      ...headers,
      ...(options?.headers ?? {})
    }
  });
  return (await response.json()) as ApiResponse<T>;
}

export const setupConsoleApi = {
  getOverview: () =>
    fetchConsoleJson<SetupConsoleOverviewDto>("/api/v1/setup-console/overview"),
  getSystemState: () =>
    fetchConsoleJson<SystemSetupStateDto>("/api/v1/setup-console/system/state"),
  authenticate: (request: ConsoleAuthChallengeRequest) =>
    fetchConsoleJson<ConsoleAuthTokenDto>("/api/v1/setup-console/auth/recover", {
      method: "POST",
      body: JSON.stringify(request)
    }),
  refreshAuth: (consoleToken: string) =>
    fetchConsoleJson<ConsoleAuthTokenDto>("/api/v1/setup-console/auth/refresh", {
      method: "POST",
      body: JSON.stringify({ consoleToken })
    }),
  revokeAuth: (consoleToken: string) =>
    fetchConsoleJson<{ success: boolean }>("/api/v1/setup-console/auth/revoke", {
      method: "POST",
      body: JSON.stringify({ consoleToken })
    }),

  systemPrecheck: (request: SystemPrecheckRequest) =>
    fetchConsoleJson<SetupStepResultDto>("/api/v1/setup-console/system/precheck", {
      method: "POST",
      body: JSON.stringify(request)
    }),
  systemSchema: (request: SystemSchemaRequest) =>
    fetchConsoleJson<SetupStepResultDto>("/api/v1/setup-console/system/schema", {
      method: "POST",
      body: JSON.stringify(request)
    }),
  systemSeed: (request: SystemSeedRequest) =>
    fetchConsoleJson<SetupStepResultDto>("/api/v1/setup-console/system/seed", {
      method: "POST",
      body: JSON.stringify(request)
    }),
  systemBootstrapUser: (request: SystemBootstrapUserRequest) =>
    fetchConsoleJson<SystemBootstrapUserResponse>("/api/v1/setup-console/system/bootstrap-user", {
      method: "POST",
      body: JSON.stringify(request)
    }),
  systemDefaultWorkspace: (request: SystemDefaultWorkspaceRequest) =>
    fetchConsoleJson<SetupStepResultDto>("/api/v1/setup-console/system/default-workspace", {
      method: "POST",
      body: JSON.stringify(request)
    }),
  systemComplete: () =>
    fetchConsoleJson<SetupStepResultDto>("/api/v1/setup-console/system/complete", {
      method: "POST"
    }),
  systemRetry: (step: SetupConsoleStep) =>
    fetchConsoleJson<SetupStepResultDto>(`/api/v1/setup-console/system/retry/${encodeURIComponent(step)}`, {
      method: "POST"
    }),
  systemReopen: () =>
    fetchConsoleJson<SystemSetupStateDto>("/api/v1/setup-console/system/reopen", {
      method: "POST"
    }),

  listWorkspaces: () =>
    fetchConsoleJson<WorkspaceSetupStateDto[]>("/api/v1/setup-console/workspaces"),
  workspaceInit: (workspaceId: string, request: WorkspaceInitRequest) =>
    fetchConsoleJson<WorkspaceSetupStateDto>(`/api/v1/setup-console/workspaces/${encodeURIComponent(workspaceId)}/init`, {
      method: "POST",
      body: JSON.stringify(request)
    }),
  workspaceSeedBundle: (workspaceId: string, request: WorkspaceSeedBundleRequest) =>
    fetchConsoleJson<WorkspaceSetupStateDto>(`/api/v1/setup-console/workspaces/${encodeURIComponent(workspaceId)}/seed-bundle`, {
      method: "POST",
      body: JSON.stringify(request)
    }),
  workspaceComplete: (workspaceId: string) =>
    fetchConsoleJson<WorkspaceSetupStateDto>(`/api/v1/setup-console/workspaces/${encodeURIComponent(workspaceId)}/complete`, {
      method: "POST"
    }),

  migrationTestConnection: (request: MigrationTestConnectionRequest) =>
    fetchConsoleJson<MigrationTestConnectionResponse>("/api/v1/setup-console/migration/test-connection", {
      method: "POST",
      body: JSON.stringify(request)
    }),
  createMigrationJob: (request: DataMigrationJobCreateRequest) =>
    fetchConsoleJson<DataMigrationJobDto>("/api/v1/setup-console/migration/jobs", {
      method: "POST",
      body: JSON.stringify(request)
    }),
  getMigrationJob: (jobId: string) =>
    fetchConsoleJson<DataMigrationJobDto>(`/api/v1/setup-console/migration/jobs/${encodeURIComponent(jobId)}`),
  precheckMigrationJob: (jobId: string) =>
    fetchConsoleJson<DataMigrationPrecheckResultDto>(`/api/v1/setup-console/migration/jobs/${encodeURIComponent(jobId)}/precheck`, {
      method: "POST"
    }),
  startMigrationJob: (jobId: string) =>
    fetchConsoleJson<DataMigrationJobDto>(`/api/v1/setup-console/migration/jobs/${encodeURIComponent(jobId)}/start`, {
      method: "POST"
    }),
  cancelMigrationJob: (jobId: string) =>
    fetchConsoleJson<DataMigrationJobDto>(`/api/v1/setup-console/migration/jobs/${encodeURIComponent(jobId)}/cancel`, {
      method: "POST"
    }),
  getMigrationProgress: (jobId: string) =>
    fetchConsoleJson<DataMigrationProgressDto>(`/api/v1/setup-console/migration/jobs/${encodeURIComponent(jobId)}/progress`),
  validateMigrationJob: (jobId: string) =>
    fetchConsoleJson<DataMigrationReportDto>(`/api/v1/setup-console/migration/jobs/${encodeURIComponent(jobId)}/validate`, {
      method: "POST"
    }),
  cutoverMigrationJob: (jobId: string, request: DataMigrationCutoverRequest) =>
    fetchConsoleJson<DataMigrationJobDto>(`/api/v1/setup-console/migration/jobs/${encodeURIComponent(jobId)}/cutover`, {
      method: "POST",
      body: JSON.stringify(request)
    }),
  rollbackMigrationJob: (jobId: string) =>
    fetchConsoleJson<DataMigrationJobDto>(`/api/v1/setup-console/migration/jobs/${encodeURIComponent(jobId)}/rollback`, {
      method: "POST"
    }),
  retryMigrationJob: (jobId: string) =>
    fetchConsoleJson<DataMigrationJobDto>(`/api/v1/setup-console/migration/jobs/${encodeURIComponent(jobId)}/retry`, {
      method: "POST"
    }),
  getMigrationReport: (jobId: string) =>
    fetchConsoleJson<DataMigrationReportDto>(`/api/v1/setup-console/migration/jobs/${encodeURIComponent(jobId)}/report`),
  getMigrationLogs: (
    jobId: string,
    query?: { level?: "info" | "warn" | "error"; pageIndex?: number; pageSize?: number }
  ) => {
    const params = new URLSearchParams();
    if (query?.level) {
      params.set("level", query.level);
    }
    if (query?.pageIndex) {
      params.set("pageIndex", String(query.pageIndex));
    }
    if (query?.pageSize) {
      params.set("pageSize", String(query.pageSize));
    }
    const suffix = params.size > 0 ? `?${params.toString()}` : "";
    return fetchConsoleJson<DataMigrationLogPagedResponse>(
      `/api/v1/setup-console/migration/jobs/${encodeURIComponent(jobId)}/logs${suffix}`
    );
  },

  listEntityCatalog: (category?: SetupConsoleCatalogCategoryDto["category"]) => {
    const query = category ? `?category=${encodeURIComponent(category)}` : "";
    return fetchConsoleJson<SetupConsoleCatalogSummaryDto>(`/api/v1/setup-console/catalog/entities${query}`);
  }
} as const;
