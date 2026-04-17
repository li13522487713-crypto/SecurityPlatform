import type {
  DataMigrationBatchDto,
  DataMigrationJobDto,
  DataMigrationLogItemDto,
  DataMigrationReportDto,
  SetupStepRecordDto,
  SystemSetupStateDto,
  WorkspaceSetupStateDto
} from "../api-setup-console";
import type { SetupConsoleStep, SystemSetupState } from "../../app/setup-console-state-machine";

/**
 * 控制台 mock 共享内存 store。
 *
 * - 仅给 4 个 mock 文件内部共享，禁止 UI 直接 import；UI 只能走 `services/mock` 出口。
 * - 进程内单例，刷新页面后清空（M5 切真接口后该文件可整体删除）。
 * - 所有变更通过 *State* / *Job* 等纯函数完成，确保单测可重置（`__resetForTests()`）。
 */

type SystemStoreState = {
  state: SystemSetupState;
  version: string;
  lastUpdatedAt: string;
  failureMessage: string | null;
  recoveryKeyConfigured: boolean;
  steps: SetupStepRecordDto[];
};

type WorkspaceStoreEntry = WorkspaceSetupStateDto;

type MigrationStoreEntry = {
  job: DataMigrationJobDto;
  batches: DataMigrationBatchDto[];
  logs: DataMigrationLogItemDto[];
  report: DataMigrationReportDto | null;
};

const initialSteps: SetupStepRecordDto[] = (
  ["precheck", "schema", "seed", "bootstrap-user", "default-workspace", "complete"] as SetupConsoleStep[]
).map((step) => ({
  step,
  state: "running",
  startedAt: null,
  endedAt: null,
  attemptCount: 0,
  errorMessage: null
}));

const systemStore: SystemStoreState = {
  state: "not_started",
  version: "v1",
  lastUpdatedAt: new Date().toISOString(),
  failureMessage: null,
  recoveryKeyConfigured: false,
  steps: cloneSteps(initialSteps)
};

function cloneSteps(steps: SetupStepRecordDto[]): SetupStepRecordDto[] {
  return steps.map((step) => ({ ...step }));
}

const workspaceStore = new Map<string, WorkspaceStoreEntry>([
  [
    "default",
    {
      workspaceId: "default",
      workspaceName: "Default workspace",
      state: "workspace_init_pending",
      seedBundleVersion: "v0",
      lastUpdatedAt: new Date().toISOString()
    }
  ]
]);

const migrationStore = new Map<string, MigrationStoreEntry>();
let activeMigrationId: string | null = null;

// ============================================================================
// 系统级 store API
// ============================================================================

export function snapshotSystemState(): SystemSetupStateDto {
  return {
    state: systemStore.state,
    version: systemStore.version,
    lastUpdatedAt: systemStore.lastUpdatedAt,
    failureMessage: systemStore.failureMessage,
    recoveryKeyConfigured: systemStore.recoveryKeyConfigured,
    steps: cloneSteps(systemStore.steps)
  };
}

export function setSystemState(state: SystemSetupState, failureMessage: string | null = null): void {
  systemStore.state = state;
  systemStore.failureMessage = failureMessage;
  systemStore.lastUpdatedAt = new Date().toISOString();
}

export function recordStep(
  step: SetupConsoleStep,
  state: SetupStepRecordDto["state"],
  options?: { errorMessage?: string | null; payload?: Record<string, unknown> }
): SetupStepRecordDto {
  const now = new Date().toISOString();
  const existing = systemStore.steps.find((item) => item.step === step);
  if (!existing) {
    const created: SetupStepRecordDto = {
      step,
      state,
      startedAt: now,
      endedAt: state === "running" ? null : now,
      attemptCount: 1,
      errorMessage: options?.errorMessage ?? null
    };
    systemStore.steps.push(created);
    systemStore.lastUpdatedAt = now;
    return created;
  }

  if (state === "running") {
    existing.startedAt = now;
    existing.endedAt = null;
    existing.errorMessage = null;
    existing.attemptCount += 1;
  } else {
    existing.endedAt = now;
    existing.errorMessage = options?.errorMessage ?? null;
  }
  existing.state = state;
  systemStore.lastUpdatedAt = now;
  return { ...existing };
}

export function markRecoveryKeyConfigured(): void {
  systemStore.recoveryKeyConfigured = true;
  systemStore.lastUpdatedAt = new Date().toISOString();
}

// ============================================================================
// 工作空间级 store API
// ============================================================================

export function snapshotWorkspaces(): WorkspaceSetupStateDto[] {
  return Array.from(workspaceStore.values()).map((item) => ({ ...item }));
}

export function getWorkspace(workspaceId: string): WorkspaceSetupStateDto | undefined {
  const found = workspaceStore.get(workspaceId);
  return found ? { ...found } : undefined;
}

export function upsertWorkspace(entry: WorkspaceSetupStateDto): WorkspaceSetupStateDto {
  workspaceStore.set(entry.workspaceId, { ...entry, lastUpdatedAt: new Date().toISOString() });
  return { ...workspaceStore.get(entry.workspaceId)! };
}

// ============================================================================
// 数据迁移 store API
// ============================================================================

export function snapshotActiveMigration(): DataMigrationJobDto | null {
  if (!activeMigrationId) {
    return null;
  }
  const entry = migrationStore.get(activeMigrationId);
  return entry ? { ...entry.job } : null;
}

export function setActiveMigration(jobId: string | null): void {
  activeMigrationId = jobId;
}

export function getMigration(jobId: string): MigrationStoreEntry | undefined {
  return migrationStore.get(jobId);
}

export function upsertMigration(entry: MigrationStoreEntry): MigrationStoreEntry {
  migrationStore.set(entry.job.id, entry);
  return entry;
}

export function listMigrationJobs(): DataMigrationJobDto[] {
  return Array.from(migrationStore.values()).map((item) => ({ ...item.job }));
}

export function appendMigrationLog(jobId: string, log: DataMigrationLogItemDto): void {
  const entry = migrationStore.get(jobId);
  if (entry) {
    entry.logs.push(log);
  }
}

export function appendMigrationBatch(jobId: string, batch: DataMigrationBatchDto): void {
  const entry = migrationStore.get(jobId);
  if (entry) {
    entry.batches.push(batch);
  }
}

export function setMigrationReport(jobId: string, report: DataMigrationReportDto): void {
  const entry = migrationStore.get(jobId);
  if (entry) {
    entry.report = report;
  }
}

// ============================================================================
// 单测重置工具
// ============================================================================

/** 仅供单测调用，把所有 store 复位到初始状态。 */
export function __resetSetupConsoleStoreForTests(): void {
  systemStore.state = "not_started";
  systemStore.version = "v1";
  systemStore.lastUpdatedAt = new Date().toISOString();
  systemStore.failureMessage = null;
  systemStore.recoveryKeyConfigured = false;
  systemStore.steps = cloneSteps(initialSteps);
  workspaceStore.clear();
  workspaceStore.set("default", {
    workspaceId: "default",
    workspaceName: "Default workspace",
    state: "workspace_init_pending",
    seedBundleVersion: "v0",
    lastUpdatedAt: new Date().toISOString()
  });
  migrationStore.clear();
  activeMigrationId = null;
}
