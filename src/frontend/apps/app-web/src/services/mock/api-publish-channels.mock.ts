import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi, toQuery } from "../api-core";

/**
 * 工作空间-发布渠道（PRD 04-4.6）。已切换为真实 REST：
 *   Atlas.AppHost/Controllers/WorkspacePublishChannelsController.cs
 *   Atlas.Infrastructure/Services/Coze/WorkspacePublishChannelService.cs
 *   Atlas.Domain/AiPlatform/Entities/WorkspacePublishChannel.cs
 */

export type PublishChannelType = "web-sdk" | "open-api" | "wechat" | "feishu" | "lark" | "custom";
export type PublishChannelStatus = "active" | "inactive" | "pending";
export type PublishChannelAuthStatus = "authorized" | "expired" | "unauthorized";

export interface PublishChannelItem {
  id: string;
  workspaceId: string;
  name: string;
  type: PublishChannelType;
  status: PublishChannelStatus;
  authStatus: PublishChannelAuthStatus;
  description?: string;
  supportedTargets: Array<"agent" | "app" | "workflow">;
  lastSyncAt?: string;
  createdAt: string;
}

export interface PublishChannelCreateRequest {
  name: string;
  type: PublishChannelType;
  description?: string;
  supportedTargets: Array<"agent" | "app" | "workflow">;
}

export interface PublishChannelUpdateRequest {
  name?: string;
  description?: string;
  status?: PublishChannelStatus;
  supportedTargets?: Array<"agent" | "app" | "workflow">;
}

function channelsBase(workspaceId: string): string {
  return `/workspaces/${encodeURIComponent(workspaceId)}/publish-channels`;
}

export async function listPublishChannels(
  workspaceId: string,
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<PublishChannelItem>> {
  const query = toQuery(
    {
      pageIndex: request.pageIndex ?? 1,
      pageSize: request.pageSize ?? 50
    },
    { keyword: request.keyword }
  );
  const response = await requestApi<ApiResponse<PagedResult<PublishChannelItem>>>(
    `${channelsBase(workspaceId)}?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "Failed to load publish channels");
  }
  return response.data;
}

export async function createPublishChannel(
  workspaceId: string,
  request: PublishChannelCreateRequest
): Promise<{ channelId: string }> {
  const response = await requestApi<ApiResponse<{ id?: string; channelId?: string }>>(
    channelsBase(workspaceId),
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  const channelId = response.data?.channelId ?? response.data?.id ?? "";
  if (!channelId) {
    throw new Error(response.message || "Failed to create channel");
  }
  return { channelId };
}

export async function updatePublishChannel(
  workspaceId: string,
  channelId: string,
  request: PublishChannelUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>(
    `${channelsBase(workspaceId)}/${encodeURIComponent(channelId)}`,
    {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "Failed to update channel");
  }
}

export async function reauthPublishChannel(workspaceId: string, channelId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>(
    `${channelsBase(workspaceId)}/${encodeURIComponent(channelId)}/reauth`,
    {
      method: "POST"
    }
  );
  if (!response.success) {
    throw new Error(response.message || "Failed to reauthorize channel");
  }
}

export async function deletePublishChannel(workspaceId: string, channelId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>(
    `${channelsBase(workspaceId)}/${encodeURIComponent(channelId)}`,
    {
      method: "DELETE"
    }
  );
  if (!response.success) {
    throw new Error(response.message || "Failed to delete channel");
  }
}
