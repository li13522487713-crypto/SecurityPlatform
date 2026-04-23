import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi, toQuery } from "./api-core";

/**
 * 项目开发-文件夹真实服务。
 *
 * 2026-04 起项目开发页直接走 AppHost 的
 * `/api/v1/workspaces/{workspaceId}/folders*` 路由，不再通过 mock 命名文件兜底。
 */

export interface FolderListItem {
  id: string;
  workspaceId: string;
  name: string;
  description?: string;
  itemCount: number;
  createdByDisplayName: string;
  createdAt: string;
  updatedAt: string;
}

export interface FolderCreateRequest {
  name: string;
  description?: string;
}

export interface FolderUpdateRequest {
  name?: string;
  description?: string;
}

export interface FolderItemMoveRequest {
  itemType: "agent" | "app" | "project";
  itemId: string;
}

function foldersBase(workspaceId: string): string {
  return `/workspaces/${encodeURIComponent(workspaceId)}/folders`;
}

export async function listFolders(
  workspaceId: string,
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<FolderListItem>> {
  const query = toQuery(
    {
      pageIndex: request.pageIndex ?? 1,
      pageSize: request.pageSize ?? 20
    },
    { keyword: request.keyword }
  );
  const response = await requestApi<ApiResponse<PagedResult<FolderListItem>>>(
    `${foldersBase(workspaceId)}?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "Failed to load folders");
  }
  return response.data;
}

export async function createFolder(
  workspaceId: string,
  request: FolderCreateRequest
): Promise<{ folderId: string }> {
  const response = await requestApi<ApiResponse<{ id?: string; folderId?: string }>>(
    foldersBase(workspaceId),
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  const folderId = response.data?.folderId ?? response.data?.id ?? "";
  if (!folderId) {
    throw new Error(response.message || "Failed to create folder");
  }
  return { folderId };
}

export async function updateFolder(
  workspaceId: string,
  folderId: string,
  request: FolderUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>(
    `${foldersBase(workspaceId)}/${encodeURIComponent(folderId)}`,
    {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "Failed to update folder");
  }
}

export async function deleteFolder(workspaceId: string, folderId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>(
    `${foldersBase(workspaceId)}/${encodeURIComponent(folderId)}`,
    {
      method: "DELETE"
    }
  );
  if (!response.success) {
    throw new Error(response.message || "Failed to delete folder");
  }
}

export async function moveItemToFolder(
  workspaceId: string,
  folderId: string,
  request: FolderItemMoveRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>(
    `${foldersBase(workspaceId)}/${encodeURIComponent(folderId)}/items`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "Failed to move item");
  }
}
