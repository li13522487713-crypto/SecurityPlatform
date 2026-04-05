import { requestApi, toQuery, resolveAppHostPrefix } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import {
  getAccessToken,
  getAntiforgeryToken,
  getTenantId
} from "@atlas/shared-core";

export type SnowflakeId = string;

export interface ConversationDto {
  id: SnowflakeId;
  agentId: SnowflakeId;
  userId: SnowflakeId;
  title?: string;
  createdAt: string;
  lastMessageAt?: string;
  messageCount: number;
}

export interface ChatMessageDto {
  id: SnowflakeId;
  role: "system" | "user" | "assistant";
  content: string;
  metadata?: string;
  createdAt: string;
  isContextCleared: boolean;
}

export interface AgentChatResponse {
  conversationId: SnowflakeId;
  messageId: SnowflakeId;
  content: string;
  sources?: string;
}

export interface AgentChatRequest {
  conversationId?: SnowflakeId;
  message: string;
  enableRag?: boolean;
  attachments?: AgentChatAttachment[];
}

export interface AgentChatAttachment {
  type: string;
  url?: string;
  fileId?: string;
  mimeType?: string;
  name?: string;
  text?: string;
}

export type AgentStreamEventMode = "legacy" | "react";

function convBase(appKey: string): string {
  return `${resolveAppHostPrefix(appKey)}/api/v1/conversations`;
}

function agentBase(appKey: string): string {
  return `${resolveAppHostPrefix(appKey)}/api/v1/agents`;
}

export async function getConversationsPaged(
  appKey: string,
  request: PagedRequest,
  agentId?: SnowflakeId
): Promise<PagedResult<ConversationDto>> {
  const query = toQuery(request, { agentId: agentId || undefined });
  const response = await requestApi<ApiResponse<PagedResult<ConversationDto>>>(
    `${convBase(appKey)}?${query}`
  );
  if (!response.data) throw new Error(response.message || "查询会话列表失败");
  return response.data;
}

export async function createConversation(appKey: string, agentId: SnowflakeId, title?: string): Promise<string> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    convBase(appKey),
    {
      method: "POST",
      body: JSON.stringify({ agentId, title })
    }
  );
  if (!response.success || !response.data) throw new Error(response.message || "创建会话失败");
  return response.data.id;
}

export async function deleteConversation(appKey: string, id: SnowflakeId): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`${convBase(appKey)}/${id}`, {
    method: "DELETE"
  });
  if (!response.success) throw new Error(response.message || "删除会话失败");
}

export async function clearConversationContext(appKey: string, id: SnowflakeId): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `${convBase(appKey)}/${id}/clear-context`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "清除上下文失败");
}

export async function clearConversationHistory(appKey: string, id: SnowflakeId): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `${convBase(appKey)}/${id}/clear-history`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "清除历史失败");
}

export async function getMessages(
  appKey: string,
  conversationId: SnowflakeId,
  options?: { includeContextMarkers?: boolean; limit?: number }
): Promise<ChatMessageDto[]> {
  const params = new URLSearchParams();
  if (options?.includeContextMarkers !== undefined) {
    params.set("includeContextMarkers", String(options.includeContextMarkers));
  }
  if (options?.limit !== undefined) {
    params.set("limit", String(options.limit));
  }
  const query = params.toString();
  const response = await requestApi<ApiResponse<ChatMessageDto[]>>(
    `${convBase(appKey)}/${conversationId}/messages${query ? `?${query}` : ""}`
  );
  if (!response.data) throw new Error(response.message || "查询消息失败");
  return response.data;
}

export function createAgentChatStream(
  appKey: string,
  agentId: SnowflakeId,
  request: AgentChatRequest,
  mode: AgentStreamEventMode = "legacy"
): { fetchPromise: Promise<Response>; abortController: AbortController } {
  const abortController = new AbortController();

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    Accept: "text/event-stream",
    "Idempotency-Key": crypto.randomUUID()
  };

  const token = getAccessToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  const tenantId = getTenantId();
  if (tenantId) headers["X-Tenant-Id"] = tenantId;

  const afToken = getAntiforgeryToken();
  if (afToken) headers["X-CSRF-TOKEN"] = afToken;

  const base = agentBase(appKey);
  const streamUrl =
    mode === "react"
      ? `${base}/${agentId}/chat/stream?eventMode=react`
      : `${base}/${agentId}/chat/stream`;

  if (mode === "react") {
    headers["X-Stream-Event-Mode"] = "react";
  }

  const fetchPromise = fetch(streamUrl, {
    method: "POST",
    credentials: "include",
    headers,
    body: JSON.stringify(request),
    signal: abortController.signal
  });

  return { fetchPromise, abortController };
}
