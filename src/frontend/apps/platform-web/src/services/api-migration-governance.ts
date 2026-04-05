import type { ApiResponse } from "@atlas/shared-core";
import { requestApi } from "@/services/api-core";
import type { MigrationGovernanceOverview } from "@/types/platform-console";

const MIGRATION_GOVERNANCE_BASE = "/api/v2/migration-governance";

export async function getMigrationGovernanceOverview(): Promise<MigrationGovernanceOverview> {
  const response = await requestApi<ApiResponse<MigrationGovernanceOverview>>(
    `${MIGRATION_GOVERNANCE_BASE}/overview`
  );
  if (!response.data) {
    throw new Error(response.message || "查询迁移治理指标失败");
  }
  return response.data;
}
