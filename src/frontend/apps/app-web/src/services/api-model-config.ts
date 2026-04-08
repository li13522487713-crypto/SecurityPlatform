import { requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";

export interface ModelConfigDto {
  id: number;
  name: string;
  providerType: string;
  baseUrl: string;
  defaultModel: string;
  modelId: string;
  systemPrompt?: string;
  isEnabled: boolean;
  supportsEmbedding: boolean;
  enableStreaming: boolean;
  enableReasoning: boolean;
  enableTools: boolean;
  enableVision: boolean;
  enableJsonMode: boolean;
  temperature?: number;
  maxTokens?: number;
  topP?: number;
  frequencyPenalty?: number;
  presencePenalty?: number;
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

export interface ModelConfigStatsDto {
  total: number;
  enabled: number;
  disabled: number;
  embeddingCount: number;
}

export interface ModelConfigTestResult {
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

export async function getModelConfigsPaged(request: PagedRequest): Promise<PagedResult<ModelConfigDto>> {
  const query = toQuery(request);
  const response = await requestApi<ApiResponse<PagedResult<ModelConfigDto>>>(`/model-configs?${query}`);
  if (!response.data) throw new Error(response.message || "Failed to query model configs");
  return response.data;
}

export async function getModelConfigById(id: number): Promise<ModelConfigDto> {
  const response = await requestApi<ApiResponse<ModelConfigDto>>(`/model-configs/${id}`);
  if (!response.data) throw new Error(response.message || "Failed to query model config");
  return response.data;
}

export async function getModelConfigStats(keyword?: string): Promise<ModelConfigStatsDto> {
  const query = new URLSearchParams();
  if (keyword) query.set("keyword", keyword);
  const url = query.size > 0 ? `/model-configs/stats?${query.toString()}` : "/model-configs/stats";
  const response = await requestApi<ApiResponse<ModelConfigStatsDto>>(url);
  if (!response.data) throw new Error(response.message || "Failed to query stats");
  return response.data;
}

export async function createModelConfig(request: ModelConfigCreateRequest): Promise<string> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/model-configs", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data?.id) throw new Error(response.message || "Failed to create model config");
  return response.data.id;
}

export async function updateModelConfig(id: number, request: ModelConfigUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/model-configs/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "Failed to update model config");
}

export async function deleteModelConfig(id: number): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/model-configs/${id}`, {
    method: "DELETE"
  });
  if (!response.success) throw new Error(response.message || "Failed to delete model config");
}

export async function testModelConfigConnection(request: {
  modelConfigId?: number;
  providerType: string;
  apiKey: string;
  baseUrl: string;
  model: string;
}): Promise<ModelConfigTestResult> {
  const response = await requestApi<ApiResponse<ModelConfigTestResult>>(
    "/model-configs/test",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    },
    { suppressErrorMessage: true }
  );
  if (!response.data) throw new Error(response.message || "Test connection failed");
  return response.data;
}

export function createModelConfigPromptTestStream(request: ModelConfigPromptTestRequest) {
  const abortController = new AbortController();
  const token = localStorage.getItem("atlas_app_token");
  const tenantId = localStorage.getItem("atlas_app_tenant_id") || "";
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    Accept: "text/event-stream"
  };
  if (token) headers["Authorization"] = `Bearer ${token}`;
  if (tenantId) headers["X-Tenant-Id"] = tenantId;

  const baseUrl = import.meta.env.VITE_API_BASE_URL || "/api/v1";
  const fetchPromise = fetch(`${baseUrl}/model-configs/test/stream`, {
    method: "POST",
    headers,
    body: JSON.stringify(request),
    signal: abortController.signal,
    credentials: "include"
  });
  return { fetchPromise, abortController };
}
