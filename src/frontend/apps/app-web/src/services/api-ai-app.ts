import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi, toQuery } from "./api-core";

export interface AiAppListItem {
  id: string;
  name: string;
  description?: string;
  icon?: string;
  agentId?: string;
  workflowId?: string;
  promptTemplateId?: string;
  status: string;
  publishVersion: number;
  createdAt: string;
  updatedAt?: string;
  publishedAt?: string;
}

export interface AiAppDetail extends AiAppListItem {
  publishRecords: AiAppPublishRecordItem[];
}

export interface AiAppCreateRequest {
  name: string;
  description?: string;
  icon?: string;
  agentId?: string;
  workflowId?: string;
  promptTemplateId?: string;
}

export interface AiAppUpdateRequest {
  name: string;
  description?: string;
  icon?: string;
  agentId?: string;
  workflowId?: string;
  promptTemplateId?: string;
}

export interface AiAppPublishRequest {
  releaseNote?: string;
}

export interface AiAppPublishRecordItem {
  id: string;
  appId: string;
  version: string;
  releaseNote?: string;
  publishedByUserId: string;
  createdAt: string;
}

export interface AiAppVersionCheckResult {
  appId: string;
  currentPublishVersion: number;
  latestVersion?: string;
  latestPublishedAt?: string;
}

export interface AiAppConversationTemplateListItem {
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

export interface AiAppConversationTemplateCreateRequest {
  name: string;
  createMethod: string;
  sourceWorkflowId?: string;
  connectorId?: string;
  isDefault?: boolean;
  configJson?: string;
}

export interface AiAppConversationTemplateUpdateRequest {
  name: string;
  sourceWorkflowId?: string;
  connectorId?: string;
  isDefault?: boolean;
  configJson?: string;
}

export async function getAiAppsPaged(request: PagedRequest): Promise<PagedResult<AiAppListItem>> {
  const response = await requestApi<ApiResponse<PagedResult<AiAppListItem>>>(`/ai-apps?${toQuery(request)}`);
  if (!response.data) {
    throw new Error(response.message || "查询应用失败");
  }
  return response.data;
}

export async function getAiAppById(id: string): Promise<AiAppDetail> {
  const response = await requestApi<ApiResponse<AiAppDetail>>(`/ai-apps/${encodeURIComponent(id)}`);
  if (!response.data) {
    throw new Error(response.message || "查询应用详情失败");
  }
  return response.data;
}

export async function createAiApp(request: AiAppCreateRequest): Promise<string> {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>("/ai-apps", {
    method: "POST",
    body: JSON.stringify({
      ...request,
      agentId: request.agentId ? Number(request.agentId) : null,
      workflowId: request.workflowId ? Number(request.workflowId) : null,
      promptTemplateId: request.promptTemplateId ? Number(request.promptTemplateId) : null
    })
  });
  const id = response.data?.id ?? response.data?.Id;
  if (!response.success || id === undefined || id === null) {
    throw new Error(response.message || "创建应用失败");
  }
  return String(id);
}

export async function updateAiApp(id: string, request: AiAppUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-apps/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify({
      ...request,
      agentId: request.agentId ? Number(request.agentId) : null,
      workflowId: request.workflowId ? Number(request.workflowId) : null,
      promptTemplateId: request.promptTemplateId ? Number(request.promptTemplateId) : null
    })
  });
  if (!response.success) {
    throw new Error(response.message || "更新应用失败");
  }
}

export async function deleteAiApp(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-apps/${encodeURIComponent(id)}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除应用失败");
  }
}

export async function publishAiApp(id: string, request: AiAppPublishRequest = {}): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/ai-apps/${encodeURIComponent(id)}/publish`, {
    method: "POST",
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "发布应用失败");
  }
}

export async function getAiAppVersionCheck(id: string): Promise<AiAppVersionCheckResult> {
  const response = await requestApi<ApiResponse<AiAppVersionCheckResult>>(`/ai-apps/${encodeURIComponent(id)}/version-check`);
  if (!response.data) {
    throw new Error(response.message || "查询应用版本状态失败");
  }
  return response.data;
}

export async function getAiAppPublishRecords(id: string, top = 20): Promise<AiAppPublishRecordItem[]> {
  const response = await requestApi<ApiResponse<AiAppPublishRecordItem[]>>(
    `/ai-apps/${encodeURIComponent(id)}/publish-records?${toQuery({ pageIndex: 1, pageSize: top })}&top=${top}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询应用发布记录失败");
  }
  return response.data;
}

export async function getAiAppConversationTemplates(id: string): Promise<AiAppConversationTemplateListItem[]> {
  const response = await requestApi<ApiResponse<AiAppConversationTemplateListItem[]>>(
    `/ai-apps/${encodeURIComponent(id)}/conversation-templates`
  );
  if (!response.data) {
    throw new Error(response.message || "查询应用会话模板失败");
  }
  return response.data;
}

export async function createAiAppConversationTemplate(
  id: string,
  request: AiAppConversationTemplateCreateRequest
): Promise<string> {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>(
    `/ai-apps/${encodeURIComponent(id)}/conversation-templates`,
    {
      method: "POST",
      body: JSON.stringify({
        ...request,
        sourceWorkflowId: request.sourceWorkflowId ? Number(request.sourceWorkflowId) : null,
        connectorId: request.connectorId ? Number(request.connectorId) : null
      })
    }
  );
  const templateId = response.data?.id ?? response.data?.Id;
  if (!response.success || templateId === undefined || templateId === null) {
    throw new Error(response.message || "创建应用会话模板失败");
  }
  return String(templateId);
}

export async function updateAiAppConversationTemplate(
  id: string,
  templateId: string,
  request: AiAppConversationTemplateUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `/ai-apps/${encodeURIComponent(id)}/conversation-templates/${encodeURIComponent(templateId)}`,
    {
      method: "PUT",
      body: JSON.stringify({
        ...request,
        sourceWorkflowId: request.sourceWorkflowId ? Number(request.sourceWorkflowId) : null,
        connectorId: request.connectorId ? Number(request.connectorId) : null
      })
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新应用会话模板失败");
  }
}

export async function deleteAiAppConversationTemplate(id: string, templateId: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `/ai-apps/${encodeURIComponent(id)}/conversation-templates/${encodeURIComponent(templateId)}`,
    {
      method: "DELETE"
    }
  );
  if (!response.success) {
    throw new Error(response.message || "删除应用会话模板失败");
  }
}
