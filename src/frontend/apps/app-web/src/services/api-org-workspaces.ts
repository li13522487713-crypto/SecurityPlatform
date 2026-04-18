import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { extractResourceId, requestApi } from "./api-core";

export interface WorkspaceSummaryDto {
  id: string;
  orgId: string;
  name: string;
  description?: string;
  icon?: string;
  /** 1→N 模型：默认/主应用实例 ID。若工作空间尚未绑定任何应用实例，API 层会规范化为空字符串。 */
  appInstanceId: string;
  /** 1→N 模型：默认/主应用 Key。若工作空间尚未绑定任何应用实例，API 层会规范化为空字符串。 */
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
  /** 1→N 模型：默认/主应用实例 ID。若工作空间尚未绑定任何应用实例，API 层会规范化为空字符串。 */
  appInstanceId: string;
  /** 1→N 模型：默认/主应用 Key。若工作空间尚未绑定任何应用实例，API 层会规范化为空字符串。 */
  appKey: string;
  roleCode: string;
  allowedActions: string[];
  createdAt: string;
  lastVisitedAt?: string;
}

export interface WorkspaceCreateRequest {
  name: string;
  description?: string;
  icon?: string;
}

export interface WorkspaceUpdateRequest {
  name: string;
  description?: string;
  icon?: string;
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

export interface WorkspaceMemberCreateRequest {
  userId: string;
  roleCode: string;
}

export interface WorkspaceMemberRoleUpdateRequest {
  roleCode: string;
}

export interface WorkspaceRolePermissionUpdateItem {
  roleCode: string;
  actions: string[];
}

export interface WorkspaceResourcePermissionUpdateRequest {
  items: WorkspaceRolePermissionUpdateItem[];
}

/**
 * 1→N 模型：在已有工作空间内创建一个 AppManifest（应用实例）。
 * AppKey 留空时由后端自动生成。
 */
export interface WorkspaceAppInstanceCreateRequest {
  name: string;
  description?: string;
  icon?: string;
  category?: string;
  appKey?: string;
}

export interface WorkspaceAppInstanceDto {
  appInstanceId: string;
  appKey: string;
  name: string;
  description?: string;
  icon?: string;
  category?: string;
  status: string;
  version: number;
  createdAt: string;
  updatedAt: string;
}

function base(orgId: string): string {
  return `/organizations/${encodeURIComponent(orgId)}/workspaces`;
}

/**
 * 1→N 模型适配层：后端 Workspace.AppInstanceId/AppKey 现在是可空字段（未绑定主应用时为 null）。
 * 老的前端代码大量使用 `workspace.appKey` 做字符串拼接和回退，因此在边界处统一规范化为空串。
 */
function normalizeWorkspaceSummary(item: WorkspaceSummaryDto): WorkspaceSummaryDto {
  return {
    ...item,
    appInstanceId: item.appInstanceId ?? "",
    appKey: item.appKey ?? ""
  };
}

function normalizeWorkspaceDetail(item: WorkspaceDetailDto): WorkspaceDetailDto {
  return {
    ...item,
    appInstanceId: item.appInstanceId ?? "",
    appKey: item.appKey ?? ""
  };
}

export async function getWorkspaces(orgId: string): Promise<WorkspaceSummaryDto[]> {
  const response = await requestApi<ApiResponse<WorkspaceSummaryDto[]>>(base(orgId));
  if (!response.data) {
    throw new Error(response.message || "获取工作空间列表失败");
  }
  return response.data.map(normalizeWorkspaceSummary);
}

export async function getWorkspaceById(orgId: string, workspaceId: string): Promise<WorkspaceDetailDto> {
  const response = await requestApi<ApiResponse<WorkspaceDetailDto>>(`${base(orgId)}/${encodeURIComponent(workspaceId)}`);
  if (!response.data) {
    throw new Error(response.message || "获取工作空间详情失败");
  }
  return normalizeWorkspaceDetail(response.data);
}

export async function getWorkspaceByIdOrNull(orgId: string, workspaceId: string): Promise<WorkspaceDetailDto | null> {
  const response = await requestApi<ApiResponse<WorkspaceDetailDto>>(
    `${base(orgId)}/${encodeURIComponent(workspaceId)}`,
    undefined,
    {
      suppressErrorMessage: true
    }
  );
  return response.data ? normalizeWorkspaceDetail(response.data) : null;
}

export async function getWorkspaceByAppKey(orgId: string, appKey: string): Promise<WorkspaceDetailDto | null> {
  const response = await requestApi<ApiResponse<WorkspaceDetailDto>>(`${base(orgId)}/by-app-key/${encodeURIComponent(appKey)}`, undefined, {
    suppressErrorMessage: true
  });
  return response.data ? normalizeWorkspaceDetail(response.data) : null;
}

export async function createWorkspace(orgId: string, request: WorkspaceCreateRequest): Promise<string> {
  const response = await requestApi<ApiResponse<{ id?: string | number | null; Id?: string | number | null }>>(
    base(orgId),
    {
      method: "POST",
      body: JSON.stringify(request)
    }
  );
  const workspaceId = extractResourceId(response.data);
  if (!workspaceId) {
    throw new Error(response.message || "创建工作空间失败");
  }
  return workspaceId;
}

export async function updateWorkspace(
  orgId: string,
  workspaceId: string,
  request: WorkspaceUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `${base(orgId)}/${encodeURIComponent(workspaceId)}`,
    {
      method: "PUT",
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新工作空间失败");
  }
}

export async function deleteWorkspace(orgId: string, workspaceId: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `${base(orgId)}/${encodeURIComponent(workspaceId)}`,
    {
      method: "DELETE"
    }
  );
  if (!response.success) {
    throw new Error(response.message || "归档工作空间失败");
  }
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

export async function getWorkspaceMembers(orgId: string, workspaceId: string): Promise<WorkspaceMemberDto[]> {
  const response = await requestApi<ApiResponse<WorkspaceMemberDto[]>>(
    `${base(orgId)}/${encodeURIComponent(workspaceId)}/members`
  );
  if (!response.data) {
    throw new Error(response.message || "获取工作空间成员失败");
  }
  return response.data;
}

export async function addWorkspaceMember(
  orgId: string,
  workspaceId: string,
  request: WorkspaceMemberCreateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `${base(orgId)}/${encodeURIComponent(workspaceId)}/members`,
    {
      method: "POST",
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "添加工作空间成员失败");
  }
}

export async function updateWorkspaceMemberRole(
  orgId: string,
  workspaceId: string,
  userId: string,
  request: WorkspaceMemberRoleUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `${base(orgId)}/${encodeURIComponent(workspaceId)}/members/${encodeURIComponent(userId)}`,
    {
      method: "PUT",
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新工作空间成员角色失败");
  }
}

export async function removeWorkspaceMember(orgId: string, workspaceId: string, userId: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `${base(orgId)}/${encodeURIComponent(workspaceId)}/members/${encodeURIComponent(userId)}`,
    {
      method: "DELETE"
    }
  );
  if (!response.success) {
    throw new Error(response.message || "移除工作空间成员失败");
  }
}

export async function getWorkspaceResourcePermissions(
  orgId: string,
  workspaceId: string,
  resourceType: string,
  resourceId: string
): Promise<WorkspaceRolePermissionDto[]> {
  const response = await requestApi<ApiResponse<WorkspaceRolePermissionDto[]>>(
    `${base(orgId)}/${encodeURIComponent(workspaceId)}/resources/${encodeURIComponent(resourceType)}/${encodeURIComponent(resourceId)}/permissions`
  );
  if (!response.data) {
    throw new Error(response.message || "获取资源权限失败");
  }
  return response.data;
}

/**
 * 1→N 模型：在工作空间内创建一个新的应用实例（AppManifest）。
 * 后端会自动把 AppManifest.WorkspaceId 设为该 workspace；若 workspace 还没有默认主应用，会回填为本应用。
 */
export async function createWorkspaceAppInstance(
  orgId: string,
  workspaceId: string,
  request: WorkspaceAppInstanceCreateRequest
): Promise<WorkspaceAppInstanceDto> {
  const response = await requestApi<ApiResponse<WorkspaceAppInstanceDto>>(
    `${base(orgId)}/${encodeURIComponent(workspaceId)}/app-instances`,
    {
      method: "POST",
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "创建应用实例失败");
  }
  return response.data;
}

export async function updateWorkspaceResourcePermissions(
  orgId: string,
  workspaceId: string,
  resourceType: string,
  resourceId: string,
  request: WorkspaceResourcePermissionUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `${base(orgId)}/${encodeURIComponent(workspaceId)}/resources/${encodeURIComponent(resourceType)}/${encodeURIComponent(resourceId)}/permissions`,
    {
      method: "PUT",
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新资源权限失败");
  }
}
