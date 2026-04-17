import type { PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { matchKeyword, mockPaged, mockResolve } from "./mock-utils";

/**
 * Mock：工作空间-发布渠道（PRD 04-空间配置 4.6）。
 *
 * 路由：
 *   GET    /api/v1/workspaces/{workspaceId}/publish-channels
 *   POST   /api/v1/workspaces/{workspaceId}/publish-channels
 *   PATCH  /api/v1/workspaces/{workspaceId}/publish-channels/{channelId}
 *   POST   /api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/reauth
 *   DELETE /api/v1/workspaces/{workspaceId}/publish-channels/{channelId}
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

const CHANNELS: PublishChannelItem[] = [
  {
    id: "channel-web-sdk",
    workspaceId: "default",
    name: "Web SDK",
    type: "web-sdk",
    status: "active",
    authStatus: "authorized",
    description: "在外部站点嵌入聊天窗口",
    supportedTargets: ["agent", "app"],
    lastSyncAt: new Date().toISOString(),
    createdAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString()
  },
  {
    id: "channel-open-api",
    workspaceId: "default",
    name: "Open API",
    type: "open-api",
    status: "active",
    authStatus: "authorized",
    description: "通过 OpenAPI 调用智能体能力",
    supportedTargets: ["agent", "workflow"],
    lastSyncAt: new Date().toISOString(),
    createdAt: new Date(Date.now() - 14 * 24 * 60 * 60 * 1000).toISOString()
  }
];

export async function listPublishChannels(
  workspaceId: string,
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<PublishChannelItem>> {
  const items = CHANNELS.filter(item => item.workspaceId === workspaceId || item.workspaceId === "default")
    .filter(item => matchKeyword(item.name, request.keyword));
  return mockPaged(items, request);
}

export async function createPublishChannel(
  workspaceId: string,
  request: PublishChannelCreateRequest
): Promise<{ channelId: string }> {
  const id = `channel-${Date.now()}`;
  CHANNELS.push({
    id,
    workspaceId,
    name: request.name.trim(),
    type: request.type,
    status: "pending",
    authStatus: "unauthorized",
    description: request.description?.trim() ?? "",
    supportedTargets: request.supportedTargets,
    createdAt: new Date().toISOString()
  });
  return mockResolve({ channelId: id });
}

export async function updatePublishChannel(
  _workspaceId: string,
  channelId: string,
  request: PublishChannelUpdateRequest
): Promise<void> {
  const channel = CHANNELS.find(item => item.id === channelId);
  if (channel) {
    if (request.name) {
      channel.name = request.name.trim();
    }
    if (request.description !== undefined) {
      channel.description = request.description.trim();
    }
    if (request.status) {
      channel.status = request.status;
    }
    if (request.supportedTargets) {
      channel.supportedTargets = request.supportedTargets;
    }
  }
  return mockResolve(undefined);
}

export async function reauthPublishChannel(_workspaceId: string, channelId: string): Promise<void> {
  const channel = CHANNELS.find(item => item.id === channelId);
  if (channel) {
    channel.authStatus = "authorized";
    channel.lastSyncAt = new Date().toISOString();
  }
  return mockResolve(undefined);
}

export async function deletePublishChannel(_workspaceId: string, channelId: string): Promise<void> {
  const index = CHANNELS.findIndex(item => item.id === channelId);
  if (index >= 0) {
    CHANNELS.splice(index, 1);
  }
  return mockResolve(undefined);
}
