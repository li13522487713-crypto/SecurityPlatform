import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@/types/api";

export interface AgentPublicationItem {
  id: number;
  agentId: number;
  version: number;
  isActive: boolean;
  embedToken: string;
  embedTokenExpiresAt: string;
  releaseNote?: string;
  publishedByUserId: number;
  createdAt: string;
  updatedAt?: string;
  revokedAt?: string;
}

export interface AgentPublicationResult {
  publicationId: number;
  agentId: number;
  version: number;
  embedToken: string;
  embedTokenExpiresAt: string;
}

export interface AgentEmbedTokenResult {
  publicationId: number;
  agentId: number;
  version: number;
  embedToken: string;
  embedTokenExpiresAt: string;
}

export async function getAgentPublications(agentId: number) {
  const response = await requestApi<ApiResponse<AgentPublicationItem[]>>(
    `/agent-publications/agents/${agentId}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询发布版本失败");
  }
  return response.data;
}

export async function publishAgentPublication(agentId: number, releaseNote?: string) {
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
  agentId: number,
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

export async function regenerateAgentEmbedToken(agentId: number) {
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
