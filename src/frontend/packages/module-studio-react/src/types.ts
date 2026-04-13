import type { PagedResult } from "@atlas/shared-react-core/types";

export type StudioLocale = "zh-CN" | "en-US";
export type DevelopFocus = "overview" | "agents" | "workflow" | "chatflow" | "models" | "chat";
export type DevelopResourceKind = "agent" | "workflow" | "chatflow" | "model";

export interface AgentListItem {
  id: string;
  name: string;
  description?: string;
  status: string;
  modelName?: string;
  createdAt?: string;
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
}

export interface AgentCreateRequest {
  name: string;
  description?: string;
  systemPrompt?: string;
}

export interface AgentUpdateRequest {
  name: string;
  description?: string;
  systemPrompt?: string;
  modelConfigId?: string;
  modelName?: string;
  temperature?: number;
  maxTokens?: number;
}

export interface ConversationItem {
  id: string;
  title?: string;
  messageCount: number;
  createdAt: string;
}

export interface ChatMessageItem {
  id: string;
  role: "system" | "user" | "assistant";
  content: string;
  createdAt: string;
}

export interface ModelConfigItem {
  id: number;
  name: string;
  providerType: string;
  baseUrl?: string;
  defaultModel: string;
  modelId?: string;
  isEnabled: boolean;
  supportsEmbedding: boolean;
  enableStreaming?: boolean;
  enableReasoning?: boolean;
  enableTools?: boolean;
  enableVision?: boolean;
  enableJsonMode?: boolean;
  systemPrompt?: string;
  apiKeyMasked?: string;
  temperature?: number;
  maxTokens?: number;
  topP?: number;
  frequencyPenalty?: number;
  presencePenalty?: number;
  createdAt: string;
}

export interface ModelConfigStats {
  total: number;
  enabled: number;
  disabled: number;
  embeddingCount: number;
}

export interface ModelConfigCreateRequest {
  name: string;
  providerType: string;
  apiKey: string;
  baseUrl: string;
  defaultModel: string;
  supportsEmbedding: boolean;
  modelId?: string;
  systemPrompt?: string;
  enableStreaming?: boolean;
  enableReasoning?: boolean;
  enableTools?: boolean;
  enableVision?: boolean;
  enableJsonMode?: boolean;
  temperature?: number;
  maxTokens?: number;
  topP?: number;
  frequencyPenalty?: number;
  presencePenalty?: number;
}

export interface ModelConfigUpdateRequest {
  name: string;
  apiKey: string;
  baseUrl: string;
  defaultModel: string;
  isEnabled: boolean;
  supportsEmbedding: boolean;
  modelId?: string;
  systemPrompt?: string;
  enableStreaming?: boolean;
  enableReasoning?: boolean;
  enableTools?: boolean;
  enableVision?: boolean;
  enableJsonMode?: boolean;
  temperature?: number;
  maxTokens?: number;
  topP?: number;
  frequencyPenalty?: number;
  presencePenalty?: number;
}

export interface ModelConfigConnectionTestRequest {
  modelConfigId?: number;
  providerType: string;
  apiKey: string;
  baseUrl: string;
  model: string;
}

export interface ModelConfigConnectionTestResult {
  success: boolean;
  errorMessage?: string;
  latencyMs?: number;
}

export interface ModelConfigPromptTestRequest {
  modelConfigId?: number;
  providerType: string;
  apiKey: string;
  baseUrl: string;
  model: string;
  prompt: string;
  enableReasoning: boolean;
  enableTools: boolean;
  enableStreaming?: boolean;
}

export interface DevelopResourceSummary {
  id: string;
  kind: DevelopResourceKind;
  title: string;
  description?: string;
  status?: string;
  updatedAt?: string;
  meta?: string;
}

export interface StudioModuleApi {
  listAgents: (params?: { pageIndex?: number; pageSize?: number; keyword?: string; status?: string }) => Promise<PagedResult<AgentListItem>>;
  getAgent: (id: string) => Promise<AgentDetail>;
  createAgent: (request: AgentCreateRequest) => Promise<string>;
  updateAgent: (id: string, request: AgentUpdateRequest) => Promise<void>;
  listConversations: (agentId?: string) => Promise<PagedResult<ConversationItem>>;
  getMessages: (conversationId: string) => Promise<ChatMessageItem[]>;
  generateAssistant: (kind: "form" | "sql" | "workflow", description: string) => Promise<{ result: string; explanation: string } | null>;
  listModelConfigs: () => Promise<PagedResult<ModelConfigItem>>;
  getModelConfig: (id: number) => Promise<ModelConfigItem>;
  getModelConfigStats: (keyword?: string) => Promise<ModelConfigStats>;
  createModelConfig: (request: ModelConfigCreateRequest) => Promise<string>;
  updateModelConfig: (id: number, request: ModelConfigUpdateRequest) => Promise<void>;
  deleteModelConfig: (id: number) => Promise<void>;
  testModelConfigConnection: (request: ModelConfigConnectionTestRequest) => Promise<ModelConfigConnectionTestResult>;
  runModelConfigPromptTest: (request: ModelConfigPromptTestRequest) => Promise<string>;
}

export interface StudioPageProps {
  api: StudioModuleApi;
  locale: StudioLocale;
}
