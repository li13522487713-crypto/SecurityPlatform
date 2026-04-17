import type { ApiResponse } from "@atlas/shared-react-core/types";
import type {
  ConsoleAuthChallengeRequest,
  ConsoleAuthTokenDto,
  SetupConsoleCatalogCategoryDto,
  SetupConsoleCatalogSummaryDto,
  SetupConsoleOverviewDto,
  SystemSetupStateDto
} from "../api-setup-console";
import { setupConsoleApi } from "../api-setup-console";
import { mockApiResponse, mockReject, MOCK_DELAY_MS } from "./mock-utils";
import { shouldUseRealConsoleApi } from "./mock-switch";
import { snapshotSystemState, snapshotWorkspaces, snapshotActiveMigration } from "./setup-console-store";

/**
 * 控制台总览 + 二次认证 mock。
 *
 * - 二次认证使用"恢复密钥"或"BootstrapAdmin 用户名/密码"双因子之一即可通过。
 * - 默认 mock 恢复密钥：`ATLS-MOCK-XXXX-XXXX-XXXX-XXXX`（dev 唯一）。
 * - BootstrapAdmin 默认凭证：与 `appsettings.Development.json` 一致（`admin / P@ssw0rd!`）。
 * - ConsoleToken 30 分钟过期，每次写操作前 UI 自动 `refreshAuth`。
 */

const MOCK_RECOVERY_KEY = "ATLS-MOCK-AAAA-BBBB-CCCC-DDDD";
const MOCK_BOOTSTRAP_USERNAME = "admin";
const MOCK_BOOTSTRAP_PASSWORD = "P@ssw0rd!";

const CONSOLE_TOKEN_TTL_SECONDS = 30 * 60;

function generateConsoleToken(): string {
  const random = Math.random().toString(36).slice(2, 10);
  return `mock-console-token-${random}`;
}

function buildCatalogSummary(): SetupConsoleCatalogSummaryDto {
  // 与后端 AtlasOrmSchemaCatalog.AllRuntimeEntityTypes 大致同量级，
  // M5 切真接口后由后端实时拼装。
  const categories: SetupConsoleCatalogCategoryDto[] = [
    {
      category: "system-foundation",
      displayKey: "setupConsoleCatalogCategorySystemFoundation",
      entityCount: 8,
      hasSeed: true
    },
    {
      category: "identity-permission",
      displayKey: "setupConsoleCatalogCategoryIdentityPermission",
      entityCount: 17,
      hasSeed: true
    },
    {
      category: "workspace",
      displayKey: "setupConsoleCatalogCategoryWorkspace",
      entityCount: 11,
      hasSeed: true
    },
    {
      category: "business-domain",
      displayKey: "setupConsoleCatalogCategoryBusinessDomain",
      entityCount: 153,
      hasSeed: false
    },
    {
      category: "resource-runtime",
      displayKey: "setupConsoleCatalogCategoryResourceRuntime",
      entityCount: 14,
      hasSeed: false
    },
    {
      category: "audit-log",
      displayKey: "setupConsoleCatalogCategoryAuditLog",
      entityCount: 8,
      hasSeed: false
    }
  ];

  return {
    totalEntities: categories.reduce((sum, item) => sum + item.entityCount, 0),
    totalCategories: categories.length,
    missingCriticalTables: [],
    categories
  };
}

export async function getSetupConsoleOverview(): Promise<ApiResponse<SetupConsoleOverviewDto>> {
  if (shouldUseRealConsoleApi()) {
    return setupConsoleApi.getOverview();
  }
  return mockApiResponse<SetupConsoleOverviewDto>({
    system: snapshotSystemState(),
    workspaces: snapshotWorkspaces(),
    activeMigration: snapshotActiveMigration(),
    catalogSummary: buildCatalogSummary()
  });
}

export async function authenticateSetupConsole(
  request: ConsoleAuthChallengeRequest
): Promise<ApiResponse<ConsoleAuthTokenDto>> {
  if (shouldUseRealConsoleApi()) {
    return setupConsoleApi.authenticate(request);
  }
  const recoveryOk = typeof request.recoveryKey === "string" && request.recoveryKey.trim() === MOCK_RECOVERY_KEY;
  const credentialsOk =
    typeof request.bootstrapAdminUsername === "string" &&
    typeof request.bootstrapAdminPassword === "string" &&
    request.bootstrapAdminUsername.trim() === MOCK_BOOTSTRAP_USERNAME &&
    request.bootstrapAdminPassword === MOCK_BOOTSTRAP_PASSWORD;

  if (!recoveryOk && !credentialsOk) {
    return mockReject("UNAUTHORIZED", "recovery key or bootstrap credentials invalid", MOCK_DELAY_MS);
  }

  const issuedAt = new Date();
  const expiresAt = new Date(issuedAt.getTime() + CONSOLE_TOKEN_TTL_SECONDS * 1000);
  return mockApiResponse<ConsoleAuthTokenDto>({
    consoleToken: generateConsoleToken(),
    issuedAt: issuedAt.toISOString(),
    expiresAt: expiresAt.toISOString(),
    permissions: ["system", "workspace", "migration"]
  });
}

export async function refreshSetupConsoleAuth(consoleToken: string): Promise<ApiResponse<ConsoleAuthTokenDto>> {
  if (shouldUseRealConsoleApi()) {
    return setupConsoleApi.refreshAuth(consoleToken);
  }
  if (!consoleToken || !consoleToken.startsWith("mock-console-token-")) {
    return mockReject("UNAUTHORIZED", "console token invalid or expired", MOCK_DELAY_MS);
  }
  const issuedAt = new Date();
  const expiresAt = new Date(issuedAt.getTime() + CONSOLE_TOKEN_TTL_SECONDS * 1000);
  return mockApiResponse<ConsoleAuthTokenDto>({
    consoleToken,
    issuedAt: issuedAt.toISOString(),
    expiresAt: expiresAt.toISOString(),
    permissions: ["system", "workspace", "migration"]
  });
}

export async function getSetupConsoleSystemState(): Promise<ApiResponse<SystemSetupStateDto>> {
  if (shouldUseRealConsoleApi()) {
    return setupConsoleApi.getSystemState();
  }
  return mockApiResponse<SystemSetupStateDto>(snapshotSystemState());
}

export async function getSetupConsoleCatalog(): Promise<ApiResponse<SetupConsoleCatalogSummaryDto>> {
  if (shouldUseRealConsoleApi()) {
    return setupConsoleApi.listEntityCatalog();
  }
  return mockApiResponse<SetupConsoleCatalogSummaryDto>(buildCatalogSummary());
}

/**
 * 实体目录下钻（M10/D9）：返回指定分类的全部实体名。
 * 真接口走 setupConsoleApi.listEntityCatalogDetails；mock 暂用预定义清单。
 */
export async function getSetupConsoleCatalogEntities(category: string): Promise<ApiResponse<readonly string[]>> {
  if (shouldUseRealConsoleApi()) {
    // 真接口在 SetupConsoleController 上为 GET /catalog/entities/{category}/details
    const response = await fetch(
      `/api/v1/setup-console/catalog/entities/${encodeURIComponent(category)}/details`,
      {
        headers: { "X-Setup-Console-Token": readSessionToken() ?? "" }
      }
    );
    return (await response.json()) as ApiResponse<readonly string[]>;
  }
  // mock：预设三类示例数据，满足 UI 下钻交互验证
  const samples: Record<string, string[]> = {
    "system-foundation": ["Tenant", "SystemConfig", "DictType", "DictData", "Notification", "FileRecord"],
    "identity-permission": ["UserAccount", "Role", "Permission", "Menu", "Department", "Position", "UserRole"],
    "workspace": ["Workspace", "WorkspaceRole", "WorkspaceMember", "WorkspaceFolder", "WorkspacePublishChannel"],
    "business-domain": ["Agent", "AiApp", "WorkflowMeta", "AiPlugin", "KnowledgeBase", "Conversation", "ChatMessage"],
    "resource-runtime": ["ModelConfig", "PluginConfig", "OutboxMessage", "InboxMessage", "Asset", "FileUploadSession"],
    "audit-log": ["AuditRecord", "LoginLog", "ApiCallLog", "LlmUsageRecord", "AlertRecord"]
  };
  return mockApiResponse<readonly string[]>(samples[category] ?? []);
}

function readSessionToken(): string | null {
  if (typeof window === "undefined") return null;
  try {
    return window.sessionStorage.getItem("atlas_setup_console_token");
  } catch {
    return null;
  }
}

export const MOCK_SETUP_CONSOLE_CONSTANTS = Object.freeze({
  recoveryKey: MOCK_RECOVERY_KEY,
  bootstrapUsername: MOCK_BOOTSTRAP_USERNAME,
  bootstrapPassword: MOCK_BOOTSTRAP_PASSWORD,
  consoleTokenTtlSeconds: CONSOLE_TOKEN_TTL_SECONDS
});
