import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@/types/api";

export interface AgentPublicationItem {
  id: string;
  agentId: string;
  version: number;
  isActive: boolean;
  embedToken: string;
  embedTokenExpiresAt: string;
  releaseNote?: string;
  publishedByUserId: string;
  createdAt: string;
  updatedAt?: string;
  revokedAt?: string;
}

export interface AgentPublicationResult {
  publicationId: string;
  agentId: string;
  version: number;
  embedToken: string;
  embedTokenExpiresAt: string;
}

export interface AgentEmbedTokenResult {
  publicationId: string;
  agentId: string;
  version: number;
  embedToken: string;
  embedTokenExpiresAt: string;
}

export async function getAgentPublications(agentId: string) {
  const response = await requestApi<ApiResponse<AgentPublicationItem[]>>(
    `/agent-publications/agents/${agentId}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询发布版本失败");
  }
  return response.data;
}

export async function publishAgentPublication(agentId: string, releaseNote?: string) {
  const response = await requestApi<ApiResponse<AgentPublicationResult>>(
    `/agent-publications/agents/${agentId}/publish`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ releaseNote: releaseNote || undefined })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "发布 Agent 失败");
  }
  return response.data;
}

export async function rollbackAgentPublication(
  agentId: string,
  targetVersion: number,
  releaseNote?: string
) {
  const response = await requestApi<ApiResponse<AgentPublicationResult>>(
    `/agent-publications/agents/${agentId}/rollback`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        targetVersion,
        releaseNote: releaseNote || undefined
      })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "回滚 Agent 失败");
  }
  return response.data;
}

export async function regenerateAgentEmbedToken(agentId: string) {
  const response = await requestApi<ApiResponse<AgentEmbedTokenResult>>(
    `/agent-publications/agents/${agentId}/embed-token`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({})
    }
  );
  if (!response.data) {
    throw new Error(response.message || "更新 Embed Token 失败");
  }
  return response.data;
}
