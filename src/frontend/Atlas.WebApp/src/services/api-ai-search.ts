import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@/types/api";

export interface AiSearchResultItem {
  resourceType: string;
  resourceId: number;
  title: string;
  description?: string;
  path: string;
  updatedAt?: string;
}

export interface AiRecentEditItem {
  id: number;
  resourceType: string;
  resourceId: number;
  title: string;
  path: string;
  updatedAt: string;
}

export interface AiSearchResponse {
  items: AiSearchResultItem[];
  recentEdits: AiRecentEditItem[];
}

export interface AiRecentEditCreateRequest {
  resourceType: string;
  resourceId: number;
  title: string;
  path: string;
}

export async function searchAiGlobal(keyword?: string, limit = 20) {
  const query = new URLSearchParams();
  if (keyword) {
    query.set("keyword", keyword);
  }

  query.set("limit", String(limit));
  const response = await requestApi<ApiResponse<AiSearchResponse>>(`/ai-search?${query.toString()}`);
  if (!response.data) {
    throw new Error(response.message || "统一搜索失败");
  }

  return response.data;
}

export async function getAiRecentEdits(limit = 20) {
  const response = await requestApi<ApiResponse<AiRecentEditItem[]>>(`/ai-search/recent?limit=${limit}`);
  if (!response.data) {
    throw new Error(response.message || "加载最近编辑失败");
  }

  return response.data;
}

export async function recordAiRecentEdit(request: AiRecentEditCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/ai-search/recent", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "记录最近编辑失败");
  }

  return Number(response.data.id);
}

export async function deleteAiRecentEdit(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-search/recent/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除最近编辑失败");
  }
}
