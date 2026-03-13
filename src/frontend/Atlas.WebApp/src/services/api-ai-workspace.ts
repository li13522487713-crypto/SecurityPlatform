import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@/types/api";

export interface AiWorkspaceDto {
  id: number;
  name: string;
  theme: string;
  lastVisitedPath: string;
  favoriteResourceIds: number[];
  createdAt: string;
  updatedAt?: string;
}

export interface AiWorkspaceUpdateRequest {
  name: string;
  theme: string;
  lastVisitedPath: string;
  favoriteResourceIds: number[];
}

export interface AiLibraryItem {
  resourceType: string;
  resourceId: number;
  name: string;
  description?: string;
  updatedAt: string;
  path: string;
}

export interface AiLibraryPagedResult {
  items: AiLibraryItem[];
  total: number;
  pageIndex: number;
  pageSize: number;
}

export async function getCurrentAiWorkspace() {
  const response = await requestApi<ApiResponse<AiWorkspaceDto>>("/ai-workspaces/current");
  if (!response.data) {
    throw new Error(response.message || "加载工作台失败");
  }

  return response.data;
}

export async function updateCurrentAiWorkspace(request: AiWorkspaceUpdateRequest) {
  const response = await requestApi<ApiResponse<AiWorkspaceDto>>("/ai-workspaces/current", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "保存工作台失败");
  }

  return response.data;
}

export async function getAiWorkspaceLibrary(params: {
  keyword?: string;
  resourceType?: string;
  pageIndex: number;
  pageSize: number;
}) {
  const query = new URLSearchParams();
  if (params.keyword) {
    query.set("keyword", params.keyword);
  }

  if (params.resourceType) {
    query.set("resourceType", params.resourceType);
  }

  query.set("pageIndex", String(params.pageIndex));
  query.set("pageSize", String(params.pageSize));
  const response = await requestApi<ApiResponse<AiLibraryPagedResult>>(`/ai-workspaces/library?${query.toString()}`);
  if (!response.data) {
    throw new Error(response.message || "加载资源库失败");
  }

  return response.data;
}
