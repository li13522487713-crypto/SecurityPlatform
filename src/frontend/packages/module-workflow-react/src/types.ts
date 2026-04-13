import type { PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import type { WorkflowApiClient } from "@atlas/workflow-core-react";

export type WorkflowResourceMode = "workflow" | "chatflow";
export type WorkflowCreateSource = "blank" | "template" | "duplicate";
export type WorkflowStatusFilter = "all" | "draft" | "published";
export type ResourceIdeGroup = "workflow" | "plugin" | "data" | "settings";
export type ResourceIdeLibraryType = "workflow" | "plugin" | "knowledge-base" | "database";
export type ResourceIdeType =
  | "workflow"
  | "chatflow"
  | "plugin"
  | "knowledge-base"
  | "database"
  | "variables"
  | "conversations";
export type ResourceIdeTabKind =
  | "workflow-editor"
  | "chatflow-editor"
  | "plugin"
  | "knowledge"
  | "database"
  | "variables"
  | "conversations"
  | "problems"
  | "trace-list"
  | "trace-step"
  | "references";
export type AiVariableScope = 0 | 1 | 2;
export type KnowledgeBaseType = 0 | 1 | 2;
export type AiPluginType = 0 | 1;
export type AiPluginStatus = 0 | 1;
export type AiPluginSourceType = 0 | 1 | 2;
export type AiPluginAuthType = 0 | 1 | 2 | 3 | 4;

export interface WorkflowTemplateSummary {
  id: string;
  title: string;
  description: string;
  mode: WorkflowResourceMode;
  createSource: WorkflowCreateSource;
  accentColor?: string;
  badge?: string;
}

export interface WorkflowListQuery {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  mode?: WorkflowResourceMode;
  status?: WorkflowStatusFilter;
}

export interface WorkflowListItem {
  id: string;
  name: string;
  description?: string;
  code?: string;
  updatedAt?: string;
  createdAt?: string;
  publishedAt?: string;
  mode?: 0 | 1;
  status?: 0 | 1 | 2;
  latestVersionNumber?: number;
}

export interface WorkflowCreateRequest {
  name: string;
  description?: string;
  mode: WorkflowResourceMode;
  createSource: WorkflowCreateSource;
  templateId?: string;
}

export interface ResourceIdeItem {
  id: string;
  resourceType: ResourceIdeType;
  name: string;
  description?: string;
  icon?: string;
  active?: boolean;
  status?: string;
  badge?: string;
  updatedAt?: string;
  canRename?: boolean;
  canDelete?: boolean;
  libraryState?: "local" | "library";
  mode?: WorkflowResourceMode;
}

export interface ResourceIdeGroupSection {
  key: ResourceIdeGroup;
  title: string;
  items: ResourceIdeItem[];
  emptyText?: string;
}

export interface ResourceIdeTab {
  key: string;
  kind: ResourceIdeTabKind;
  resourceId?: string;
  title: string;
  closable: boolean;
  mode?: WorkflowResourceMode;
}

export interface WorkflowProblemItem {
  key: string;
  level: "canvas" | "node" | "resource";
  label: string;
  nodeKey?: string;
  resourceType?: string;
  resourceId?: string;
  sourceNodeKeys?: string[];
}

export interface WorkflowTraceStepSummary {
  timestamp: string;
  nodeKey: string;
  status: "running" | "success" | "failed" | "skipped" | "blocked";
  detail?: string;
}

export interface ResourceIdeLibraryItem {
  resourceType: ResourceIdeLibraryType;
  resourceId: number;
  name: string;
  description?: string;
  updatedAt: string;
  path: string;
  resourceSubType?: string;
  status?: string;
}

export interface ResourceIdeLibraryImportRequest {
  resourceType: ResourceIdeLibraryType;
  libraryItemId: number;
  targetAppId?: number;
  targetWorkspaceId?: number;
}

export interface ResourceIdeLibraryMutationRequest {
  resourceType: ResourceIdeLibraryType;
  resourceId: number;
}

export interface ResourceIdeLibraryMutationResult {
  resourceId: number;
  resourceType: ResourceIdeLibraryType;
  libraryItemId: number;
}

export interface WorkflowDependencyItem {
  resourceType: string;
  resourceId: string;
  name: string;
  description?: string;
  sourceNodeKeys?: string[];
}

export interface WorkflowDependencies {
  workflowId: string;
  subWorkflows: WorkflowDependencyItem[];
  plugins: WorkflowDependencyItem[];
  knowledgeBases: WorkflowDependencyItem[];
  databases: WorkflowDependencyItem[];
  variables: WorkflowDependencyItem[];
  conversations: WorkflowDependencyItem[];
}

export interface AiPluginApiItem {
  id: number;
  pluginId: number;
  name: string;
  description?: string;
  method: string;
  path: string;
  requestSchemaJson: string;
  responseSchemaJson: string;
  timeoutSeconds: number;
  isEnabled: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface AiPluginListItem {
  id: number;
  name: string;
  description?: string;
  icon?: string;
  category?: string;
  type: AiPluginType;
  sourceType: AiPluginSourceType;
  authType: AiPluginAuthType;
  status: AiPluginStatus;
  isLocked: boolean;
  createdAt: string;
  updatedAt?: string;
  publishedAt?: string;
}

export interface AiPluginDetail extends AiPluginListItem {
  definitionJson: string;
  authConfigJson: string;
  toolSchemaJson: string;
  openApiSpecJson: string;
  apis: AiPluginApiItem[];
}

export interface AiPluginMutationRequest {
  name: string;
  description?: string;
  icon?: string;
  category?: string;
  type: AiPluginType;
  definitionJson?: string;
  sourceType: AiPluginSourceType;
  authType: AiPluginAuthType;
  authConfigJson?: string;
  toolSchemaJson?: string;
  openApiSpecJson?: string;
}

export interface KnowledgeBaseListItem {
  id: number;
  name: string;
  description?: string;
  type: KnowledgeBaseType;
  documentCount: number;
  chunkCount: number;
  createdAt: string;
}

export interface KnowledgeBaseMutationRequest {
  name: string;
  description?: string;
  type: KnowledgeBaseType;
}

export interface AiDatabaseListItem {
  id: number;
  name: string;
  description?: string;
  botId?: number;
  recordCount: number;
  createdAt: string;
  updatedAt?: string;
}

export interface AiDatabaseDetail extends AiDatabaseListItem {
  tableSchema: string;
}

export interface AiDatabaseMutationRequest {
  name: string;
  description?: string;
  botId?: number;
  tableSchema: string;
}

export interface AiDatabaseSchemaValidateResult {
  isValid: boolean;
  errors: string[];
}

export interface AiVariableListItem {
  id: number;
  key: string;
  value?: string;
  scope: AiVariableScope;
  scopeId?: number;
  createdAt: string;
  updatedAt?: string;
}

export interface AiVariableMutationRequest {
  key: string;
  value?: string;
  scope: AiVariableScope;
  scopeId?: number;
}

export interface AiSystemVariableDefinition {
  key: string;
  name: string;
  description: string;
  defaultValue?: string;
}

export interface ConversationListItem {
  id: string;
  agentId: string;
  userId: string;
  title?: string;
  createdAt: string;
  lastMessageAt?: string;
  messageCount: number;
}

export interface ConversationCreateRequest {
  agentId: string;
  title?: string;
}

export interface ConversationMessageItem {
  id: string;
  role: "system" | "user" | "assistant" | "tool";
  content: string;
  metadata?: string;
  createdAt: string;
  isContextCleared: boolean;
}

export interface AgentSummaryItem {
  id: string;
  name: string;
  description?: string;
}

export interface WorkflowModuleApi {
  listWorkflows: (query?: WorkflowListQuery) => Promise<PagedResult<WorkflowListItem>>;
  listTemplates: (mode: WorkflowResourceMode) => Promise<WorkflowTemplateSummary[]>;
  createWorkflow: (request: WorkflowCreateRequest) => Promise<string>;
  duplicateWorkflow: (id: string) => Promise<string>;
  deleteWorkflow: (id: string) => Promise<void>;
  getVersions: (id: string) => Promise<Array<{ id: string; versionNumber: number; publishedAt?: string }>>;
  getDependencies: (id: string) => Promise<WorkflowDependencies>;
  listLibrary: (request: PagedRequest, resourceType?: ResourceIdeLibraryType) => Promise<PagedResult<ResourceIdeLibraryItem>>;
  importLibraryItem: (request: ResourceIdeLibraryImportRequest) => Promise<ResourceIdeLibraryMutationResult>;
  exportLibraryItem: (request: ResourceIdeLibraryMutationRequest) => Promise<ResourceIdeLibraryMutationResult>;
  moveLibraryItem: (request: ResourceIdeLibraryMutationRequest) => Promise<ResourceIdeLibraryMutationResult>;
  listPlugins: (request: PagedRequest, keyword?: string) => Promise<PagedResult<AiPluginListItem>>;
  getPluginDetail: (id: number) => Promise<AiPluginDetail>;
  createPlugin: (request: AiPluginMutationRequest) => Promise<number>;
  updatePlugin: (id: number, request: AiPluginMutationRequest) => Promise<void>;
  deletePlugin: (id: number) => Promise<void>;
  publishPlugin: (id: number) => Promise<void>;
  listKnowledgeBases: (request: PagedRequest, keyword?: string) => Promise<PagedResult<KnowledgeBaseListItem>>;
  getKnowledgeBase: (id: number) => Promise<KnowledgeBaseListItem>;
  createKnowledgeBase: (request: KnowledgeBaseMutationRequest) => Promise<number>;
  updateKnowledgeBase: (id: number, request: KnowledgeBaseMutationRequest) => Promise<void>;
  deleteKnowledgeBase: (id: number) => Promise<void>;
  listDatabases: (request: PagedRequest, keyword?: string) => Promise<PagedResult<AiDatabaseListItem>>;
  getDatabaseDetail: (id: number) => Promise<AiDatabaseDetail>;
  createDatabase: (request: AiDatabaseMutationRequest) => Promise<number>;
  updateDatabase: (id: number, request: AiDatabaseMutationRequest) => Promise<void>;
  deleteDatabase: (id: number) => Promise<void>;
  validateDatabaseSchema: (schemaJson: string) => Promise<AiDatabaseSchemaValidateResult>;
  listVariables: (
    request: PagedRequest,
    filters?: { keyword?: string; scope?: AiVariableScope; scopeId?: number }
  ) => Promise<PagedResult<AiVariableListItem>>;
  createVariable: (request: AiVariableMutationRequest) => Promise<number>;
  updateVariable: (id: number, request: AiVariableMutationRequest) => Promise<void>;
  deleteVariable: (id: number) => Promise<void>;
  listSystemVariables: () => Promise<AiSystemVariableDefinition[]>;
  listConversations: (request: PagedRequest) => Promise<PagedResult<ConversationListItem>>;
  createConversation: (request: ConversationCreateRequest) => Promise<string>;
  deleteConversation: (id: string) => Promise<void>;
  clearConversationContext: (id: string) => Promise<void>;
  clearConversationHistory: (id: string) => Promise<void>;
  listConversationMessages: (conversationId: string) => Promise<ConversationMessageItem[]>;
  appendConversationMessage: (
    conversationId: string,
    request: { role: "system" | "user" | "assistant" | "tool"; content: string; metadata?: string }
  ) => Promise<string>;
  listAgents: (request: PagedRequest, keyword?: string) => Promise<PagedResult<AgentSummaryItem>>;
  apiClient: WorkflowApiClient;
}

export interface WorkflowPageProps {
  api: WorkflowModuleApi;
  locale: "zh-CN" | "en-US";
}
