/**
 * Platform AI / Agent / 模型配置 / 多 Agent 编排 / 会话与流式对话 / 知识库 / AI 工作流 API 聚合层。
 */
import {
  getAccessToken,
  getAntiforgeryToken,
  getTenantId,
  type ApiResponse,
  type PagedRequest,
  type PagedResult
} from "@atlas/shared-core";
import { API_BASE, requestApi, toQuery } from "@/services/api-core";

function generateIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return `idem-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function resolveAppIdForFetch(): string | null {
  if (typeof window !== "undefined") {
    const match = window.location.pathname.match(/^\/apps\/([^/]+)/);
    if (match?.[1]) {
      return decodeURIComponent(match[1]);
    }
  }
  return null;
}

// ── 模型配置 ─────────────────────────────────────────

export interface ModelConfigDto {
  id: number;
  name: string;
  providerType: string;
  baseUrl: string;
  defaultModel: string;
  isEnabled: boolean;
  supportsEmbedding: boolean;
  apiKeyMasked?: string;
  createdAt: string;
}

export interface ModelConfigCreateRequest {
  name: string;
  providerType: string;
  apiKey: string;
  baseUrl: string;
  defaultModel: string;
  supportsEmbedding: boolean;
}

export interface ModelConfigUpdateRequest {
  name: string;
  apiKey: string;
  baseUrl: string;
  defaultModel: string;
  isEnabled: boolean;
  supportsEmbedding: boolean;
}

export interface ModelConfigTestRequest {
  modelConfigId?: number;
  providerType: string;
  apiKey: string;
  baseUrl: string;
  model: string;
}

export interface ModelConfigPromptTestRequest extends ModelConfigTestRequest {
  prompt: string;
  enableReasoning: boolean;
  enableTools: boolean;
}

export interface ModelConfigTestResult {
  success: boolean;
  errorMessage?: string;
  latencyMs?: number;
}

export interface ModelConfigStatsDto {
  total: number;
  enabled: number;
  disabled: number;
  embeddingCount: number;
}

export async function getModelConfigsPaged(request: PagedRequest) {
  const query = toQuery(request);
  const response = await requestApi<ApiResponse<PagedResult<ModelConfigDto>>>(`/model-configs?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询模型配置失败");
  }
  return response.data;
}

export async function getModelConfigById(id: number) {
  const response = await requestApi<ApiResponse<ModelConfigDto>>(`/model-configs/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询模型配置失败");
  }
  return response.data;
}

export async function getEnabledModelConfigs() {
  const response = await requestApi<ApiResponse<ModelConfigDto[]>>("/model-configs/enabled");
  if (!response.data) {
    throw new Error(response.message || "查询启用模型配置失败");
  }
  return response.data;
}

export async function getModelConfigStats(keyword?: string) {
  const query = new URLSearchParams();
  if (keyword) {
    query.set("keyword", keyword);
  }
  const url = query.size > 0 ? `/model-configs/stats?${query.toString()}` : "/model-configs/stats";
  const response = await requestApi<ApiResponse<ModelConfigStatsDto>>(url);
  if (!response.data) {
    throw new Error(response.message || "查询模型配置统计失败");
  }
  return response.data;
}

export async function createModelConfig(request: ModelConfigCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/model-configs", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建模型配置失败");
  }
}

export async function updateModelConfig(id: number, request: ModelConfigUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/model-configs/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新模型配置失败");
  }
}

export async function deleteModelConfig(id: number) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/model-configs/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除模型配置失败");
  }
}

export async function testModelConfigConnection(request: ModelConfigTestRequest) {
  const response = await requestApi<ApiResponse<ModelConfigTestResult>>(
    "/model-configs/test",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    },
    {
      suppressErrorMessage: true
    }
  );
  if (!response.data) {
    throw new Error(response.message || "测试连接失败");
  }
  return response.data;
}

export function createModelConfigPromptTestStream(request: ModelConfigPromptTestRequest) {
  const abortController = new AbortController();
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    Accept: "text/event-stream",
    "Idempotency-Key": generateIdempotencyKey()
  };

  const accessToken = getAccessToken();
  if (accessToken) {
    headers.Authorization = `Bearer ${accessToken}`;
  }

  const tenantId = getTenantId();
  if (tenantId) {
    headers["X-Tenant-Id"] = tenantId;
  }

  const csrfToken = getAntiforgeryToken();
  if (csrfToken) {
    headers["X-CSRF-TOKEN"] = csrfToken;
  }

  const fetchPromise = fetch(`${API_BASE}/model-configs/test/stream`, {
    method: "POST",
    headers,
    body: JSON.stringify(request),
    signal: abortController.signal,
    credentials: "include"
  });

  return { fetchPromise, abortController };
}

// ── Agent ───────────────────────────────────────────

export interface AgentListItem {
  id: string;
  name: string;
  description?: string;
  avatarUrl?: string;
  status: string;
  modelName?: string;
  createdAt: string;
  publishVersion: number;
}

export interface AgentDetail {
  id: string;
  name: string;
  description?: string;
  avatarUrl?: string;
  systemPrompt?: string;
  modelConfigId?: string;
  modelName?: string;
  temperature?: number;
  maxTokens?: number;
  status: string;
  creatorId: string;
  createdAt: string;
  updatedAt?: string;
  publishedAt?: string;
  publishVersion: number;
  enableMemory: boolean;
  enableShortTermMemory: boolean;
  enableLongTermMemory: boolean;
  longTermMemoryTopK: number;
  knowledgeBaseIds?: string[];
  pluginBindings?: AgentPluginBindingItem[];
}

export interface AgentPluginBindingItem {
  pluginId: string;
  sortOrder: number;
  isEnabled: boolean;
  toolConfigJson: string;
}

export interface AgentPluginBindingInput {
  pluginId: string;
  sortOrder: number;
  isEnabled: boolean;
  toolConfigJson?: string;
}

export interface AgentCreateRequest {
  name: string;
  description?: string;
  systemPrompt?: string;
  modelConfigId?: string;
  modelName?: string;
  temperature?: number;
  maxTokens?: number;
  enableMemory?: boolean;
  enableShortTermMemory?: boolean;
  enableLongTermMemory?: boolean;
  longTermMemoryTopK?: number;
}

export interface AgentUpdateRequest extends AgentCreateRequest {
  avatarUrl?: string;
  knowledgeBaseIds?: string[];
  pluginBindings?: AgentPluginBindingInput[];
}

export async function getAgentsPaged(request: PagedRequest, status?: string) {
  const query = toQuery(request, { status });
  const response = await requestApi<ApiResponse<PagedResult<AgentListItem>>>(`/agents?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询 Agent 列表失败");
  }
  return response.data;
}

export async function getAgentById(id: string) {
  const response = await requestApi<ApiResponse<AgentDetail>>(`/agents/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询 Agent 失败");
  }
  return response.data;
}

export async function createAgent(request: AgentCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/agents", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建 Agent 失败");
  }
}

export async function updateAgent(id: string, request: AgentUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agents/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新 Agent 失败");
  }
}

export async function deleteAgent(id: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agents/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除 Agent 失败");
  }
}

export async function duplicateAgent(id: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agents/${id}/duplicate`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "复制 Agent 失败");
  }
}

export async function publishAgent(id: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agents/${id}/publish`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "发布 Agent 失败");
  }
}

// ── Agent 发布版本 ───────────────────────────────────

export interface AgentPublicationItem {
  id: string;
  agentId: string;
  version: number;
  isActive: boolean;
  embedToken: string;
  embedTokenExpiresAt: string;
  releaseNote?: string;
  publishedByUserId: string;
  createdAt: string;
  updatedAt?: string;
  revokedAt?: string;
}

export interface AgentPublicationResult {
  publicationId: string;
  agentId: string;
  version: number;
  embedToken: string;
  embedTokenExpiresAt: string;
}

export interface AgentEmbedTokenResult {
  publicationId: string;
  agentId: string;
  version: number;
  embedToken: string;
  embedTokenExpiresAt: string;
}

export async function getAgentPublications(agentId: string) {
  const response = await requestApi<ApiResponse<AgentPublicationItem[]>>(
    `/agent-publications/agents/${agentId}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询发布版本失败");
  }
  return response.data;
}

export async function publishAgentPublication(agentId: string, releaseNote?: string) {
  const response = await requestApi<ApiResponse<AgentPublicationResult>>(
    `/agent-publications/agents/${agentId}/publish`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ releaseNote: releaseNote || undefined })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "发布 Agent 失败");
  }
  return response.data;
}

export async function rollbackAgentPublication(
  agentId: string,
  targetVersion: number,
  releaseNote?: string
) {
  const response = await requestApi<ApiResponse<AgentPublicationResult>>(
    `/agent-publications/agents/${agentId}/rollback`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        targetVersion,
        releaseNote: releaseNote || undefined
      })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "回滚 Agent 失败");
  }
  return response.data;
}

export async function regenerateAgentEmbedToken(agentId: string) {
  const response = await requestApi<ApiResponse<AgentEmbedTokenResult>>(
    `/agent-publications/agents/${agentId}/embed-token`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({})
    }
  );
  if (!response.data) {
    throw new Error(response.message || "更新 Embed Token 失败");
  }
  return response.data;
}

// ── 多 Agent 编排 ───────────────────────────────────

export type MultiAgentOrchestrationMode = 0 | 1;
export type MultiAgentOrchestrationStatus = 0 | 1 | 2;
export type MultiAgentExecutionStatus = 0 | 1 | 2 | 3 | 4 | 5;

export interface MultiAgentMemberInput {
  agentId: string;
  alias?: string;
  sortOrder: number;
  isEnabled: boolean;
  promptPrefix?: string;
}

export interface MultiAgentOrchestrationListItem {
  id: number;
  name: string;
  description?: string;
  mode: MultiAgentOrchestrationMode;
  status: MultiAgentOrchestrationStatus;
  memberCount: number;
  creatorUserId: number;
  createdAt: string;
  updatedAt: string;
}

export interface MultiAgentOrchestrationDetail extends MultiAgentOrchestrationListItem {
  members: MultiAgentMemberInput[];
}

export interface MultiAgentExecutionStep {
  agentId: number;
  agentName: string;
  alias?: string;
  inputMessage: string;
  outputMessage?: string;
  status: MultiAgentExecutionStatus;
  errorMessage?: string;
  startedAt: string;
  completedAt?: string;
}

export interface MultiAgentExecutionResult {
  executionId: number;
  orchestrationId: number;
  status: MultiAgentExecutionStatus;
  outputMessage?: string;
  errorMessage?: string;
  steps: MultiAgentExecutionStep[];
  startedAt: string;
  completedAt?: string;
}

export interface MultiAgentRunRequest {
  message: string;
  enableRag?: boolean;
}

export interface MultiAgentStreamEvent {
  eventType: string;
  data: string;
  parsed?: unknown;
}

export async function getMultiAgentOrchestrationsPaged(request: PagedRequest) {
  const query = toQuery(request);
  const response = await requestApi<ApiResponse<PagedResult<MultiAgentOrchestrationListItem>>>(
    `/multi-agent-orchestrations?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询多 Agent 编排失败");
  }

  return response.data;
}

export async function getMultiAgentOrchestrationById(id: number) {
  const response = await requestApi<ApiResponse<MultiAgentOrchestrationDetail>>(
    `/multi-agent-orchestrations/${id}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询多 Agent 编排详情失败");
  }

  return response.data;
}

export async function createMultiAgentOrchestration(request: {
  name: string;
  description?: string;
  mode: MultiAgentOrchestrationMode;
  members: MultiAgentMemberInput[];
}) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/multi-agent-orchestrations", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建多 Agent 编排失败");
  }

  return response.data?.id;
}

export async function updateMultiAgentOrchestration(
  id: number,
  request: {
    name: string;
    description?: string;
    mode: MultiAgentOrchestrationMode;
    members: MultiAgentMemberInput[];
    status?: MultiAgentOrchestrationStatus;
  }
) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/multi-agent-orchestrations/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新多 Agent 编排失败");
  }
}

export async function deleteMultiAgentOrchestration(id: number) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/multi-agent-orchestrations/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除多 Agent 编排失败");
  }
}

export async function runMultiAgentOrchestration(id: number, request: MultiAgentRunRequest) {
  const response = await requestApi<ApiResponse<MultiAgentExecutionResult>>(
    `/multi-agent-orchestrations/${id}/run`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "执行多 Agent 编排失败");
  }

  return response.data;
}

export async function getMultiAgentExecutionById(executionId: number) {
  const response = await requestApi<ApiResponse<MultiAgentExecutionResult>>(
    `/multi-agent-orchestrations/executions/${executionId}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询多 Agent 执行结果失败");
  }

  return response.data;
}

export async function streamMultiAgentOrchestration(
  id: number,
  request: MultiAgentRunRequest,
  onEvent: (event: MultiAgentStreamEvent) => void,
  signal?: AbortSignal
) {
  const headers = new Headers({
    "Content-Type": "application/json",
    Accept: "text/event-stream",
    "Idempotency-Key": generateIdempotencyKey()
  });

  const token = getAccessToken();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const tenantId = getTenantId();
  if (tenantId) {
    headers.set("X-Tenant-Id", tenantId);
  }

  const csrfToken = getAntiforgeryToken();
  if (csrfToken) {
    headers.set("X-CSRF-TOKEN", csrfToken);
  }

  const response = await fetch(`${API_BASE}/multi-agent-orchestrations/${id}/stream`, {
    method: "POST",
    credentials: "include",
    headers,
    body: JSON.stringify(request),
    signal
  });

  if (!response.ok) {
    let message = "流式执行多 Agent 编排失败";
    try {
      const payload = (await response.json()) as ApiResponse<unknown>;
      if (payload?.message) {
        message = payload.message;
      }
    } catch {
      // ignored
    }
    throw new Error(message);
  }

  if (!response.body) {
    throw new Error("流式响应为空");
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  while (true) {
    const { value, done } = await reader.read();
    if (done) {
      break;
    }

    buffer += decoder.decode(value, { stream: true });
    const chunks = buffer.split("\n\n");
    buffer = chunks.pop() ?? "";
    chunks.forEach((chunk) => {
      const lines = chunk.split("\n");
      let eventType = "message";
      const dataLines: string[] = [];
      lines.forEach((line) => {
        if (line.startsWith("event:")) {
          eventType = line.slice("event:".length).trim();
        } else if (line.startsWith("data:")) {
          dataLines.push(line.slice("data:".length).trim());
        }
      });

      const data = dataLines.join("\n");
      if (!data) {
        return;
      }

      let parsed: unknown;
      try {
        parsed = JSON.parse(data);
      } catch {
        parsed = undefined;
      }
      onEvent({ eventType, data, parsed });
    });
  }
}

// ── 会话与 Agent 对话（非流式 / 流式 / 取消）────────────

/** 雪花 long ID：禁止用 JS number，避免超过 MAX_SAFE_INTEGER 时精度丢失 */
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

export async function getConversationsPaged(request: PagedRequest, agentId?: SnowflakeId) {
  const query = toQuery(request, { agentId: agentId || undefined });
  const response = await requestApi<ApiResponse<PagedResult<ConversationDto>>>(`/conversations?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询会话列表失败");
  }
  return response.data;
}

export async function getConversationById(id: SnowflakeId) {
  const response = await requestApi<ApiResponse<ConversationDto>>(`/conversations/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询会话失败");
  }
  return response.data;
}

export async function createConversation(agentId: SnowflakeId, title?: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/conversations", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ agentId, title })
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "创建会话失败");
  }
  return response.data.id;
}

export async function updateConversation(id: SnowflakeId, title: string) {
  const response = await requestApi<ApiResponse<object>>(`/conversations/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ title })
  });
  if (!response.success) {
    throw new Error(response.message || "更新会话失败");
  }
}

export async function deleteConversation(id: SnowflakeId) {
  const response = await requestApi<ApiResponse<object>>(`/conversations/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除会话失败");
  }
}

export async function clearConversationContext(id: SnowflakeId) {
  const response = await requestApi<ApiResponse<object>>(`/conversations/${id}/clear-context`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "清除上下文失败");
  }
}

export async function clearConversationHistory(id: SnowflakeId) {
  const response = await requestApi<ApiResponse<object>>(`/conversations/${id}/clear-history`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "清除历史失败");
  }
}

export async function getMessages(
  conversationId: SnowflakeId,
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

export async function deleteMessage(conversationId: SnowflakeId, messageId: SnowflakeId) {
  const response = await requestApi<ApiResponse<object>>(
    `/conversations/${conversationId}/messages/${messageId}`,
    { method: "DELETE" }
  );
  if (!response.success) {
    throw new Error(response.message || "删除消息失败");
  }
}

export async function agentChat(agentId: SnowflakeId, request: AgentChatRequest): Promise<AgentChatResponse> {
  const response = await requestApi<ApiResponse<AgentChatResponse>>(`/agents/${agentId}/chat`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "对话失败");
  }
  return response.data;
}

export async function cancelAgentChat(agentId: SnowflakeId, conversationId: SnowflakeId) {
  const response = await requestApi<ApiResponse<object>>(`/agents/${agentId}/chat/cancel`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ conversationId })
  });
  if (!response.success) {
    throw new Error(response.message || "取消失败");
  }
}

export function createAgentChatStream(
  agentId: SnowflakeId,
  request: AgentChatRequest,
  mode: AgentStreamEventMode = "legacy"
) {
  const abortController = new AbortController();

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    Accept: "text/event-stream",
    "Idempotency-Key": generateIdempotencyKey()
  };

  const token = getAccessToken();
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const tenantId = getTenantId();
  if (tenantId) {
    headers["X-Tenant-Id"] = tenantId;
  }

  const appId = resolveAppIdForFetch();
  if (appId) {
    headers["X-App-Id"] = appId;
    headers["X-App-Workspace"] = "1";
  }

  const afToken = getAntiforgeryToken();
  if (afToken) {
    headers["X-CSRF-TOKEN"] = afToken;
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
    credentials: "include",
    headers,
    body: JSON.stringify(request),
    signal: abortController.signal
  });

  return { fetchPromise, abortController };
}

// ── 知识库 ──────────────────────────────────────────

export type KnowledgeBaseType = 0 | 1 | 2;
export type DocumentProcessingStatus = 0 | 1 | 2 | 3;
export type KnowledgeRetrievalStrategy = "vector" | "bm25" | "hybrid";

export interface KnowledgeBaseDto {
  id: number;
  name: string;
  description?: string;
  type: KnowledgeBaseType;
  documentCount: number;
  chunkCount: number;
  createdAt: string;
}

export interface KnowledgeDocumentDto {
  id: number;
  knowledgeBaseId: number;
  fileId?: number;
  fileName: string;
  contentType?: string;
  fileSizeBytes: number;
  status: DocumentProcessingStatus;
  errorMessage?: string;
  chunkCount: number;
  createdAt: string;
  processedAt?: string;
}

export interface DocumentChunkDto {
  id: number;
  knowledgeBaseId: number;
  documentId: number;
  chunkIndex: number;
  content: string;
  startOffset: number;
  endOffset: number;
  hasEmbedding: boolean;
  createdAt: string;
}

export interface KnowledgeBaseCreateRequest {
  name: string;
  description?: string;
  type: KnowledgeBaseType;
}

export interface KnowledgeBaseUpdateRequest extends KnowledgeBaseCreateRequest {}

export interface DocumentCreateRequest {
  fileId: number;
}

export interface DocumentResegmentRequest {
  chunkSize?: number;
  overlap?: number;
  strategy?: 0 | 1 | 2;
}

export interface KnowledgeRetrievalConfigDto {
  strategy: KnowledgeRetrievalStrategy;
  enableRerank: boolean;
  vectorTopK: number;
  bm25TopK: number;
  bm25CandidateCount: number;
  rrfK: number;
}

export interface KnowledgeRetrievalConfigUpdateRequest extends KnowledgeRetrievalConfigDto {}

export interface KnowledgeRetrievalTestRequest {
  query: string;
  topK?: number;
}

export interface KnowledgeRetrievalTestItem {
  knowledgeBaseId: number;
  documentId: number;
  chunkId: number;
  content: string;
  score: number;
  documentName?: string;
}

export interface ChunkCreateRequest {
  documentId: number;
  chunkIndex: number;
  content: string;
  startOffset: number;
  endOffset: number;
}

export interface ChunkUpdateRequest {
  content: string;
  startOffset: number;
  endOffset: number;
}

export async function getKnowledgeBasesPaged(request: PagedRequest, keyword?: string) {
  const query = toQuery(request, { keyword });
  const response = await requestApi<ApiResponse<PagedResult<KnowledgeBaseDto>>>(`/knowledge-bases?${query}`);
  if (!response.data) throw new Error(response.message || "查询知识库失败");
  return response.data;
}

export async function getKnowledgeBaseById(id: number) {
  const response = await requestApi<ApiResponse<KnowledgeBaseDto>>(`/knowledge-bases/${id}`);
  if (!response.data) throw new Error(response.message || "查询知识库失败");
  return response.data;
}

export async function createKnowledgeBase(request: KnowledgeBaseCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/knowledge-bases", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) throw new Error(response.message || "创建知识库失败");
  return Number(response.data.id);
}

export async function updateKnowledgeBase(id: number, request: KnowledgeBaseUpdateRequest) {
  const response = await requestApi<ApiResponse<object>>(`/knowledge-bases/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新知识库失败");
}

export async function deleteKnowledgeBase(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/knowledge-bases/${id}`, { method: "DELETE" });
  if (!response.success) throw new Error(response.message || "删除知识库失败");
}

export async function getKnowledgeDocumentsPaged(knowledgeBaseId: number, request: PagedRequest) {
  const query = toQuery(request);
  const response = await requestApi<ApiResponse<PagedResult<KnowledgeDocumentDto>>>(
    `/knowledge-bases/${knowledgeBaseId}/documents?${query}`
  );
  if (!response.data) throw new Error(response.message || "查询文档失败");
  return response.data;
}

export async function createKnowledgeDocument(knowledgeBaseId: number, request: DocumentCreateRequest) {
  const form = new FormData();
  form.append("fileId", String(request.fileId));
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/knowledge-bases/${knowledgeBaseId}/documents`,
    {
      method: "POST",
      body: form
    }
  );
  if (!response.success || !response.data) throw new Error(response.message || "新增文档失败");
  return Number(response.data.id);
}

export async function createKnowledgeDocumentByFile(knowledgeBaseId: number, file: File) {
  const form = new FormData();
  form.append("file", file);
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/knowledge-bases/${knowledgeBaseId}/documents`,
    {
      method: "POST",
      body: form
    }
  );
  if (!response.success || !response.data) throw new Error(response.message || "新增文档失败");
  return Number(response.data.id);
}

export async function deleteKnowledgeDocument(knowledgeBaseId: number, documentId: number) {
  const response = await requestApi<ApiResponse<object>>(
    `/knowledge-bases/${knowledgeBaseId}/documents/${documentId}`,
    { method: "DELETE" }
  );
  if (!response.success) throw new Error(response.message || "删除文档失败");
}

export async function getDocumentProgress(knowledgeBaseId: number, documentId: number) {
  const response = await requestApi<ApiResponse<{
    id: number;
    status: DocumentProcessingStatus;
    chunkCount: number;
    errorMessage?: string;
    processedAt?: string;
  }>>(`/knowledge-bases/${knowledgeBaseId}/documents/${documentId}/progress`);
  if (!response.data) throw new Error(response.message || "查询进度失败");
  return response.data;
}

export async function resegmentDocument(
  knowledgeBaseId: number,
  documentId: number,
  request: DocumentResegmentRequest
) {
  const response = await requestApi<ApiResponse<object>>(
    `/knowledge-bases/${knowledgeBaseId}/documents/${documentId}/resegment`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "重分段失败");
}

export async function getDocumentChunksPaged(
  knowledgeBaseId: number,
  documentId: number,
  request: PagedRequest
) {
  const query = toQuery(request);
  const response = await requestApi<ApiResponse<PagedResult<DocumentChunkDto>>>(
    `/knowledge-bases/${knowledgeBaseId}/documents/${documentId}/chunks?${query}`
  );
  if (!response.data) throw new Error(response.message || "查询分片失败");
  return response.data;
}

export async function createChunk(knowledgeBaseId: number, request: ChunkCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/knowledge-bases/${knowledgeBaseId}/chunks`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) throw new Error(response.message || "新增分片失败");
  return Number(response.data.id);
}

export async function updateChunk(knowledgeBaseId: number, chunkId: number, request: ChunkUpdateRequest) {
  const response = await requestApi<ApiResponse<object>>(`/knowledge-bases/${knowledgeBaseId}/chunks/${chunkId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新分片失败");
}

export async function deleteChunk(knowledgeBaseId: number, chunkId: number) {
  const response = await requestApi<ApiResponse<object>>(`/knowledge-bases/${knowledgeBaseId}/chunks/${chunkId}`, {
    method: "DELETE"
  });
  if (!response.success) throw new Error(response.message || "删除分片失败");
}

export async function getKnowledgeRetrievalConfig(knowledgeBaseId: number) {
  const response = await requestApi<ApiResponse<KnowledgeRetrievalConfigDto>>(
    `/knowledge-bases/${knowledgeBaseId}/retrieval-config`
  );
  if (!response.data) {
    throw new Error(response.message || "查询检索配置失败");
  }
  return response.data;
}

export async function updateKnowledgeRetrievalConfig(
  knowledgeBaseId: number,
  request: KnowledgeRetrievalConfigUpdateRequest
) {
  const response = await requestApi<ApiResponse<object>>(`/knowledge-bases/${knowledgeBaseId}/retrieval-config`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新检索配置失败");
  }
}

export async function testKnowledgeRetrieval(knowledgeBaseId: number, request: KnowledgeRetrievalTestRequest) {
  const response = await requestApi<ApiResponse<KnowledgeRetrievalTestItem[]>>(
    `/knowledge-bases/${knowledgeBaseId}/retrieval-test`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "检索测试失败");
  }
  return response.data;
}

// ── AI 工作流 ───────────────────────────────────────

export interface AiWorkflowDefinitionDto {
  id: number;
  name: string;
  description?: string;
  status: number | string;
  publishVersion: number;
  creatorId: number;
  createdAt: string;
  updatedAt?: string;
  publishedAt?: string;
}

export interface AiWorkflowDetailDto extends AiWorkflowDefinitionDto {
  canvasJson: string;
  definitionJson: string;
}

export interface AiWorkflowNodeTypeDto {
  key: string;
  name: string;
  category: string;
  description: string;
}

export async function getAiWorkflowsPaged(request: PagedRequest, keyword?: string) {
  const query = toQuery(request, { keyword });
  const response = await requestApi<ApiResponse<PagedResult<AiWorkflowDefinitionDto>>>(`/ai-workflows?${query}`);
  if (!response.data) throw new Error(response.message || "查询工作流失败");
  return response.data;
}

export async function getAiWorkflowById(id: number) {
  const response = await requestApi<ApiResponse<AiWorkflowDetailDto>>(`/ai-workflows/${id}`);
  if (!response.data) throw new Error(response.message || "查询工作流失败");
  return response.data;
}

export async function createAiWorkflow(request: {
  name: string;
  description?: string;
  canvasJson: string;
  definitionJson: string;
}) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/ai-workflows", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) throw new Error(response.message || "创建工作流失败");
  return Number(response.data.id);
}

export async function saveAiWorkflow(id: number, request: { canvasJson: string; definitionJson: string }) {
  const response = await requestApi<ApiResponse<object>>(`/ai-workflows/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "保存工作流失败");
}

export async function updateAiWorkflowMeta(id: number, request: { name: string; description?: string }) {
  const response = await requestApi<ApiResponse<object>>(`/ai-workflows/${id}/meta`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新元信息失败");
}

export async function deleteAiWorkflow(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-workflows/${id}`, { method: "DELETE" });
  if (!response.success) throw new Error(response.message || "删除工作流失败");
}

export async function copyAiWorkflow(id: number) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/ai-workflows/${id}/copy`, { method: "POST" });
  if (!response.success || !response.data) throw new Error(response.message || "复制工作流失败");
  return Number(response.data.id);
}

export async function publishAiWorkflow(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-workflows/${id}/publish`, { method: "POST" });
  if (!response.success) throw new Error(response.message || "发布工作流失败");
}

export async function validateAiWorkflow(id: number) {
  const response = await requestApi<ApiResponse<{ isValid: boolean; errors: string[] }>>(
    `/ai-workflows/${id}/validate`,
    {
      method: "POST"
    }
  );
  if (!response.data) throw new Error(response.message || "校验失败");
  return response.data;
}

export async function runAiWorkflow(id: number, inputs: Record<string, unknown>) {
  const response = await requestApi<ApiResponse<{ executionId: string }>>(`/ai-workflows/${id}/run`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ inputs })
  });
  if (!response.data) throw new Error(response.message || "执行失败");
  return response.data;
}

export async function cancelAiWorkflowExecution(executionId: string) {
  const response = await requestApi<ApiResponse<object>>(`/ai-workflows/executions/${executionId}/cancel`, {
    method: "POST"
  });
  if (!response.success) throw new Error(response.message || "取消执行失败");
}

export async function getAiWorkflowExecutionProgress(executionId: string) {
  const response = await requestApi<ApiResponse<{
    executionId: string;
    workflowId: string;
    version: number;
    status: string;
    createdAt: string;
    completedAt?: string;
  }>>(`/ai-workflows/executions/${executionId}/progress`);
  if (!response.data) throw new Error(response.message || "查询执行进度失败");
  return response.data;
}

export async function getAiWorkflowExecutionNodes(executionId: string) {
  const response = await requestApi<
    ApiResponse<
      Array<{
        pointerId: string;
        stepId: number;
        stepName?: string;
        status: string;
        startTime?: string;
        endTime?: string;
        outcome?: unknown;
      }>
    >
  >(`/ai-workflows/executions/${executionId}/nodes`);
  if (!response.data) throw new Error(response.message || "查询节点历史失败");
  return response.data;
}

export async function getAiWorkflowNodeTypes() {
  const response = await requestApi<ApiResponse<AiWorkflowNodeTypeDto[]>>(`/ai-workflows/node-types`);
  if (!response.data) throw new Error(response.message || "查询节点类型失败");
  return response.data;
}

export interface AiWorkflowVersionItem {
  snapshotId: number;
  version: number;
  workflowName: string;
  publishedByUserId: number;
  publishedAt: string;
  changeLog?: string;
}

export interface AiWorkflowVersionDiff {
  workflowDefinitionId: number;
  fromVersion: number;
  toVersion: number;
  addedNodeIds: string[];
  removedNodeIds: string[];
  modifiedNodeIds: string[];
  addedEdges: number;
  removedEdges: number;
}

export interface AiWorkflowRollbackResult {
  workflowDefinitionId: number;
  newVersion: number;
  rolledBackFromVersion: number;
}

export async function getAiWorkflowVersions(id: number): Promise<AiWorkflowVersionItem[]> {
  const response = await requestApi<ApiResponse<AiWorkflowVersionItem[]>>(`/ai-workflows/${id}/versions`);
  if (!response.data) throw new Error(response.message || "查询版本历史失败");
  return response.data;
}

export async function getAiWorkflowVersionDiff(id: number, from: number, to: number): Promise<AiWorkflowVersionDiff> {
  const response = await requestApi<ApiResponse<AiWorkflowVersionDiff>>(
    `/ai-workflows/${id}/versions/diff?from=${from}&to=${to}`
  );
  if (!response.data) throw new Error(response.message || "查询版本差异失败");
  return response.data;
}

export async function rollbackAiWorkflow(id: number, version: number): Promise<AiWorkflowRollbackResult> {
  const response = await requestApi<ApiResponse<AiWorkflowRollbackResult>>(
    `/ai-workflows/${id}/versions/${version}/rollback`,
    { method: "POST" }
  );
  if (!response.data) throw new Error(response.message || "版本回滚失败");
  return response.data;
}
