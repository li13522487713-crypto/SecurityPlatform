import type { PagedResult } from "@atlas/shared-react-core/types";

export type StudioLocale = "zh-CN" | "en-US";
export type DevelopFocus = "overview" | "agents" | "projects" | "workflow" | "chatflow" | "plugins" | "data" | "models" | "chat";
export type DevelopResourceKind = "agent" | "workflow" | "chatflow" | "model";
export type WorkspaceIdeResourceType = "agent" | "app" | "workflow" | "chatflow" | "plugin" | "knowledge-base" | "database";

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
  personaMarkdown?: string;
  goals?: string;
  replyLogic?: string;
  outputFormat?: string;
  constraints?: string;
  openingMessage?: string;
  presetQuestions?: string[];
  knowledgeBindings?: AgentKnowledgeBinding[];
  databaseBindings?: AgentDatabaseBinding[];
  variableBindings?: AgentVariableBinding[];
  knowledgeBaseIds?: number[];
  pluginBindings?: AgentPluginBinding[];
  databaseBindingIds?: number[];
  variableBindingIds?: number[];
  modelConfigId?: string;
  modelName?: string;
  temperature?: number;
  maxTokens?: number;
  defaultWorkflowId?: string;
  defaultWorkflowName?: string;
  enableMemory?: boolean;
  enableShortTermMemory?: boolean;
  enableLongTermMemory?: boolean;
  longTermMemoryTopK?: number;
  status: string;
}

export interface AgentCreateRequest {
  name: string;
  description?: string;
  avatarUrl?: string;
  systemPrompt?: string;
  personaMarkdown?: string;
  goals?: string;
  replyLogic?: string;
  outputFormat?: string;
  constraints?: string;
  openingMessage?: string;
  presetQuestions?: string[];
  knowledgeBindings?: AgentKnowledgeBindingInput[];
  databaseBindings?: AgentDatabaseBindingInput[];
  variableBindings?: AgentVariableBindingInput[];
  knowledgeBaseIds?: number[];
  pluginBindings?: AgentPluginBindingInput[];
  databaseBindingIds?: number[];
  variableBindingIds?: number[];
  modelConfigId?: string;
  modelName?: string;
  temperature?: number;
  maxTokens?: number;
  defaultWorkflowId?: string;
  defaultWorkflowName?: string;
  enableMemory?: boolean;
  enableShortTermMemory?: boolean;
  enableLongTermMemory?: boolean;
  longTermMemoryTopK?: number;
}

export interface AgentUpdateRequest {
  name: string;
  description?: string;
  avatarUrl?: string;
  systemPrompt?: string;
  personaMarkdown?: string;
  goals?: string;
  replyLogic?: string;
  outputFormat?: string;
  constraints?: string;
  openingMessage?: string;
  presetQuestions?: string[];
  knowledgeBindings?: AgentKnowledgeBindingInput[];
  databaseBindings?: AgentDatabaseBindingInput[];
  variableBindings?: AgentVariableBindingInput[];
  knowledgeBaseIds?: number[];
  pluginBindings?: AgentPluginBindingInput[];
  databaseBindingIds?: number[];
  variableBindingIds?: number[];
  modelConfigId?: string;
  modelName?: string;
  temperature?: number;
  maxTokens?: number;
  defaultWorkflowId?: string;
  defaultWorkflowName?: string;
  enableMemory?: boolean;
  enableShortTermMemory?: boolean;
  enableLongTermMemory?: boolean;
  longTermMemoryTopK?: number;
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

export interface AgentKnowledgeBinding {
  knowledgeBaseId: number;
  isEnabled: boolean;
  invokeMode: "auto" | "manual";
  topK: number;
  scoreThreshold?: number;
  enabledContentTypes: Array<"text" | "table" | "image">;
  rewriteQueryTemplate?: string;
}

export interface AgentKnowledgeBindingInput extends AgentKnowledgeBinding {}

export interface AgentDatabaseBinding {
  databaseId: number;
  alias?: string;
  accessMode: "readonly" | "readwrite";
  tableAllowlist: string[];
  isDefault: boolean;
}

export interface AgentDatabaseBindingInput extends AgentDatabaseBinding {}

export interface AgentVariableBinding {
  variableId: number;
  alias?: string;
  isRequired: boolean;
  defaultValueOverride?: string;
}

export interface AgentVariableBindingInput extends AgentVariableBinding {}

export interface AgentPluginBinding {
  pluginId: number;
  sortOrder: number;
  isEnabled: boolean;
  toolConfigJson?: string;
  toolBindings?: AgentPluginToolBinding[];
}

export interface AgentPluginBindingInput extends AgentPluginBinding {}

export interface AgentPluginToolBinding {
  apiId: number;
  isEnabled: boolean;
  timeoutSeconds: number;
  failurePolicy: "skip" | "fail";
  parameterBindings: AgentPluginParameterBinding[];
}

export interface AgentPluginParameterBinding {
  parameterName: string;
  valueSource: "literal" | "variable";
  literalValue?: string;
  variableKey?: string;
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

export interface StudioApplicationSummary {
  id: string;
  name: string;
  description?: string;
  icon?: string;
  status?: string;
  publishVersion?: number;
  workflowId?: string;
  entryRoute?: string;
  isFavorite?: boolean;
  updatedAt?: string;
  lastEditedAt?: string;
  badge?: string;
}

export interface StudioApplicationCreateRequest {
  name: string;
  description?: string;
  icon?: string;
}

export interface StudioApplicationUpdateRequest {
  name: string;
  description?: string;
  icon?: string;
}

export interface WorkspaceIdeSummary {
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

export interface WorkspaceIdeResource {
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

export interface StudioWorkspaceOverview {
  appId: string;
  memberCount: number;
  roleCount: number;
  departmentCount: number;
  positionCount: number;
  projectCount: number;
  uncoveredMemberCount: number;
  applications: StudioApplicationSummary[];
}

export interface StudioModuleApi {
  listAgents: (params?: { pageIndex?: number; pageSize?: number; keyword?: string; status?: string }) => Promise<PagedResult<AgentListItem>>;
  getAgent: (id: string) => Promise<AgentDetail>;
  createAgent: (request: AgentCreateRequest) => Promise<string>;
  updateAgent: (id: string, request: AgentUpdateRequest) => Promise<void>;
  getWorkspaceOverview: () => Promise<StudioWorkspaceOverview>;
  getWorkspaceSummary: () => Promise<WorkspaceIdeSummary>;
  listWorkspaceResources: (params?: {
    keyword?: string;
    resourceType?: WorkspaceIdeResourceType;
    favoriteOnly?: boolean;
    pageIndex?: number;
    pageSize?: number;
  }) => Promise<PagedResult<WorkspaceIdeResource>>;
  createApplication: (request: StudioApplicationCreateRequest) => Promise<{ appId: string; workflowId: string; entryRoute: string }>;
  updateApplication: (id: string, request: StudioApplicationUpdateRequest) => Promise<void>;
  deleteApplication: (id: string) => Promise<void>;
  toggleWorkspaceFavorite: (resourceType: WorkspaceIdeResourceType, resourceId: string, isFavorite: boolean) => Promise<void>;
  recordWorkspaceActivity: (request: { resourceType: WorkspaceIdeResourceType; resourceId: number; resourceTitle: string; entryRoute: string }) => Promise<void>;
  listConversations: (agentId?: string) => Promise<PagedResult<ConversationItem>>;
  getMessages: (conversationId: string) => Promise<ChatMessageItem[]>;
  createConversation: (agentId: string, title?: string) => Promise<string>;
  deleteConversation: (conversationId: string) => Promise<void>;
  clearConversationContext: (conversationId: string) => Promise<void>;
  clearConversationHistory: (conversationId: string) => Promise<void>;
  sendAgentMessage: (
    agentId: string,
    request: { conversationId?: string; message: string; enableRag?: boolean }
  ) => AsyncIterable<AgentChatStreamChunk>;
  appendConversationMessage: (
    conversationId: string,
    request: { role: "system" | "user" | "assistant" | "tool"; content: string; metadata?: string }
  ) => Promise<string>;
  listWorkflows: (params?: { keyword?: string; status?: "draft" | "published" | "all" }) => Promise<WorkflowListItem[]>;
  listPlugins: () => Promise<Array<{ id: number; name: string; category?: string; status: number }>>;
  getPluginDetail: (pluginId: number) => Promise<{
    id: number;
    name: string;
    category?: string;
    apis: Array<{ id: number; name: string; requestSchemaJson: string; timeoutSeconds: number; isEnabled: boolean }>;
  }>;
  listKnowledgeBases: () => Promise<Array<{ id: number; name: string; type: number }>>;
  listDatabases: () => Promise<Array<{ id: number; name: string; botId?: number }>>;
  listBotVariables: (botId: string) => Promise<Array<{ id: number; key: string; scopeId?: number }>>;
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
