import type { PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { matchKeyword, mockPaged, mockResolve } from "./mock-utils";

/**
 * Mock：项目开发-文件夹（PRD 03-项目开发）。
 *
 * 路由：
 *   GET    /api/v1/workspaces/{workspaceId}/folders
 *   POST   /api/v1/workspaces/{workspaceId}/folders
 *   PATCH  /api/v1/workspaces/{workspaceId}/folders/{folderId}
 *   DELETE /api/v1/workspaces/{workspaceId}/folders/{folderId}
 *   POST   /api/v1/workspaces/{workspaceId}/folders/{folderId}/items
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

const FOLDERS: FolderListItem[] = [
  {
    id: "folder-default",
    workspaceId: "default",
    name: "示例文件夹",
    description: "用于演示项目分组",
    itemCount: 0,
    createdByDisplayName: "RootUser",
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString()
  }
];

export async function listFolders(
  workspaceId: string,
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<FolderListItem>> {
  const items = FOLDERS.filter(item => item.workspaceId === workspaceId || item.workspaceId === "default")
    .filter(item => matchKeyword(item.name, request.keyword));
  return mockPaged(items, request);
}

export async function createFolder(workspaceId: string, request: FolderCreateRequest): Promise<{ folderId: string }> {
  const id = `folder-${Date.now()}`;
  FOLDERS.push({
    id,
    workspaceId,
    name: request.name.trim(),
    description: request.description?.trim() ?? "",
    itemCount: 0,
    createdByDisplayName: "Me",
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString()
  });
  return mockResolve({ folderId: id });
}

export async function updateFolder(
  _workspaceId: string,
  folderId: string,
  request: FolderUpdateRequest
): Promise<void> {
  const folder = FOLDERS.find(item => item.id === folderId);
  if (folder) {
    if (request.name) {
      folder.name = request.name.trim();
    }
    if (request.description !== undefined) {
      folder.description = request.description.trim();
    }
    folder.updatedAt = new Date().toISOString();
  }
  return mockResolve(undefined);
}

export async function deleteFolder(_workspaceId: string, folderId: string): Promise<void> {
  const index = FOLDERS.findIndex(item => item.id === folderId);
  if (index >= 0) {
    FOLDERS.splice(index, 1);
  }
  return mockResolve(undefined);
}

export async function moveItemToFolder(
  _workspaceId: string,
  folderId: string,
  _request: FolderItemMoveRequest
): Promise<void> {
  const folder = FOLDERS.find(item => item.id === folderId);
  if (folder) {
    folder.itemCount += 1;
    folder.updatedAt = new Date().toISOString();
  }
  return mockResolve(undefined);
}
