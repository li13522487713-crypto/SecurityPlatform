import { describe, expect, it } from "vitest";
import {
  DATA_MIGRATION_STATES,
  SETUP_CONSOLE_STEPS,
  SYSTEM_SETUP_STATES,
  SYSTEM_TERMINAL_STATES,
  WORKSPACE_SETUP_STATES,
  WORKSPACE_TERMINAL_STATES,
  canTransitionMigration,
  canTransitionSystem,
  canTransitionWorkspace,
  isDataMigrationState,
  isMigrationBusy,
  isMigrationDone,
  isSystemBusy,
  isSystemInitDone,
  isSystemSetupState,
  isWorkspaceInitDone,
  isWorkspaceSetupState,
  shouldShowResumeBanner,
  type DataMigrationState,
  type SystemSetupState
} from "./setup-console-state-machine";

describe("setup-console state machine - constants", () => {
  it("exports the 14 system states required by the original PRD chapter 6", () => {
    expect(SYSTEM_SETUP_STATES).toHaveLength(14);
    expect(SYSTEM_SETUP_STATES).toContain("not_started");
    expect(SYSTEM_SETUP_STATES).toContain("completed");
    expect(SYSTEM_SETUP_STATES).toContain("failed");
    expect(SYSTEM_SETUP_STATES).toContain("dismissed");
  });

  it("exports the 4 workspace-level states", () => {
    expect(WORKSPACE_SETUP_STATES).toHaveLength(4);
    expect(WORKSPACE_SETUP_STATES).toContain("workspace_init_pending");
    expect(WORKSPACE_SETUP_STATES).toContain("workspace_init_completed");
  });

  it("exports the 6 console steps", () => {
    expect(SETUP_CONSOLE_STEPS).toHaveLength(6);
    expect(SETUP_CONSOLE_STEPS).toEqual([
      "precheck",
      "schema",
      "seed",
      "bootstrap-user",
      "default-workspace",
      "complete"
    ]);
  });

  it("exports the 9 data migration states matching backend AppMigrationTaskStatuses", () => {
    expect(DATA_MIGRATION_STATES).toHaveLength(9);
    expect(DATA_MIGRATION_STATES).toContain("pending");
    expect(DATA_MIGRATION_STATES).toContain("cutover-completed");
    expect(DATA_MIGRATION_STATES).toContain("rolled-back");
  });
});

describe("setup-console state machine - type guards", () => {
  it("isSystemSetupState narrows unknown to SystemSetupState", () => {
    expect(isSystemSetupState("not_started")).toBe(true);
    expect(isSystemSetupState("completed")).toBe(true);
    expect(isSystemSetupState("nope")).toBe(false);
    expect(isSystemSetupState(42)).toBe(false);
    expect(isSystemSetupState(null)).toBe(false);
  });

  it("isWorkspaceSetupState narrows unknown to WorkspaceSetupState", () => {
    expect(isWorkspaceSetupState("workspace_init_pending")).toBe(true);
    expect(isWorkspaceSetupState("workspace_init_completed")).toBe(true);
    expect(isWorkspaceSetupState("not_started")).toBe(false);
  });

  it("isDataMigrationState narrows unknown to DataMigrationState", () => {
    expect(isDataMigrationState("pending")).toBe(true);
    expect(isDataMigrationState("cutover-ready")).toBe(true);
    expect(isDataMigrationState("Cutover-Completed")).toBe(false);
  });
});

describe("setup-console state machine - system transitions", () => {
  it("allows idempotent same-state transition for every system state", () => {
    for (const state of SYSTEM_SETUP_STATES) {
      expect(canTransitionSystem(state, state)).toBe(true);
    }
  });

  it("allows the happy path: not_started -> precheck_passed -> schema_initializing -> ... -> completed", () => {
    const path: SystemSetupState[] = [
      "not_started",
      "precheck_passed",
      "schema_initializing",
      "schema_initialized",
      "seed_initializing",
      "seed_initialized",
      "completed"
    ];
    for (let index = 0; index < path.length - 1; index += 1) {
      expect(canTransitionSystem(path[index], path[index + 1])).toBe(true);
    }
  });

  it("rejects skipping precheck (not_started -> schema_initializing illegal)", () => {
    expect(canTransitionSystem("not_started", "schema_initializing")).toBe(false);
    expect(canTransitionSystem("not_started", "completed")).toBe(false);
  });

  it("rejects regressing from completed back to running states", () => {
    expect(canTransitionSystem("completed", "schema_initializing")).toBe(false);
    expect(canTransitionSystem("completed", "seed_initializing")).toBe(false);
    expect(canTransitionSystem("completed", "migration_running")).toBe(false);
  });

  it("only allows completed -> dismissed as termination escape", () => {
    expect(canTransitionSystem("completed", "dismissed")).toBe(true);
    expect(canTransitionSystem("completed", "failed")).toBe(false);
  });

  it("allows failed to retry by going back to a running step or to dismissed", () => {
    expect(canTransitionSystem("failed", "schema_initializing")).toBe(true);
    expect(canTransitionSystem("failed", "seed_initializing")).toBe(true);
    expect(canTransitionSystem("failed", "dismissed")).toBe(true);
    expect(canTransitionSystem("failed", "completed")).toBe(false);
  });

  it("allows dismissed to be re-opened back to not_started only", () => {
    expect(canTransitionSystem("dismissed", "not_started")).toBe(true);
    expect(canTransitionSystem("dismissed", "schema_initializing")).toBe(false);
    expect(canTransitionSystem("dismissed", "completed")).toBe(false);
  });

  it("allows partially_completed migration to resume", () => {
    expect(canTransitionSystem("migration_partially_completed", "migration_running")).toBe(true);
    expect(canTransitionSystem("migration_partially_completed", "validation_running")).toBe(true);
  });

  it("running phases must transition only to their natural successor or failed", () => {
    expect(canTransitionSystem("schema_initializing", "schema_initialized")).toBe(true);
    expect(canTransitionSystem("schema_initializing", "failed")).toBe(true);
    expect(canTransitionSystem("schema_initializing", "completed")).toBe(false);

    expect(canTransitionSystem("seed_initializing", "seed_initialized")).toBe(true);
    expect(canTransitionSystem("seed_initializing", "completed")).toBe(false);
  });
});

describe("setup-console state machine - workspace transitions", () => {
  it("allows pending -> running -> completed", () => {
    expect(canTransitionWorkspace("workspace_init_pending", "workspace_init_running")).toBe(true);
    expect(canTransitionWorkspace("workspace_init_running", "workspace_init_completed")).toBe(true);
  });

  it("allows failed to retry by going back to pending or running", () => {
    expect(canTransitionWorkspace("workspace_init_failed", "workspace_init_pending")).toBe(true);
    expect(canTransitionWorkspace("workspace_init_failed", "workspace_init_running")).toBe(true);
  });

  it("allows completed to be re-opened to pending (re-init scenario)", () => {
    expect(canTransitionWorkspace("workspace_init_completed", "workspace_init_pending")).toBe(true);
    expect(canTransitionWorkspace("workspace_init_completed", "workspace_init_failed")).toBe(false);
  });
});

describe("setup-console state machine - migration transitions", () => {
  it("allows the happy path", () => {
    const path: DataMigrationState[] = [
      "pending",
      "prechecking",
      "ready",
      "running",
      "validating",
      "cutover-ready",
      "cutover-completed"
    ];
    for (let index = 0; index < path.length - 1; index += 1) {
      expect(canTransitionMigration(path[index], path[index + 1])).toBe(true);
    }
  });

  it("blocks any transition out of cutover-completed (terminal)", () => {
    expect(canTransitionMigration("cutover-completed", "running")).toBe(false);
    expect(canTransitionMigration("cutover-completed", "rolled-back")).toBe(false);
    expect(canTransitionMigration("cutover-completed", "cutover-completed")).toBe(true);
  });

  it("allows running -> rolled-back as emergency stop", () => {
    expect(canTransitionMigration("running", "rolled-back")).toBe(true);
  });

  it("allows failed to retry from pending / ready / running entry points", () => {
    expect(canTransitionMigration("failed", "pending")).toBe(true);
    expect(canTransitionMigration("failed", "ready")).toBe(true);
    expect(canTransitionMigration("failed", "running")).toBe(true);
    expect(canTransitionMigration("failed", "cutover-completed")).toBe(false);
  });
});

describe("setup-console state machine - helpers", () => {
  it("isSystemInitDone returns true only for completed (not dismissed)", () => {
    expect(isSystemInitDone("completed")).toBe(true);
    expect(isSystemInitDone("dismissed")).toBe(false);
    expect(isSystemInitDone("failed")).toBe(false);
    expect(isSystemInitDone("not_started")).toBe(false);
  });

  it("isWorkspaceInitDone matches workspace_init_completed only", () => {
    expect(isWorkspaceInitDone("workspace_init_completed")).toBe(true);
    expect(isWorkspaceInitDone("workspace_init_pending")).toBe(false);
  });

  it("isSystemBusy detects all running phases", () => {
    expect(isSystemBusy("schema_initializing")).toBe(true);
    expect(isSystemBusy("seed_initializing")).toBe(true);
    expect(isSystemBusy("migration_running")).toBe(true);
    expect(isSystemBusy("validation_running")).toBe(true);
    expect(isSystemBusy("not_started")).toBe(false);
    expect(isSystemBusy("completed")).toBe(false);
  });

  it("isMigrationBusy / isMigrationDone are consistent with cutover-completed terminal", () => {
    expect(isMigrationBusy("running")).toBe(true);
    expect(isMigrationBusy("validating")).toBe(true);
    expect(isMigrationBusy("prechecking")).toBe(true);
    expect(isMigrationBusy("cutover-completed")).toBe(false);
    expect(isMigrationDone("cutover-completed")).toBe(true);
    expect(isMigrationDone("running")).toBe(false);
  });

  it("shouldShowResumeBanner triggers only for dismissed / failed / partially completed", () => {
    expect(shouldShowResumeBanner("dismissed")).toBe(true);
    expect(shouldShowResumeBanner("failed")).toBe(true);
    expect(shouldShowResumeBanner("migration_partially_completed")).toBe(true);
    expect(shouldShowResumeBanner("not_started")).toBe(false);
    expect(shouldShowResumeBanner("completed")).toBe(false);
    expect(shouldShowResumeBanner("schema_initializing")).toBe(false);
  });

  it("terminal state sets are consistent with the helpers above", () => {
    expect(SYSTEM_TERMINAL_STATES.has("completed")).toBe(true);
    expect(SYSTEM_TERMINAL_STATES.has("dismissed")).toBe(true);
    expect(SYSTEM_TERMINAL_STATES.has("failed")).toBe(false);
    expect(WORKSPACE_TERMINAL_STATES.has("workspace_init_completed")).toBe(true);
  });
});
