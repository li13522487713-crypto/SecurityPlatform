import { requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";

export interface LongTermMemoryListItem {
  id: number;
  agentId: number;
  conversationId: number;
  memoryKey: string;
  content: string;
  source: string;
  hitCount: number;
  lastReferencedAt: string;
  createdAt: string;
  updatedAt: string;
}

export async function getLongTermMemoriesPaged(
  request: PagedRequest,
  agentId?: number,
  keyword?: string
) {
  const query = toQuery(request, {
    agentId: agentId && agentId > 0 ? String(agentId) : undefined,
    keyword: keyword && keyword.trim() ? keyword.trim() : undefined
  });
  const response = await requestApi<ApiResponse<PagedResult<LongTermMemoryListItem>>>(
    `/memories/long-term?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询长期记忆失败");
  }

  return response.data;
}

export async function deleteLongTermMemory(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/memories/long-term/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除长期记忆失败");
  }
}

export async function clearLongTermMemories(agentId?: number) {
  const query = agentId && agentId > 0 ? `?agentId=${agentId}` : "";
  const response = await requestApi<ApiResponse<{ cleared: number }>>(`/memories/long-term${query}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "清空长期记忆失败");
  }

  return response.data;
}
