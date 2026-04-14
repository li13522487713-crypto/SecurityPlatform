import type { ApiResponse, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi } from "./api-core";

export type WorkspaceIdeResourceType =
  | "agent"
  | "app"
  | "workflow"
  | "chatflow"
  | "plugin"
  | "knowledge-base"
  | "database";

export interface WorkspaceIdeSummaryDto {
  appCount: number;
  agentCount: number;
  workflowCount: number;
  chatflowCount: number;
  pluginCount: number;
  knowledgeBaseCount: number;
  databaseCount: number;
  favoriteCount: number;
  recentCount: number;
}

export interface WorkspaceIdeResourceCardDto {
  resourceType: WorkspaceIdeResourceType;
  resourceId: string;
  name: string;
  description?: string;
  icon?: string;
  status: string;
  publishStatus: string;
  updatedAt: string;
  isFavorite: boolean;
  lastOpenedAt?: string;
  lastEditedAt?: string;
  entryRoute: string;
  badge?: string;
  linkedWorkflowId?: string;
}

export interface WorkspaceIdeResourceQuery {
  keyword?: string;
  resourceType?: WorkspaceIdeResourceType;
  favoriteOnly?: boolean;
  pageIndex?: number;
  pageSize?: number;
}

export interface WorkspaceIdeCreateAppRequest {
  name: string;
  description?: string;
  icon?: string;
}

export interface WorkspaceIdeCreateAppResult {
  appId: string;
  workflowId: string;
  entryRoute: string;
}

export interface WorkspaceIdeActivityCreateRequest {
  resourceType: WorkspaceIdeResourceType;
  resourceId: number;
  resourceTitle: string;
  entryRoute: string;
}

export interface WorkspaceIdePendingPublishItemDto {
  resourceType: "agent" | "app" | "workflow" | "plugin";
  resourceId: string;
  resourceName: string;
  updatedAt: string;
}

export interface WorkspaceIdeDashboardStatsDto {
  agentCount: number;
  appCount: number;
  workflowCount: number;
  enabledModelCount: number;
  pluginCount: number;
  knowledgeBaseCount: number;
  pendingPublishItems: WorkspaceIdePendingPublishItemDto[];
  recentActivities: WorkspaceIdeResourceCardDto[];
}

export interface WorkspaceIdeResourceReferenceDto {
  referrerType: "agent" | "app" | "workflow";
  referrerId: string;
  referrerName: string;
  bindingField: string;
}

export interface WorkspaceIdePublishCenterItemDto {
  resourceType: "agent" | "app" | "workflow" | "plugin";
  resourceId: string;
  resourceName: string;
  currentVersion: number;
  draftVersion: number;
  lastPublishedAt?: string;
  status: "draft" | "published" | "outdated";
  apiEndpoint?: string;
  embedToken?: string;
}

export async function getWorkspaceIdeSummary(): Promise<WorkspaceIdeSummaryDto> {
  const response = await requestApi<ApiResponse<WorkspaceIdeSummaryDto>>("/workspace-ide/summary");
  if (!response.data) {
    throw new Error(response.message || "获取工作空间摘要失败");
  }
  return response.data;
}

export async function getWorkspaceIdeResources(query?: WorkspaceIdeResourceQuery): Promise<PagedResult<WorkspaceIdeResourceCardDto>> {
  const params = new URLSearchParams({
    pageIndex: String(query?.pageIndex ?? 1),
    pageSize: String(query?.pageSize ?? 24)
  });

  if (query?.keyword) {
    params.set("keyword", query.keyword);
  }

  if (query?.resourceType) {
    params.set("resourceType", query.resourceType);
  }

  if (query?.favoriteOnly) {
    params.set("favoriteOnly", "true");
  }

  const response = await requestApi<ApiResponse<PagedResult<WorkspaceIdeResourceCardDto>>>(`/workspace-ide/resources?${params.toString()}`);
  if (!response.data) {
    throw new Error(response.message || "获取工作空间资源失败");
  }
  return response.data;
}

export async function createWorkspaceIdeApp(request: WorkspaceIdeCreateAppRequest): Promise<WorkspaceIdeCreateAppResult> {
  const response = await requestApi<ApiResponse<WorkspaceIdeCreateAppResult>>("/workspace-ide/apps", {
    method: "POST",
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建应用失败");
  }
  return response.data;
}

export async function updateWorkspaceIdeFavorite(
  resourceType: WorkspaceIdeResourceType,
  resourceId: string,
  isFavorite: boolean
): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/workspace-ide/favorites/${encodeURIComponent(resourceType)}/${encodeURIComponent(resourceId)}`, {
    method: "PUT",
    body: JSON.stringify({ isFavorite })
  });
  if (!response.success) {
    throw new Error(response.message || "更新收藏状态失败");
  }
}

export async function recordWorkspaceIdeActivity(request: WorkspaceIdeActivityCreateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>("/workspace-ide/activities", {
    method: "POST",
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "记录最近编辑失败");
  }
}

export async function getWorkspaceDashboardStats(): Promise<WorkspaceIdeDashboardStatsDto> {
  const response = await requestApi<ApiResponse<WorkspaceIdeDashboardStatsDto>>("/workspace-ide/dashboard-stats");
  if (!response.data) {
    throw new Error(response.message || "获取工作台统计失败");
  }
  return response.data;
}

export async function getWorkspaceResourceReferences(resourceType: string, resourceId: string): Promise<WorkspaceIdeResourceReferenceDto[]> {
  const response = await requestApi<ApiResponse<WorkspaceIdeResourceReferenceDto[]>>(
    `/workspace-ide/resources/${encodeURIComponent(resourceType)}/${encodeURIComponent(resourceId)}/references`
  );
  if (!response.data) {
    throw new Error(response.message || "获取资源引用关系失败");
  }
  return response.data;
}

export async function getWorkspacePublishCenterItems(resourceType?: string): Promise<WorkspaceIdePublishCenterItemDto[]> {
  const params = new URLSearchParams();
  if (resourceType) {
    params.set("resourceType", resourceType);
  }

  const suffix = params.toString();
  const path = suffix ? `/workspace-ide/publish-center/items?${suffix}` : "/workspace-ide/publish-center/items";
  const response = await requestApi<ApiResponse<WorkspaceIdePublishCenterItemDto[]>>(path);
  if (!response.data) {
    throw new Error(response.message || "获取发布中心数据失败");
  }
  return response.data;
}
