import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@atlas/shared-core";

export interface AgentDetail {
  id: string;
  name: string;
  description?: string;
  avatarUrl?: string;
  status: string;
}

export async function getAgentById(id: string): Promise<AgentDetail> {
  const response = await requestApi<ApiResponse<AgentDetail>>(`/agents/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询 Agent 失败");
  }
  return response.data;
}
