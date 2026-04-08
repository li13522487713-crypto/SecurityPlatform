import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@atlas/shared-core";
import type { PagedResult } from "@atlas/shared-core";

export interface AgentDetail {
  id: string;
  name: string;
  description?: string;
  avatarUrl?: string;
  status: string;
}

export interface AgentListItem {
  id: string;
  name: string;
  description?: string;
  avatarUrl?: string;
  status: string;
  modelName?: string;
}

export async function getAgentsPaged(params?: {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  status?: string;
}): Promise<PagedResult<AgentListItem>> {
  const pageIndex = params?.pageIndex ?? 1;
  const pageSize = params?.pageSize ?? 20;
  const query = new URLSearchParams({
    pageIndex: String(pageIndex),
    pageSize: String(pageSize),
  });

  if (params?.keyword) {
    query.set("keyword", params.keyword);
  }

  if (params?.status) {
    query.set("status", params.status);
  }

  const response = await requestApi<ApiResponse<PagedResult<AgentListItem>>>(`/agents?${query.toString()}`);
  if (!response.data) {
    throw new Error(response.message || "Failed to query agents");
  }

  return response.data;
}

export async function getAgentById(id: string): Promise<AgentDetail> {
  const response = await requestApi<ApiResponse<AgentDetail>>(`/agents/${id}`);
  if (!response.data) {
    throw new Error(response.message || "Failed to query agent");
  }
  return response.data;
}
