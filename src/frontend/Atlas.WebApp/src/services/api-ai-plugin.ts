import { requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";

export type AiPluginType = 0 | 1;
export type AiPluginStatus = 0 | 1;
export type AiPluginSourceType = 0 | 1 | 2;
export type AiPluginAuthType = 0 | 1 | 2 | 3 | 4;

export interface AiPluginListItem {
  id: number;
  name: string;
  description?: string;
  icon?: string;
  category?: string;
  type: AiPluginType;
  sourceType: AiPluginSourceType;
  authType: AiPluginAuthType;
  authConfigJson: string;
  toolSchemaJson: string;
  openApiSpecJson: string;
  status: AiPluginStatus;
  isLocked: boolean;
  createdAt: string;
  updatedAt?: string;
  publishedAt?: string;
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

export interface AiPluginDetail extends AiPluginListItem {
  definitionJson: string;
  apis: AiPluginApiItem[];
}

export interface AiPluginCreateRequest {
  name: string;
  description?: string;
  icon?: string;
  category?: string;
  type: AiPluginType;
  sourceType: AiPluginSourceType;
  authType: AiPluginAuthType;
  authConfigJson?: string;
  toolSchemaJson?: string;
  openApiSpecJson?: string;
  definitionJson?: string;
}

export interface AiPluginUpdateRequest extends AiPluginCreateRequest {}

export interface AiPluginDebugRequest {
  apiId?: number;
  inputJson?: string;
}

export interface AiPluginDebugResult {
  success: boolean;
  outputJson: string;
  errorMessage?: string;
  durationMs: number;
}

export interface AiPluginOpenApiImportRequest {
  openApiJson: string;
  overwrite: boolean;
}

export interface AiPluginOpenApiImportResult {
  importedCount: number;
  importedApiNames: string[];
}

export interface AiPluginApiCreateRequest {
  name: string;
  description?: string;
  method: string;
  path: string;
  requestSchemaJson?: string;
  responseSchemaJson?: string;
  timeoutSeconds: number;
}

export interface AiPluginApiUpdateRequest extends AiPluginApiCreateRequest {
  isEnabled: boolean;
}

export interface AiPluginBuiltInMetaItem {
  code: string;
  name: string;
  description: string;
  category: string;
  version: string;
  tags: string[];
}

export async function getAiPluginsPaged(request: PagedRequest, keyword?: string) {
  const query = toQuery(request, { keyword });
  const response = await requestApi<ApiResponse<PagedResult<AiPluginListItem>>>(`/ai-plugins?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询插件列表失败");
  }

  return response.data;
}

export async function getAiPluginById(id: number) {
  const response = await requestApi<ApiResponse<AiPluginDetail>>(`/ai-plugins/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询插件详情失败");
  }

  return response.data;
}

export async function createAiPlugin(request: AiPluginCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/ai-plugins", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "创建插件失败");
  }

  return Number(response.data.id);
}

export async function updateAiPlugin(id: number, request: AiPluginUpdateRequest) {
  const response = await requestApi<ApiResponse<object>>(`/ai-plugins/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新插件失败");
  }
}

export async function deleteAiPlugin(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-plugins/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除插件失败");
  }
}

export async function publishAiPlugin(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-plugins/${id}/publish`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "发布插件失败");
  }
}

export async function setAiPluginLock(id: number, isLocked: boolean) {
  const response = await requestApi<ApiResponse<object>>(`/ai-plugins/${id}/lock`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ isLocked })
  });
  if (!response.success) {
    throw new Error(response.message || "更新插件锁状态失败");
  }
}

export async function debugAiPlugin(id: number, request: AiPluginDebugRequest) {
  const response = await requestApi<ApiResponse<AiPluginDebugResult>>(`/ai-plugins/${id}/debug`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "插件调试失败");
  }

  return response.data;
}

export async function importAiPluginOpenApi(id: number, request: AiPluginOpenApiImportRequest) {
  const response = await requestApi<ApiResponse<AiPluginOpenApiImportResult>>(`/ai-plugins/${id}/import/openapi`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "OpenAPI 导入失败");
  }

  return response.data;
}

export async function getAiPluginApis(id: number) {
  const response = await requestApi<ApiResponse<AiPluginApiItem[]>>(`/ai-plugins/${id}/apis`);
  if (!response.data) {
    throw new Error(response.message || "查询插件接口失败");
  }

  return response.data;
}

export async function createAiPluginApi(pluginId: number, request: AiPluginApiCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/ai-plugins/${pluginId}/apis`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "创建插件接口失败");
  }

  return Number(response.data.id);
}

export async function updateAiPluginApi(pluginId: number, apiId: number, request: AiPluginApiUpdateRequest) {
  const response = await requestApi<ApiResponse<object>>(`/ai-plugins/${pluginId}/apis/${apiId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新插件接口失败");
  }
}

export async function deleteAiPluginApi(pluginId: number, apiId: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-plugins/${pluginId}/apis/${apiId}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除插件接口失败");
  }
}

export async function getAiPluginBuiltInMetadata() {
  const response = await requestApi<ApiResponse<AiPluginBuiltInMetaItem[]>>("/ai-plugins/built-in-metadata");
  if (!response.data) {
    throw new Error(response.message || "查询内置插件元数据失败");
  }

  return response.data;
}
