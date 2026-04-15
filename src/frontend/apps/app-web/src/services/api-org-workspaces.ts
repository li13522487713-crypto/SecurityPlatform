import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi } from "./api-core";

export interface WorkspaceSummaryDto {
  id: string;
  orgId: string;
  name: string;
  description?: string;
  icon?: string;
  appInstanceId: string;
  appKey: string;
  roleCode: string;
  appCount: number;
  agentCount: number;
  workflowCount: number;
  createdAt: string;
  lastVisitedAt?: string;
}

export interface WorkspaceDetailDto {
  id: string;
  orgId: string;
  name: string;
  description?: string;
  icon?: string;
  appInstanceId: string;
  appKey: string;
  roleCode: string;
  allowedActions: string[];
  createdAt: string;
  lastVisitedAt?: string;
}

export interface WorkspaceAppCardDto {
  appId: string;
  name: string;
  description?: string;
  status: string;
  publishStatus: string;
  icon?: string;
  updatedAt: string;
  entryRoute: string;
  workflowId?: string;
}

export interface WorkspaceAppCreateRequest {
  name: string;
  description?: string;
  icon?: string;
}

export interface WorkspaceAppCreateResult {
  appId: string;
  workflowId: string;
  entryRoute: string;
}

export interface WorkspaceResourceCardDto {
  resourceType: string;
  resourceId: string;
  name: string;
  description?: string;
  status: string;
  publishStatus: string;
  updatedAt: string;
  entryRoute: string;
  badge?: string;
  linkedWorkflowId?: string;
}

export interface WorkspaceMemberDto {
  userId: string;
  username: string;
  displayName: string;
  roleId: string;
  roleCode: string;
  roleName: string;
  joinedAt: string;
}

export interface WorkspaceRolePermissionDto {
  roleId: string;
  roleCode: string;
  roleName: string;
  actions: string[];
}

function base(orgId: string): string {
  return `/organizations/${encodeURIComponent(orgId)}/workspaces`;
}

export async function getWorkspaces(orgId: string): Promise<WorkspaceSummaryDto[]> {
  const response = await requestApi<ApiResponse<WorkspaceSummaryDto[]>>(base(orgId));
  if (!response.data) {
    throw new Error(response.message || "获取工作空间列表失败");
  }
  return response.data;
}

export async function getWorkspaceById(orgId: string, workspaceId: string): Promise<WorkspaceDetailDto> {
  const response = await requestApi<ApiResponse<WorkspaceDetailDto>>(`${base(orgId)}/${encodeURIComponent(workspaceId)}`);
  if (!response.data) {
    throw new Error(response.message || "获取工作空间详情失败");
  }
  return response.data;
}

export async function getWorkspaceByAppKey(orgId: string, appKey: string): Promise<WorkspaceDetailDto | null> {
  const response = await requestApi<ApiResponse<WorkspaceDetailDto>>(`${base(orgId)}/by-app-key/${encodeURIComponent(appKey)}`, undefined, {
    suppressErrorMessage: true
  });
  return response.data ?? null;
}

export async function getWorkspaceDevelopApps(
  orgId: string,
  workspaceId: string,
  params: PagedRequest
): Promise<PagedResult<WorkspaceAppCardDto>> {
  const query = new URLSearchParams({
    pageIndex: String(params.pageIndex ?? 1),
    pageSize: String(params.pageSize ?? 24)
  });
  if (params.keyword) {
    query.set("keyword", params.keyword);
  }

  const response = await requestApi<ApiResponse<PagedResult<WorkspaceAppCardDto>>>(
    `${base(orgId)}/${encodeURIComponent(workspaceId)}/develop/apps?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取工作空间应用失败");
  }
  return response.data;
}

export async function createWorkspaceDevelopApp(
  orgId: string,
  workspaceId: string,
  request: WorkspaceAppCreateRequest
): Promise<WorkspaceAppCreateResult> {
  const response = await requestApi<ApiResponse<WorkspaceAppCreateResult>>(
    `${base(orgId)}/${encodeURIComponent(workspaceId)}/develop/apps`,
    {
      method: "POST",
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "创建工作空间应用失败");
  }
  return response.data;
}

export async function getWorkspaceResources(
  orgId: string,
  workspaceId: string,
  resourceType?: string,
  params?: PagedRequest
): Promise<PagedResult<WorkspaceResourceCardDto>> {
  const query = new URLSearchParams({
    pageIndex: String(params?.pageIndex ?? 1),
    pageSize: String(params?.pageSize ?? 24)
  });
  if (params?.keyword) {
    query.set("keyword", params.keyword);
  }
  if (resourceType) {
    query.set("resourceType", resourceType);
  }

  const response = await requestApi<ApiResponse<PagedResult<WorkspaceResourceCardDto>>>(
    `${base(orgId)}/${encodeURIComponent(workspaceId)}/resources?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取工作空间资源失败");
  }
  return response.data;
}
