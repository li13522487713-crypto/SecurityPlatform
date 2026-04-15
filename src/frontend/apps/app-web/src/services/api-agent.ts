import { extractResourceId, requestApi } from "./api-core";
import type { ApiResponse, PagedResult } from "@atlas/shared-react-core/types";

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
  status: string;
}

export interface AgentListItem {
  id: string;
  name: string;
  description?: string;
  avatarUrl?: string;
  status: string;
  modelName?: string;
  createdAt?: string;
  publishVersion?: number;
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
  workspaceId?: number;
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
  workspaceId?: number;
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

export type AgentKnowledgeBindingInput = AgentKnowledgeBinding;

export interface AgentDatabaseBinding {
  databaseId: number;
  alias?: string;
  accessMode: "readonly" | "readwrite";
  tableAllowlist: string[];
  isDefault: boolean;
}

export type AgentDatabaseBindingInput = AgentDatabaseBinding;

export interface AgentVariableBinding {
  variableId: number;
  alias?: string;
  isRequired: boolean;
  defaultValueOverride?: string;
}

export type AgentVariableBindingInput = AgentVariableBinding;

export interface AgentPluginBinding {
  pluginId: number;
  sortOrder: number;
  isEnabled: boolean;
  toolConfigJson?: string;
  toolBindings?: AgentPluginToolBinding[];
}

export type AgentPluginBindingInput = AgentPluginBinding;

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

export async function getAgentsPaged(params?: {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  status?: string;
}): Promise<PagedResult<AgentListItem>> {
  const pageIndex = params?.pageIndex ?? 1;
  const pageSize = params?.pageSize ?? 20;
  const query = new URLSearchParams({
    pageIndex: String(pageIndex),
    pageSize: String(pageSize),
  });

  if (params?.keyword) {
    query.set("keyword", params.keyword);
  }

  if (params?.status) {
    query.set("status", params.status);
  }

  const response = await requestApi<ApiResponse<PagedResult<AgentListItem>>>(`/agents?${query.toString()}`);
  if (!response.data) {
    throw new Error(response.message || "Failed to query agents");
  }

  return response.data;
}

export async function getAgentById(id: string): Promise<AgentDetail> {
  const response = await requestApi<ApiResponse<AgentDetail>>(`/agents/${id}`);
  if (!response.data) {
    throw new Error(response.message || "Failed to query agent");
  }
  return response.data;
}

export async function createAgent(request: AgentCreateRequest): Promise<string> {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>("/agents", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  const agentId = extractResourceId(response.data);
  if (!response.data || !agentId) {
    throw new Error(response.message || "Failed to create agent");
  }
  return agentId;
}

export async function updateAgent(id: string, request: AgentUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agents/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!response.success) {
    throw new Error(response.message || "Failed to update agent");
  }
}

export async function bindAgentWorkflow(
  id: string,
  workflowId?: string
): Promise<{ workflowId?: string; workflowName?: string }> {
  const response = await requestApi<ApiResponse<{ workflowId?: string | number; workflowName?: string }>>(
    `/draft-agents/${id}/workflow-bindings`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        workflowId: workflowId ? Number(workflowId) : null
      })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "Failed to bind workflow");
  }

  return {
    workflowId: response.data.workflowId !== undefined ? String(response.data.workflowId) : undefined,
    workflowName: response.data.workflowName
  };
}

export async function deleteAgent(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agents/${id}`, {
    method: "DELETE",
  });
  if (!response.success) {
    throw new Error(response.message || "Failed to delete agent");
  }
}
