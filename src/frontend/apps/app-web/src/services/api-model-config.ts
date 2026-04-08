import { requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";

export interface ModelConfigDto {
  id: number;
  name: string;
  providerType: string;
  baseUrl: string;
  defaultModel: string;
  isEnabled: boolean;
  supportsEmbedding: boolean;
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
}

export interface ModelConfigUpdateRequest {
  name: string;
  apiKey: string;
  baseUrl: string;
  defaultModel: string;
  isEnabled: boolean;
  supportsEmbedding: boolean;
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

export interface ModelConfigUiPreferences {
  enableStreamingTypewriter?: boolean;
}

const MODEL_CONFIG_UI_PREFS_KEY = "atlas_model_config_ui_prefs";

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

export function getModelConfigUiPreferences(modelConfigId: string | number | null | undefined): ModelConfigUiPreferences {
  if (modelConfigId === null || modelConfigId === undefined) {
    return {};
  }

  try {
    const raw = localStorage.getItem(MODEL_CONFIG_UI_PREFS_KEY);
    if (!raw) {
      return {};
    }

    const parsed = JSON.parse(raw) as Record<string, ModelConfigUiPreferences>;
    return parsed[String(modelConfigId)] ?? {};
  } catch {
    return {};
  }
}

export function setModelConfigUiPreferences(
  modelConfigId: string | number | null | undefined,
  preferences: ModelConfigUiPreferences
): void {
  if (modelConfigId === null || modelConfigId === undefined) {
    return;
  }

  try {
    const raw = localStorage.getItem(MODEL_CONFIG_UI_PREFS_KEY);
    const parsed = raw ? (JSON.parse(raw) as Record<string, ModelConfigUiPreferences>) : {};
    parsed[String(modelConfigId)] = {
      ...parsed[String(modelConfigId)],
      ...preferences
    };
    localStorage.setItem(MODEL_CONFIG_UI_PREFS_KEY, JSON.stringify(parsed));
  } catch {
    // ignore storage write errors
  }
}
