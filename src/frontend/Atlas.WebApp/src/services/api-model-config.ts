import { API_BASE, requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import { getAccessToken, getAntiforgeryToken, getTenantId } from "@/utils/auth";

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

export interface ModelConfigTestRequest {
  providerType: string;
  apiKey: string;
  baseUrl: string;
  model: string;
}

export interface ModelConfigPromptTestRequest extends ModelConfigTestRequest {
  prompt: string;
  enableReasoning: boolean;
  enableTools: boolean;
}

export interface ModelConfigTestResult {
  success: boolean;
  errorMessage?: string;
  latencyMs?: number;
}

export interface ModelConfigStatsDto {
  total: number;
  enabled: number;
  disabled: number;
  embeddingCount: number;
}

export async function getModelConfigsPaged(request: PagedRequest) {
  const query = toQuery(request);
  const response = await requestApi<ApiResponse<PagedResult<ModelConfigDto>>>(`/model-configs?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询模型配置失败");
  }
  return response.data;
}

export async function getModelConfigById(id: number) {
  const response = await requestApi<ApiResponse<ModelConfigDto>>(`/model-configs/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询模型配置失败");
  }
  return response.data;
}

export async function getEnabledModelConfigs() {
  const response = await requestApi<ApiResponse<ModelConfigDto[]>>("/model-configs/enabled");
  if (!response.data) {
    throw new Error(response.message || "查询启用模型配置失败");
  }
  return response.data;
}

export async function getModelConfigStats(keyword?: string) {
  const query = new URLSearchParams();
  if (keyword) {
    query.set("keyword", keyword);
  }
  const url = query.size > 0 ? `/model-configs/stats?${query.toString()}` : "/model-configs/stats";
  const response = await requestApi<ApiResponse<ModelConfigStatsDto>>(url);
  if (!response.data) {
    throw new Error(response.message || "查询模型配置统计失败");
  }
  return response.data;
}

export async function createModelConfig(request: ModelConfigCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/model-configs", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建模型配置失败");
  }
}

export async function updateModelConfig(id: number, request: ModelConfigUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/model-configs/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新模型配置失败");
  }
}

export async function deleteModelConfig(id: number) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/model-configs/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除模型配置失败");
  }
}

export async function testModelConfigConnection(request: ModelConfigTestRequest) {
  const response = await requestApi<ApiResponse<ModelConfigTestResult>>("/model-configs/test", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  }, {
    suppressErrorMessage: true
  });
  if (!response.data) {
    throw new Error(response.message || "测试连接失败");
  }
  return response.data;
}

export function createModelConfigPromptTestStream(request: ModelConfigPromptTestRequest) {
  const abortController = new AbortController();
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    Accept: "text/event-stream",
    "Idempotency-Key": generateIdempotencyKey()
  };

  const accessToken = getAccessToken();
  if (accessToken) {
    headers["Authorization"] = `Bearer ${accessToken}`;
  }

  const tenantId = getTenantId();
  if (tenantId) {
    headers["X-Tenant-Id"] = tenantId;
  }

  const csrfToken = getAntiforgeryToken();
  if (csrfToken) {
    headers["X-CSRF-TOKEN"] = csrfToken;
  }

  const fetchPromise = fetch(`${API_BASE}/model-configs/test/stream`, {
    method: "POST",
    headers,
    body: JSON.stringify(request),
    signal: abortController.signal,
    credentials: "include"
  });

  return { fetchPromise, abortController };
}

function generateIdempotencyKey() {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return `idem-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}
