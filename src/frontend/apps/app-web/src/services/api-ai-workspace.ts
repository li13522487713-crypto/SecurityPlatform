import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi, toQuery } from "./api-core";
import type { AiLibraryItem, ResourceType } from "@atlas/library-module-react";

export type LibraryResourceType =
  | "workflow"
  | "plugin"
  | "knowledge-base"
  | "database"
  | "agent"
  | "app"
  | "prompt"
  | "card"
  | "voice"
  | "memory";

export type LibrarySource = "all" | "official" | "custom";

type AiWorkspaceLibraryImportType = Extract<ResourceType, "workflow" | "plugin" | "knowledge-base" | "database">;

export type AiWorkspaceLibraryItem = Omit<AiLibraryItem, "resourceType"> & {
  resourceType: LibraryResourceType;
  source?: LibrarySource;
  subType?: string | null;
  typeLabel?: string | null;
};

interface AiWorkspaceLibraryResult {
  items: AiWorkspaceLibraryItem[];
  total: number;
  pageIndex: number;
  pageSize: number;
}

export interface AiLibraryImportRequest {
  resourceType: AiWorkspaceLibraryImportType;
  libraryItemId: number;
  targetAppId?: number;
  targetWorkspaceId?: number;
}

export interface AiLibraryMutationRequest {
  resourceType: AiWorkspaceLibraryImportType;
  resourceId: number;
}

export interface AiLibraryMutationResult {
  resourceId: number;
  resourceType: AiWorkspaceLibraryImportType;
  libraryItemId: number;
}

export interface LibraryQueryOptions {
  resourceType?: LibraryResourceType;
  source?: LibrarySource;
  keyword?: string;
}

export async function getLibraryPaged(
  request: PagedRequest,
  options: LibraryQueryOptions = {}
): Promise<PagedResult<AiWorkspaceLibraryItem>> {
  const resourceType = options.resourceType && options.resourceType !== ("all" as LibraryResourceType)
    ? options.resourceType
    : undefined;
  const source = options.source && options.source !== "all" ? options.source : undefined;
  const keyword = options.keyword && options.keyword.trim().length > 0 ? options.keyword.trim() : undefined;

  const query = toQuery(request, { resourceType, source, keyword });
  const response = await requestApi<ApiResponse<AiWorkspaceLibraryResult>>(`/ai-workspaces/library?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询资源库失败");
  }

  return {
    items: response.data.items,
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
