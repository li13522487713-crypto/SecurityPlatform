import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi, toQuery } from "./api-core";
import type { AiLibraryItem, ResourceType } from "@atlas/library-module-react";

type AiWorkspaceLibraryResourceType = Extract<ResourceType, "workflow" | "plugin" | "knowledge-base" | "database">;
type AiWorkspaceLibraryItem = AiLibraryItem & { resourceType: AiWorkspaceLibraryResourceType };

interface AiWorkspaceLibraryResult {
  items: AiLibraryItem[];
  total: number;
  pageIndex: number;
  pageSize: number;
}

export interface AiLibraryImportRequest {
  resourceType: AiWorkspaceLibraryResourceType;
  libraryItemId: number;
  targetAppId?: number;
  targetWorkspaceId?: number;
}

export interface AiLibraryMutationRequest {
  resourceType: AiWorkspaceLibraryResourceType;
  resourceId: number;
}

export interface AiLibraryMutationResult {
  resourceId: number;
  resourceType: AiWorkspaceLibraryResourceType;
  libraryItemId: number;
}

function isWorkspaceLibraryResourceType(resourceType: ResourceType): resourceType is AiWorkspaceLibraryResourceType {
  return resourceType === "workflow" || resourceType === "plugin" || resourceType === "knowledge-base" || resourceType === "database";
}

export async function getLibraryPaged(request: PagedRequest, resourceType?: AiWorkspaceLibraryResourceType) {
  const query = toQuery(request, { resourceType });
  const response = await requestApi<ApiResponse<AiWorkspaceLibraryResult>>(`/ai-workspaces/library?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询资源库失败");
  }

  const items = response.data.items.filter((item): item is AiWorkspaceLibraryItem => isWorkspaceLibraryResourceType(item.resourceType));

  return {
    items,
    total: response.data.total,
    pageIndex: response.data.pageIndex,
    pageSize: response.data.pageSize
  } satisfies PagedResult<AiWorkspaceLibraryItem>;
}

export async function importLibraryItem(request: AiLibraryImportRequest): Promise<AiLibraryMutationResult> {
  const response = await requestApi<ApiResponse<AiLibraryMutationResult>>("/ai-workspaces/library/imports", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "导入资源库文件失败");
  }

  return response.data;
}

export async function exportLibraryItem(request: AiLibraryMutationRequest): Promise<AiLibraryMutationResult> {
  const response = await requestApi<ApiResponse<AiLibraryMutationResult>>("/ai-workspaces/library/exports", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "复制到资源库失败");
  }

  return response.data;
}

export async function moveLibraryItem(request: AiLibraryMutationRequest): Promise<AiLibraryMutationResult> {
  const response = await requestApi<ApiResponse<AiLibraryMutationResult>>("/ai-workspaces/library/moves", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "移动到资源库失败");
  }

  return response.data;
}
