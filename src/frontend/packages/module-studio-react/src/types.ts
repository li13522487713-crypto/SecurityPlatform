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
  publishVersion?: number;
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

export interface StudioApplicationPublishRecord {
  id: string;
  appId: string;
  version: string;
  releaseNote?: string;
  publishedByUserId: string;
  createdAt: string;
}

export interface StudioApplicationConversationTemplate {
  id: string;
  appId: string;
  name: string;
  createMethod: string;
  sourceWorkflowId?: string;
  sourceWorkflowName?: string;
  connectorId?: string;
  isDefault: boolean;
  version: number;
  publishedVersion: number;
  createdAt: string;
  updatedAt?: string;
}

export interface StudioApplicationConversationTemplateCreateRequest {
  name: string;
  createMethod: string;
  sourceWorkflowId?: string;
  connectorId?: string;
  isDefault?: boolean;
  configJson?: string;
}

export interface StudioAssistantPublication {
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

export interface StudioAssistantPublishResult {
  publicationId: string;
  agentId: string;
  version: number;
  embedToken: string;
  embedTokenExpiresAt: string;
}

export interface StudioVariableItem {
  id: number;
  key: string;
  value?: string;
  scope: number;
  scopeId?: number;
  createdAt: string;
  updatedAt?: string;
}

export interface StudioSystemVariableDefinition {
  key: string;
  name: string;
  description: string;
  defaultValue?: string;
}

export interface StudioKnowledgeBaseDetail {
  id: number;
  name: string;
  description?: string;
  type: number;
  documentCount: number;
  chunkCount: number;
  createdAt: string;
}

export interface StudioDatabaseDetail {
  id: number;
  name: string;
  description?: string;
  botId?: number;
  recordCount: number;
  createdAt: string;
  updatedAt?: string;
  tableSchema: string;
}

export interface StudioDatabaseRecordItem {
  id: number;
  databaseId: number;
  dataJson: string;
  createdAt: string;
  updatedAt?: string;
}

export interface StudioDatabaseRecordUpsertRequest {
  dataJson: string;
}

export interface StudioDatabaseSchemaValidationResult {
  isValid: boolean;
  errors: string[];
}

export interface StudioDatabaseImportProgress {
  taskId: number;
  databaseId: number;
  status: number;
  totalRows: number;
  succeededRows: number;
  failedRows: number;
  errorMessage?: string;
  createdAt: string;
  updatedAt?: string;
  /** D5：导入任务来源；0=File（CSV）、1=Inline（异步批量）。 */
  source?: number;
}

/** D5：批量同步插入请求；rows[] 每项是单条记录的 dataJson。 */
export interface StudioDatabaseRecordBulkCreateRequest {
  rows: string[];
}

export interface StudioDatabaseRecordBulkRowResult {
  index: number;
  success: boolean;
  id?: string;
  errorMessage?: string;
}

export interface StudioDatabaseRecordBulkCreateResult {
  total: number;
  succeeded: number;
  failed: number;
  rows: StudioDatabaseRecordBulkRowResult[];
}

export interface StudioDatabaseBulkJobAccepted {
  taskId: number;
  rowCount: number;
}

export interface StudioPluginApiSummary {
  id: number;
  name: string;
  method: string;
  path: string;
  requestSchemaJson: string;
  timeoutSeconds: number;
  isEnabled: boolean;
}

export interface StudioPluginDetail {
  id: number;
  name: string;
  description?: string;
  category?: string;
  type: number;
  sourceType: number;
  authType: number;
  status: number;
  isLocked: boolean;
  createdAt: string;
  updatedAt?: string;
  publishedAt?: string;
  definitionJson?: string;
  authConfigJson?: string;
  toolSchemaJson?: string;
  openApiSpecJson?: string;
  apis: StudioPluginApiSummary[];
}

export interface StudioVariableCreateRequest {
  key: string;
  value?: string;
  scope: number;
  scopeId?: number;
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
  getApplication: (id: string) => Promise<StudioApplicationSummary>;
  updateApplication: (id: string, request: StudioApplicationUpdateRequest) => Promise<void>;
  deleteApplication: (id: string) => Promise<void>;
  publishApplication: (id: string, releaseNote?: string) => Promise<void>;
  getApplicationPublishRecords: (id: string) => Promise<StudioApplicationPublishRecord[]>;
  getApplicationConversationTemplates: (id: string) => Promise<StudioApplicationConversationTemplate[]>;
  createApplicationConversationTemplate: (id: string, request: StudioApplicationConversationTemplateCreateRequest) => Promise<string>;
  deleteApplicationConversationTemplate: (id: string, templateId: string) => Promise<void>;
  listVariables: (params?: { pageIndex?: number; pageSize?: number; keyword?: string; scope?: number; scopeId?: number }) => Promise<PagedResult<StudioVariableItem>>;
  createVariable: (request: StudioVariableCreateRequest) => Promise<number>;
  updateVariable: (id: number, request: StudioVariableCreateRequest) => Promise<void>;
  deleteVariable: (id: number) => Promise<void>;
  listSystemVariables: () => Promise<StudioSystemVariableDefinition[]>;
  toggleWorkspaceFavorite: (resourceType: WorkspaceIdeResourceType, resourceId: string, isFavorite: boolean) => Promise<void>;
  recordWorkspaceActivity: (request: { resourceType: WorkspaceIdeResourceType; resourceId: number; resourceTitle: string; entryRoute: string }) => Promise<void>;
  getAgentPublications: (agentId: string) => Promise<StudioAssistantPublication[]>;
  publishAgent: (agentId: string, releaseNote?: string) => Promise<StudioAssistantPublishResult>;
  regenerateAgentEmbedToken: (agentId: string) => Promise<StudioAssistantPublishResult>;
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
  listPlugins: () => Promise<Array<{ id: number; name: string; category?: string; status: number; sourceType?: number }>>;
  getPluginDetail: (pluginId: number) => Promise<StudioPluginDetail>;
  publishPlugin: (pluginId: number) => Promise<void>;
  listKnowledgeBases: () => Promise<Array<{ id: number; name: string; type: number }>>;
  getKnowledgeBase: (id: number) => Promise<StudioKnowledgeBaseDetail>;
  listDatabases: () => Promise<Array<{ id: number; name: string; botId?: number }>>;
  getDatabaseDetail: (id: number) => Promise<StudioDatabaseDetail>;
  listDatabaseRecords: (id: number, params?: { pageIndex?: number; pageSize?: number }) => Promise<PagedResult<StudioDatabaseRecordItem>>;
  createDatabaseRecord: (id: number, request: StudioDatabaseRecordUpsertRequest) => Promise<number>;
  updateDatabaseRecord: (id: number, recordId: number, request: StudioDatabaseRecordUpsertRequest) => Promise<void>;
  deleteDatabaseRecord: (id: number, recordId: number) => Promise<void>;
  validateDatabaseSchemaDraft: (schemaJson: string) => Promise<StudioDatabaseSchemaValidationResult>;
  submitDatabaseImport: (id: number, file: File) => Promise<number>;
  getDatabaseImportProgress: (id: number) => Promise<StudioDatabaseImportProgress | null>;
  downloadDatabaseTemplate: (id: number) => Promise<void>;
  /** D5：同步批量插入；受 MaxBulkInsertRows 限制（默认 1000）。可选——上层未实现时回退到逐条 createDatabaseRecord。 */
  bulkCreateDatabaseRecords?: (id: number, request: StudioDatabaseRecordBulkCreateRequest) => Promise<StudioDatabaseRecordBulkCreateResult>;
  /** D5：异步批量插入。可选——上层未实现时不暴露入口。 */
  submitDatabaseBulkInsertJob?: (id: number, request: StudioDatabaseRecordBulkCreateRequest) => Promise<StudioDatabaseBulkJobAccepted>;
  listBotVariables: (botId: string) => Promise<Array<{ id: number; key: string; scopeId?: number }>>;
  bindAgentWorkflow: (agentId: string, workflowId?: string) => Promise<WorkflowBinding>;
  runWorkflowTask: (
    workflowId: string,
    incident: string
  ) => Promise<{ execution: WorkflowExecutionSummary; trace?: WorkbenchTrace }>;
  generateAssistant: (kind: "sql" | "workflow", description: string) => Promise<{ result: string; explanation: string } | null>;
  listModelConfigs: () => Promise<PagedResult<ModelConfigItem>>;
  getModelConfig: (id: number) => Promise<ModelConfigItem>;
  getModelConfigStats: (keyword?: string) => Promise<ModelConfigStats>;
  createModelConfig: (request: ModelConfigCreateRequest) => Promise<string>;
  updateModelConfig: (id: number, request: ModelConfigUpdateRequest) => Promise<void>;
  deleteModelConfig: (id: number) => Promise<void>;
  testModelConfigConnection: (request: ModelConfigConnectionTestRequest) => Promise<ModelConfigConnectionTestResult>;
  runModelConfigPromptTest: (request: ModelConfigPromptTestRequest) => Promise<string>;
  getDashboardStats: () => Promise<DashboardStats>;
  getResourceReferences: (resourceType: string, resourceId: string) => Promise<ResourceReference[]>;
  getPublishCenterItems: (params?: { resourceType?: string }) => Promise<PublishCenterItem[]>;
  getAppBuilderConfig: (appId: string) => Promise<AppBuilderConfig>;
  updateAppBuilderConfig: (appId: string, config: AppBuilderConfig) => Promise<void>;
  runAppPreview: (appId: string, inputs: Record<string, unknown>) => Promise<{ outputs: Record<string, unknown>; trace?: WorkbenchTrace }>;
  listPromptTemplates: (params?: { keyword?: string }) => Promise<PagedResult<PromptTemplateItem>>;
}

export interface StudioPageProps {
  api: StudioModuleApi;
  locale: StudioLocale;
}

export interface PendingPublishItem {
  resourceType: "agent" | "app" | "workflow" | "plugin";
  resourceId: string;
  resourceName: string;
  updatedAt: string;
}

export interface DashboardStats {
  agentCount: number;
  appCount: number;
  workflowCount: number;
  enabledModelCount: number;
  pluginCount: number;
  knowledgeBaseCount: number;
  pendingPublishItems: PendingPublishItem[];
  recentActivities: WorkspaceIdeResource[];
}

export interface ResourceReference {
  referrerType: "agent" | "app" | "workflow";
  referrerId: string;
  referrerName: string;
  bindingField: string;
}

export interface PublishCenterItem {
  resourceType: "agent" | "app" | "workflow" | "plugin";
  resourceId: string;
  resourceName: string;
  currentVersion: number;
  draftVersion: number;
  lastPublishedAt?: string;
  status: "draft" | "published" | "outdated";
  apiEndpoint?: string;
  embedToken?: string;
}

export interface AppInputComponent {
  id: string;
  type: "text" | "textarea" | "select" | "file" | "number" | "date";
  label: string;
  variableKey: string;
  required: boolean;
  defaultValue?: string;
  options?: { label: string; value: string }[];
}

export interface AppOutputComponent {
  id: string;
  type: "text" | "markdown" | "json" | "table" | "chart";
  label: string;
  sourceExpression: string;
}

export interface AppBuilderConfig {
  inputs: AppInputComponent[];
  outputs: AppOutputComponent[];
  boundWorkflowId?: string;
  layoutMode: "form" | "chat" | "hybrid";
}

export interface PromptTemplateItem {
  id: string;
  name: string;
  description?: string;
  content: string;
  variables: string[];
}
