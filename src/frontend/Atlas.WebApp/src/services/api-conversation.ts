import { requestApi, toQuery, API_BASE } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import { getAccessToken, getTenantId, getAntiforgeryToken } from "@/utils/auth";

export interface ConversationDto {
  id: number;
  agentId: number;
  userId: number;
  title?: string;
  createdAt: string;
  lastMessageAt?: string;
  messageCount: number;
}

export interface ChatMessageDto {
  id: number;
  role: "system" | "user" | "assistant";
  content: string;
  metadata?: string;
  createdAt: string;
  isContextCleared: boolean;
}

export interface AgentChatResponse {
  conversationId: number;
  messageId: number;
  content: string;
  sources?: string;
}

export interface AgentChatRequest {
  conversationId?: number;
  message: string;
  enableRag?: boolean;
}

export type AgentStreamEventMode = "legacy" | "react";

export async function getConversationsPaged(
  request: PagedRequest,
  agentId?: number
) {
  const query = toQuery(request, { agentId: agentId?.toString() });
  const response = await requestApi<ApiResponse<PagedResult<ConversationDto>>>(
    `/conversations?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询会话列表失败");
  }
  return response.data;
}

export async function getConversationById(id: number) {
  const response = await requestApi<ApiResponse<ConversationDto>>(
    `/conversations/${id}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询会话失败");
  }
  return response.data;
}

export async function createConversation(agentId: number, title?: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    "/conversations",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ agentId, title })
    }
  );
  if (!response.success || !response.data) {
    throw new Error(response.message || "创建会话失败");
  }
  return Number(response.data.id);
}

export async function updateConversation(id: number, title: string) {
  const response = await requestApi<ApiResponse<object>>(`/conversations/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ title })
  });
  if (!response.success) {
    throw new Error(response.message || "更新会话失败");
  }
}

export async function deleteConversation(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/conversations/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除会话失败");
  }
}

export async function clearConversationContext(id: number) {
  const response = await requestApi<ApiResponse<object>>(
    `/conversations/${id}/clear-context`,
    { method: "POST" }
  );
  if (!response.success) {
    throw new Error(response.message || "清除上下文失败");
  }
}

export async function clearConversationHistory(id: number) {
  const response = await requestApi<ApiResponse<object>>(
    `/conversations/${id}/clear-history`,
    { method: "POST" }
  );
  if (!response.success) {
    throw new Error(response.message || "清除历史失败");
  }
}

export async function getMessages(
  conversationId: number,
  options?: { includeContextMarkers?: boolean; limit?: number }
) {
  const params = new URLSearchParams();
  if (options?.includeContextMarkers !== undefined) {
    params.set("includeContextMarkers", String(options.includeContextMarkers));
  }
  if (options?.limit !== undefined) {
    params.set("limit", String(options.limit));
  }
  const query = params.toString();
  const response = await requestApi<ApiResponse<ChatMessageDto[]>>(
    `/conversations/${conversationId}/messages${query ? `?${query}` : ""}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询消息失败");
  }
  return response.data;
}

export async function deleteMessage(conversationId: number, messageId: number) {
  const response = await requestApi<ApiResponse<object>>(
    `/conversations/${conversationId}/messages/${messageId}`,
    { method: "DELETE" }
  );
  if (!response.success) {
    throw new Error(response.message || "删除消息失败");
  }
}

export async function agentChat(
  agentId: number,
  request: AgentChatRequest
): Promise<AgentChatResponse> {
  const response = await requestApi<ApiResponse<AgentChatResponse>>(
    `/agents/${agentId}/chat`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "对话失败");
  }
  return response.data;
}

export async function cancelAgentChat(agentId: number, conversationId: number) {
  const response = await requestApi<ApiResponse<object>>(
    `/agents/${agentId}/chat/cancel`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ conversationId })
    }
  );
  if (!response.success) {
    throw new Error(response.message || "取消失败");
  }
}

/**
 * Create a streaming chat request that returns a ReadableStream of SSE events.
 * Returns both the stream and an abort controller for cancellation.
 */
export function createAgentChatStream(
  agentId: number,
  request: AgentChatRequest,
  mode: AgentStreamEventMode = "legacy"
) {
  const abortController = new AbortController();

  const headers: Record<string, string> = {
    "Content-Type": "application/json"
  };

  const token = getAccessToken();
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  const tenantId = getTenantId();
  if (tenantId) {
    headers["X-Tenant-Id"] = tenantId;
  }

  const afToken = getAntiforgeryToken();
  if (afToken) {
    headers["X-XSRF-TOKEN"] = afToken;
  }

  const streamUrl =
    mode === "react"
      ? `${API_BASE}/agents/${agentId}/chat/stream?eventMode=react`
      : `${API_BASE}/agents/${agentId}/chat/stream`;

  if (mode === "react") {
    headers["X-Stream-Event-Mode"] = "react";
  }

  const fetchPromise = fetch(streamUrl, {
    method: "POST",
    headers,
    body: JSON.stringify(request),
    signal: abortController.signal
  });

  return { fetchPromise, abortController };
}
