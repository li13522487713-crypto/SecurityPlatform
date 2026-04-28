/**
 * 系统初始化与迁移控制台 - 状态机定义（前端真理来源）。
 *
 * 后端镜像：`src/backend/Atlas.Domain/Setup/SetupConsoleStateMachine.cs`（M5 落地）。
 *
 * 设计原则：
 * - 同时覆盖系统级（System）与工作空间级（Workspace）两个状态机。
 * - 显式列出所有合法转移，禁止隐式跳转；非法转移必须由调用方走 `retry` / `reopen` / `dismiss` 三个明确动作。
 * - 状态机是纯函数，不依赖任何 React 上下文或 IO；可被 mock 与真实 service 共享。
 * - 与原始需求第六章 16 状态完全对齐（系统 14 + 工作空间 2）。
 */

export const SYSTEM_SETUP_STATES = [
  "not_started",
  "precheck_passed",
  "schema_initializing",
  "schema_initialized",
  "seed_initializing",
  "seed_initialized",
  "migration_pending",
  "migration_running",
  "migration_partially_completed",
  "migration_completed",
  "validation_running",
  "completed",
  "failed",
  "dismissed"
] as const;

export type SystemSetupState = typeof SYSTEM_SETUP_STATES[number];

export const WORKSPACE_SETUP_STATES = [
  "workspace_init_pending",
  "workspace_init_running",
  "workspace_init_completed",
  "workspace_init_failed"
] as const;

export type WorkspaceSetupState = typeof WORKSPACE_SETUP_STATES[number];

export const SETUP_CONSOLE_STEPS = [
  "precheck",
  "schema",
  "seed",
  "bootstrap-user",
  "default-workspace",
  "complete"
] as const;

export type SetupConsoleStep = typeof SETUP_CONSOLE_STEPS[number];

export const DATA_MIGRATION_MODES = [
  "structure-only",
  "structure-plus-data",
  "validate-only",
  "incremental-delta",
  "re-execute"
] as const;

export type DataMigrationMode = typeof DATA_MIGRATION_MODES[number];

export const DATA_MIGRATION_STATES = [
  "created",
  "pending",
  "prechecking",
  "ready",
  "queued",
  "running",
  "cancelling",
  "cancelled",
  "succeeded",
  "validating",
  "validation_failed",
  "validated",
  "cutover-ready",
  "cutover-completed",
  "cutover-failed",
  "failed",
  "rolled-back"
] as const;

export type DataMigrationState = typeof DATA_MIGRATION_STATES[number];

/**
 * 系统级合法状态转移表。
 *
 * 表项语义：`from -> to` 表示该状态可由用户行为或服务端事件直接进入。
 * 非法转移必须显式走 `retry()` / `dismiss()` / `reopen()` / `forceFail()` 等动作。
 */
const SYSTEM_LEGAL_TRANSITIONS: Readonly<Record<SystemSetupState, readonly SystemSetupState[]>> = {
  not_started: ["precheck_passed", "failed", "dismissed"],
  precheck_passed: ["schema_initializing", "failed", "dismissed"],
  schema_initializing: ["schema_initialized", "failed"],
  schema_initialized: ["seed_initializing", "failed"],
  seed_initializing: ["seed_initialized", "failed"],
  seed_initialized: ["migration_pending", "validation_running", "completed", "failed"],
  migration_pending: ["migration_running", "failed", "dismissed"],
  migration_running: ["migration_partially_completed", "migration_completed", "failed"],
  migration_partially_completed: ["migration_running", "validation_running", "failed", "dismissed"],
  migration_completed: ["validation_running", "completed", "failed"],
  validation_running: ["completed", "failed"],
  // 终态：除 dismissed/failed 显式 reopen 外，不能向其它任何状态转移。
  completed: ["dismissed"],
  failed: ["not_started", "precheck_passed", "schema_initializing", "seed_initializing", "dismissed"],
  dismissed: ["not_started"]
};

const WORKSPACE_LEGAL_TRANSITIONS: Readonly<Record<WorkspaceSetupState, readonly WorkspaceSetupState[]>> = {
  workspace_init_pending: ["workspace_init_running", "workspace_init_failed"],
  workspace_init_running: ["workspace_init_completed", "workspace_init_failed"],
  workspace_init_completed: ["workspace_init_pending"],
  workspace_init_failed: ["workspace_init_pending", "workspace_init_running"]
};

/** 终态判定（终态后只能通过显式 `reopen()` 才能离开）。 */
export const SYSTEM_TERMINAL_STATES: ReadonlySet<SystemSetupState> = new Set(["completed", "dismissed"]);
export const WORKSPACE_TERMINAL_STATES: ReadonlySet<WorkspaceSetupState> = new Set(["workspace_init_completed"]);

export function isSystemSetupState(value: unknown): value is SystemSetupState {
  return typeof value === "string" && (SYSTEM_SETUP_STATES as readonly string[]).includes(value);
}

export function isWorkspaceSetupState(value: unknown): value is WorkspaceSetupState {
  return typeof value === "string" && (WORKSPACE_SETUP_STATES as readonly string[]).includes(value);
}

export function isDataMigrationState(value: unknown): value is DataMigrationState {
  return typeof value === "string" && (DATA_MIGRATION_STATES as readonly string[]).includes(value);
}

/**
 * 校验系统级状态转移是否合法。
 * 合法情况：from === to（幂等保持），或 to ∈ legal[from]。
 */
export function canTransitionSystem(from: SystemSetupState, to: SystemSetupState): boolean {
  if (from === to) {
    return true;
  }
  return SYSTEM_LEGAL_TRANSITIONS[from].includes(to);
}

export function canTransitionWorkspace(from: WorkspaceSetupState, to: WorkspaceSetupState): boolean {
  if (from === to) {
    return true;
  }
  return WORKSPACE_LEGAL_TRANSITIONS[from].includes(to);
}

/**
 * 步骤映射到状态机的"开始 / 结束"两个状态对，便于 UI 在执行时直接置位。
 *
 * 使用方式：
 *   const [running, succeeded] = SYSTEM_STEP_TRANSITIONS["schema"];
 *   await transit(running);
 *   try { await runStep(); await transit(succeeded); } catch { await transit("failed"); }
 */
export const SYSTEM_STEP_TRANSITIONS: Readonly<Record<SetupConsoleStep, readonly [SystemSetupState, SystemSetupState]>> = {
  precheck: ["not_started", "precheck_passed"],
  schema: ["schema_initializing", "schema_initialized"],
  seed: ["seed_initializing", "seed_initialized"],
  "bootstrap-user": ["seed_initialized", "seed_initialized"],
  "default-workspace": ["seed_initialized", "seed_initialized"],
  complete: ["seed_initialized", "completed"]
};

/**
 * 系统级初始化是否已完成（终态 `completed`）。
 *
 * `dismissed` 不算完成，仅表示用户主动忽略；下次进入控制台仍需重新走流程。
 */
export function isSystemInitDone(state: SystemSetupState): boolean {
  return state === "completed";
}

/**
 * 工作空间级初始化是否已完成。
 */
export function isWorkspaceInitDone(state: WorkspaceSetupState): boolean {
  return state === "workspace_init_completed";
}

/**
 * 当前状态是否处于"任意进行中"阶段（用于 UI 显示进度环 / 禁用按钮）。
 */
export function isSystemBusy(state: SystemSetupState): boolean {
  return (
    state === "schema_initializing" ||
    state === "seed_initializing" ||
    state === "migration_running" ||
    state === "validation_running"
  );
}

export function isMigrationBusy(state: DataMigrationState): boolean {
  return (
    state === "created" ||
    state === "prechecking" ||
    state === "queued" ||
    state === "running" ||
    state === "cancelling" ||
    state === "validating"
  );
}

export function isMigrationDone(state: DataMigrationState): boolean {
  return (
    state === "cancelled" ||
    state === "succeeded" ||
    state === "validation_failed" ||
    state === "validated" ||
    state === "cutover-ready" ||
    state === "cutover-completed" ||
    state === "cutover-failed" ||
    state === "failed" ||
    state === "rolled-back"
  );
}

/**
 * 数据迁移合法状态转移表。
 * 与后端 `AppMigrationTaskStatuses` 的 9 状态保持一致（仅命名风格转 kebab-case 适配前端）。
 */
const MIGRATION_LEGAL_TRANSITIONS: Readonly<Record<DataMigrationState, readonly DataMigrationState[]>> = {
  created: ["pending", "prechecking", "ready", "queued", "cancelled", "failed"],
  pending: ["prechecking", "failed"],
  prechecking: ["ready", "failed", "cancelled"],
  ready: ["queued", "running", "failed", "cancelled"],
  queued: ["running", "cancelling", "cancelled", "failed"],
  running: ["succeeded", "validating", "cancelling", "cancelled", "failed", "rolled-back"],
  cancelling: ["cancelled", "failed"],
  cancelled: ["queued", "running"],
  succeeded: ["validating", "validated", "failed"],
  validating: ["validated", "validation_failed", "failed"],
  validation_failed: ["validating", "failed"],
  validated: ["cutover-ready", "cutover-completed", "cutover-failed", "failed"],
  "cutover-ready": ["cutover-completed", "failed", "rolled-back"],
  "cutover-failed": ["cutover-ready", "cutover-completed", "failed"],
  // 终态
  "cutover-completed": [],
  failed: ["pending", "ready", "running"],
  "rolled-back": ["pending"]
};

export function canTransitionMigration(from: DataMigrationState, to: DataMigrationState): boolean {
  if (from === to) {
    return true;
  }
  return MIGRATION_LEGAL_TRANSITIONS[from].includes(to);
}

/**
 * 控制台是否需要展示"恢复初始化"卡片（dismissed / failed / 部分完成场景）。
 */
export function shouldShowResumeBanner(state: SystemSetupState): boolean {
  return state === "dismissed" || state === "failed" || state === "migration_partially_completed";
}
