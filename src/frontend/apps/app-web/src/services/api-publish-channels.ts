import type { ApiResponse, PagedResult } from "@atlas/shared-react-core/types";
import type {
  PublishChannelActiveRelease,
  PublishChannelListItem
} from "@atlas/module-studio-react";
import { requestApi } from "./api-core";

/**
 * 治理 R1-F1：app-web 适配层 —— 把后端 WorkspacePublishChannelsController 的契约
 * 映射成 module-studio-react.PublishCenterPage 期望的类型。
 *
 * 端点：
 *  - GET /workspaces/{ws}/publish-channels?pageIndex=1&pageSize=200
 *  - GET /workspaces/{ws}/publish-channels/{channelId}/releases?status=active
 */

interface BackendPublishChannel {
  id: string;
  workspaceId: string;
  type: string;
  name: string;
  status: string;
  authStatus?: string;
  description?: string;
  supportedTargets?: string[];
  lastSyncAt?: string;
  createdAt: string;
}

interface BackendChannelRelease {
  id: string;
  channelId: string;
  releaseNo: number;
  status: string;
  publicMetadataJson?: string;
  releasedAt: string;
}

export async function listWorkspacePublishChannels(workspaceId: string): Promise<PublishChannelListItem[]> {
  const paged = await listWorkspacePublishChannelsPage(workspaceId, { pageIndex: 1, pageSize: 200 });
  const items = paged.items;
  return items.map((it) => ({
    id: it.id,
    workspaceId: it.workspaceId,
    type: it.type,
    name: it.name,
    status: it.status,
    authStatus: it.authStatus,
    lastSyncAt: it.lastSyncAt,
    createdAt: it.createdAt,
    updatedAt: it.createdAt
  }));
}

export interface WorkspacePublishChannelListItem {
  id: string;
  workspaceId: string;
  type: string;
  name: string;
  status: string;
  authStatus?: string;
  description?: string;
  supportedTargets: string[];
  lastSyncAt?: string;
  createdAt: string;
}

export async function listWorkspacePublishChannelsPage(
  workspaceId: string,
  input: {
    pageIndex: number;
    pageSize: number;
    keyword?: string;
  }
): Promise<PagedResult<WorkspacePublishChannelListItem>> {
  const params = new URLSearchParams({
    pageIndex: String(input.pageIndex),
    pageSize: String(input.pageSize)
  });
  if (input.keyword) {
    params.set("keyword", input.keyword);
  }
  const response = await requestApi<ApiResponse<PagedResult<BackendPublishChannel>>>(
    `/workspaces/${encodeURIComponent(workspaceId)}/publish-channels?${params.toString()}`
  );
  const paged = response.data;
  return {
    items: (paged?.items ?? []).map((it) => ({
      id: it.id,
      workspaceId: it.workspaceId,
      type: it.type,
      name: it.name,
      status: it.status,
      authStatus: it.authStatus,
      description: it.description,
      supportedTargets: it.supportedTargets ?? [],
      lastSyncAt: it.lastSyncAt,
      createdAt: it.createdAt
    })),
    total: paged?.total ?? 0,
    pageIndex: paged?.pageIndex ?? input.pageIndex,
    pageSize: paged?.pageSize ?? input.pageSize
  };
}

export async function reauthorizeWorkspacePublishChannel(workspaceId: string, channelId: string): Promise<void> {
  await requestApi<ApiResponse<{ success: boolean }>>(
    `/workspaces/${encodeURIComponent(workspaceId)}/publish-channels/${encodeURIComponent(channelId)}/reauth`,
    {
      method: "POST"
    }
  );
}

export async function deleteWorkspacePublishChannel(workspaceId: string, channelId: string): Promise<void> {
  await requestApi<ApiResponse<{ success: boolean }>>(
    `/workspaces/${encodeURIComponent(workspaceId)}/publish-channels/${encodeURIComponent(channelId)}`,
    {
      method: "DELETE"
    }
  );
}

export async function getWorkspaceChannelActiveRelease(
  workspaceId: string,
  channelId: string
): Promise<PublishChannelActiveRelease | null> {
  try {
    // 服务端返回 PagedResult；按 status=active 过滤后，取第一条
    const response = await requestApi<ApiResponse<PagedResult<BackendChannelRelease>>>(
      `/workspaces/${encodeURIComponent(workspaceId)}/publish-channels/${encodeURIComponent(channelId)}/releases?status=active&pageIndex=1&pageSize=1`
    );
    const item = response.data?.items?.[0];
    if (!item) return null;
    return {
      id: item.id,
      channelId: item.channelId,
      releaseNo: item.releaseNo,
      status: item.status,
      publicMetadataJson: item.publicMetadataJson,
      releasedAt: item.releasedAt
    };
  } catch {
    return null;
  }
}

/**
 * 治理 R1-F1：FeishuPublishTab / WechatMpPublishTab 共用的 fetcher。
 * 直接复用 requestApi（已带 Bearer + X-Tenant-Id 头部装配），并解包 ApiResponse.data。
 */
export async function publishChannelsHttpJson<T = unknown>(input: {
  url: string;
  method: string;
  body?: unknown;
}): Promise<T> {
  // input.url 是绝对路径（如 /api/v1/workspaces/{ws}/publish-channels/{cid}/feishu-credential）
  // requestApi 会通过 resolveRequestUrl 自动剥离 /api/v1 前缀
  const response = await requestApi<ApiResponse<T>>(input.url, {
    method: input.method,
    body: input.body !== undefined ? JSON.stringify(input.body) : undefined,
    headers: input.body !== undefined ? { "Content-Type": "application/json" } : undefined
  });
  if (response.data === undefined) {
    return undefined as T;
  }
  return response.data as T;
}
