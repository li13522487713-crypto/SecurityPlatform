import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@atlas/shared-core";

export interface AdminAiConfigDto {
  enableAiPlatform: boolean;
  enableOpenPlatform: boolean;
  enableCodeSandbox: boolean;
  enableMarketplace: boolean;
  enableContentModeration: boolean;
  maxDailyTokensPerUser: number;
  maxKnowledgeRetrievalCount: number;
}

export interface AdminAiConfigUpdateRequest extends AdminAiConfigDto {}

export async function getAdminAiConfig() {
  const response = await requestApi<ApiResponse<AdminAiConfigDto>>("/admin/ai-config");
  if (!response.data) {
    throw new Error(response.message || "加载 AI 管理配置失败");
  }

  return response.data;
}

export async function updateAdminAiConfig(request: AdminAiConfigUpdateRequest) {
  const response = await requestApi<ApiResponse<object>>("/admin/ai-config", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新 AI 管理配置失败");
  }
}
