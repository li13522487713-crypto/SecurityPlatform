import type { ApiResponse } from "@atlas/shared-react-core/types";
import type {
  SetupStepResultDto,
  SystemBootstrapUserRequest,
  SystemBootstrapUserResponse,
  SystemDefaultWorkspaceRequest,
  SystemPrecheckRequest,
  SystemSchemaRequest,
  SystemSeedRequest,
  SystemSetupStateDto
} from "../api-setup-console";
import type { SetupConsoleStep } from "../../app/setup-console-state-machine";
import { mockApiResponse, mockReject, MOCK_DELAY_MS } from "./mock-utils";
import {
  markRecoveryKeyConfigured,
  recordStep,
  setSystemState,
  snapshotSystemState,
  upsertWorkspace
} from "./setup-console-store";
import { MOCK_SETUP_CONSOLE_CONSTANTS } from "./api-setup-console.mock";

/**
 * 系统级初始化 6 步 mock。
 *
 * 防重复触发策略（与真接口一致）：
 * - 已完成的步骤再次调用直接返回 `succeeded` 但 `attemptCount` 不递增（mock 简化为 +1 不影响行为判定）。
 * - 已是 `completed` 的系统状态再调 `complete` 直接返回 succeeded，不会回退状态机。
 * - 任意 `failed` 状态可由 `retry` 显式跳回 running，再走一次。
 */

function ok<T>(data: T): Promise<ApiResponse<T>> {
  return mockApiResponse<T>(data);
}

export async function precheckSystem(
  request: SystemPrecheckRequest
): Promise<ApiResponse<SetupStepResultDto>> {
  const _ = request;
  const current = snapshotSystemState();
  if (current.state === "completed") {
    return ok<SetupStepResultDto>(buildResult("precheck", "succeeded", "system already completed"));
  }
  recordStep("precheck", "running");
  setSystemState("not_started");
  const record = recordStep("precheck", "succeeded");
  setSystemState("precheck_passed");
  return ok<SetupStepResultDto>({
    step: "precheck",
    state: "succeeded",
    message: "precheck passed",
    systemState: "precheck_passed",
    startedAt: record.startedAt,
    endedAt: record.endedAt,
    payload: { dryRun: false }
  });
}

export async function initializeSchema(
  request: SystemSchemaRequest
): Promise<ApiResponse<SetupStepResultDto>> {
  const current = snapshotSystemState();
  if (current.state === "completed") {
    return ok<SetupStepResultDto>(buildResult("schema", "succeeded", "system already completed"));
  }
  if (current.state !== "precheck_passed" && current.state !== "schema_initializing" && current.state !== "schema_initialized") {
    return mockReject(
      "VALIDATION_ERROR",
      `cannot initialize schema from state ${current.state}`,
      MOCK_DELAY_MS
    );
  }

  recordStep("schema", "running");
  setSystemState("schema_initializing");
  const record = recordStep("schema", "succeeded");
  setSystemState("schema_initialized");
  return ok<SetupStepResultDto>({
    step: "schema",
    state: "succeeded",
    message: request.dryRun ? "schema dry-run completed" : "schema initialized",
    systemState: "schema_initialized",
    startedAt: record.startedAt,
    endedAt: record.endedAt,
    payload: { tablesCreated: 290 }
  });
}

export async function seedSystem(
  request: SystemSeedRequest
): Promise<ApiResponse<SetupStepResultDto>> {
  const _ = request;
  const current = snapshotSystemState();
  if (current.state === "completed") {
    return ok<SetupStepResultDto>(buildResult("seed", "succeeded", "system already completed"));
  }
  if (current.state !== "schema_initialized" && current.state !== "seed_initializing" && current.state !== "seed_initialized") {
    return mockReject(
      "VALIDATION_ERROR",
      `cannot seed system from state ${current.state}`,
      MOCK_DELAY_MS
    );
  }

  recordStep("seed", "running");
  setSystemState("seed_initializing");
  const record = recordStep("seed", "succeeded");
  setSystemState("seed_initialized");
  return ok<SetupStepResultDto>({
    step: "seed",
    state: "succeeded",
    message: "seed bundle v1 applied",
    systemState: "seed_initialized",
    startedAt: record.startedAt,
    endedAt: record.endedAt,
    payload: {
      rolesCreated: 9,
      menuItemsCreated: 12,
      dictTypesCreated: 6
    }
  });
}

export async function bootstrapAdminUser(
  request: SystemBootstrapUserRequest
): Promise<ApiResponse<SystemBootstrapUserResponse>> {
  const current = snapshotSystemState();
  if (current.state === "completed") {
    return mockApiResponse<SystemBootstrapUserResponse>({
      ...buildResult("bootstrap-user", "succeeded", "system already completed"),
      recoveryKey: null
    });
  }
  if (current.state !== "seed_initialized") {
    return mockReject(
      "VALIDATION_ERROR",
      `cannot bootstrap admin from state ${current.state}`,
      MOCK_DELAY_MS
    );
  }

  if (!request.username || !request.password || !request.tenantId) {
    return mockReject("VALIDATION_ERROR", "username/password/tenantId are required", MOCK_DELAY_MS);
  }

  recordStep("bootstrap-user", "running");
  const record = recordStep("bootstrap-user", "succeeded");

  let recoveryKey: string | null = null;
  if (request.generateRecoveryKey) {
    recoveryKey = MOCK_SETUP_CONSOLE_CONSTANTS.recoveryKey;
    markRecoveryKeyConfigured();
  }

  return mockApiResponse<SystemBootstrapUserResponse>({
    step: "bootstrap-user",
    state: "succeeded",
    message: "bootstrap admin created",
    systemState: snapshotSystemState().state,
    startedAt: record.startedAt,
    endedAt: record.endedAt,
    payload: {
      adminUsername: request.username,
      effectiveRoles: ["SuperAdmin", "Admin", ...request.optionalRoleCodes]
    },
    recoveryKey
  });
}

export async function bootstrapDefaultWorkspace(
  request: SystemDefaultWorkspaceRequest
): Promise<ApiResponse<SetupStepResultDto>> {
  const current = snapshotSystemState();
  if (current.state === "completed") {
    return ok<SetupStepResultDto>(buildResult("default-workspace", "succeeded", "system already completed"));
  }
  if (current.state !== "seed_initialized") {
    return mockReject(
      "VALIDATION_ERROR",
      `cannot bootstrap default workspace from state ${current.state}`,
      MOCK_DELAY_MS
    );
  }
  if (!request.workspaceName || !request.ownerUsername) {
    return mockReject("VALIDATION_ERROR", "workspaceName/ownerUsername are required", MOCK_DELAY_MS);
  }

  recordStep("default-workspace", "running");
  const record = recordStep("default-workspace", "succeeded");

  upsertWorkspace({
    workspaceId: "default",
    workspaceName: request.workspaceName,
    state: "workspace_init_completed",
    seedBundleVersion: "v1",
    lastUpdatedAt: new Date().toISOString()
  });

  return ok<SetupStepResultDto>({
    step: "default-workspace",
    state: "succeeded",
    message: "default workspace created",
    systemState: snapshotSystemState().state,
    startedAt: record.startedAt,
    endedAt: record.endedAt,
    payload: {
      workspaceId: "default",
      workspaceName: request.workspaceName,
      defaultPublishChannelsApplied: request.applyDefaultPublishChannels,
      defaultModelStubApplied: request.applyDefaultModelStub
    }
  });
}

export async function completeSystemInit(): Promise<ApiResponse<SetupStepResultDto>> {
  const current = snapshotSystemState();
  if (current.state === "completed") {
    return ok<SetupStepResultDto>(buildResult("complete", "succeeded", "system already completed"));
  }
  if (current.state !== "seed_initialized") {
    return mockReject(
      "VALIDATION_ERROR",
      `cannot complete system from state ${current.state}`,
      MOCK_DELAY_MS
    );
  }

  recordStep("complete", "running");
  const record = recordStep("complete", "succeeded");
  setSystemState("completed");
  return ok<SetupStepResultDto>({
    step: "complete",
    state: "succeeded",
    message: "system initialization completed",
    systemState: "completed",
    startedAt: record.startedAt,
    endedAt: record.endedAt
  });
}

export async function retrySystemStep(step: SetupConsoleStep): Promise<ApiResponse<SetupStepResultDto>> {
  // 重置该步状态为 running，让前端再次调用对应步骤接口推进。
  recordStep(step, "running");
  return ok<SetupStepResultDto>({
    step,
    state: "running",
    message: `retry triggered for ${step}`,
    systemState: snapshotSystemState().state,
    startedAt: new Date().toISOString(),
    endedAt: null
  });
}

export async function reopenSystemConsole(): Promise<ApiResponse<SystemSetupStateDto>> {
  // 控制台重新打开：当系统已 dismissed 时回退到 not_started；其它状态保持不变（可重入）。
  const current = snapshotSystemState();
  if (current.state === "dismissed") {
    setSystemState("not_started");
  }
  return mockApiResponse<SystemSetupStateDto>(snapshotSystemState());
}

function buildResult(
  step: SetupConsoleStep,
  state: SetupStepResultDto["state"],
  message: string
): SetupStepResultDto {
  const now = new Date().toISOString();
  return {
    step,
    state,
    message,
    systemState: snapshotSystemState().state,
    startedAt: now,
    endedAt: now
  };
}
