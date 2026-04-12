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
  defaultModel: string;
  isEnabled: boolean;
  supportsEmbedding: boolean;
  createdAt: string;
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
}

export interface StudioPageProps {
  api: StudioModuleApi;
  locale: StudioLocale;
}
