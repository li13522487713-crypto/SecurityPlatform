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
  defaultWorkflowId?: string;
  defaultWorkflowName?: string;
  status: string;
}

export interface AgentCreateRequest {
  name: string;
  description?: string;
  systemPrompt?: string;
  modelConfigId?: string;
  modelName?: string;
  temperature?: number;
  maxTokens?: number;
  defaultWorkflowId?: string;
  defaultWorkflowName?: string;
}

export interface AgentUpdateRequest {
  name: string;
  description?: string;
  avatarUrl?: string;
  systemPrompt?: string;
  modelConfigId?: string;
  modelName?: string;
  temperature?: number;
  maxTokens?: number;
  defaultWorkflowId?: string;
  defaultWorkflowName?: string;
}

export interface ConversationItem {
  id: string;
  title?: string;
  messageCount: number;
  createdAt: string;
}

export interface ChatMessageItem {
  id: string;
  role: "system" | "user" | "assistant" | "tool";
  content: string;
  createdAt: string;
  metadata?: string;
}

export interface AgentChatStreamChunk {
  type: "chunk" | "final" | "thought";
  content: string;
}

export interface WorkflowListItem {
  id: string;
  name: string;
  description?: string;
  status?: number;
  latestVersionNumber?: number;
  updatedAt?: string;
}

export interface WorkflowBinding {
  workflowId?: string;
  workflowName?: string;
}

export interface WorkflowExecutionSummary {
  executionId: string;
  status?: string;
  outputsJson?: string;
  errorMessage?: string;
}

export interface WorkbenchTraceStep {
  nodeKey: string;
  status?: string;
  nodeType?: string;
  durationMs?: number;
  errorMessage?: string;
}

export interface WorkbenchTrace {
  executionId: string;
  status?: string;
  startedAt?: string;
  completedAt?: string;
  durationMs?: number;
  steps: WorkbenchTraceStep[];
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
  createConversation: (agentId: string, title?: string) => Promise<string>;
  sendAgentMessage: (
    agentId: string,
    request: { conversationId?: string; message: string; enableRag?: boolean }
  ) => AsyncIterable<AgentChatStreamChunk>;
  appendConversationMessage: (
    conversationId: string,
    request: { role: "system" | "user" | "assistant" | "tool"; content: string; metadata?: string }
  ) => Promise<string>;
  listWorkflows: (params?: { keyword?: string; status?: "draft" | "published" | "all" }) => Promise<WorkflowListItem[]>;
  bindAgentWorkflow: (agentId: string, workflowId?: string) => Promise<WorkflowBinding>;
  runWorkflowTask: (
    workflowId: string,
    incident: string
  ) => Promise<{ execution: WorkflowExecutionSummary; trace?: WorkbenchTrace }>;
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
