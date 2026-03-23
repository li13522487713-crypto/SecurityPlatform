import { requestApi, requestApiBlob, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";

export interface OpenApiProjectListItem {
  id: number;
  name: string;
  description: string;
  appId: string;
  secretPrefix: string;
  scopes: string[];
  isActive: boolean;
  expiresAt?: string;
  lastUsedAt?: string;
  createdByUserId: number;
  createdAt: string;
  updatedAt: string;
}

export interface OpenApiProjectCreateResult {
  id: number;
  name: string;
  appId: string;
  appSecret: string;
  secretPrefix: string;
  scopes: string[];
  expiresAt?: string;
}

export interface OpenApiProjectRotateSecretResult {
  id: number;
  appId: string;
  appSecret: string;
  secretPrefix: string;
}

export interface OpenApiProjectTokenExchangeResult {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  projectId: number;
  appId: string;
  scopes: string[];
}

export interface OpenApiCallStatsSummary {
  projectId?: number;
  fromUtc?: string;
  toUtc?: string;
  totalCalls: number;
  successCalls: number;
  failedCalls: number;
  successRate: number;
  averageDurationMs: number;
  maxDurationMs: number;
}

export interface WebhookSubscription {
  id: number;
  name: string;
  eventTypes: string;
  targetUrl: string;
  secret: string;
  headers?: string;
  isActive: boolean;
  createdAt: string;
  lastTriggeredAt?: string;
}

export interface WebhookDeliveryLog {
  id: number;
  subscriptionId: number;
  eventType: string;
  payload: string;
  responseCode?: number;
  responseBody?: string;
  durationMs: number;
  success: boolean;
  errorMessage?: string;
  createdAt: string;
}

interface IdPayload {
  id: string;
}

function ensureData<T>(response: ApiResponse<T>, fallbackMessage: string): T {
  if (!response.data) {
    throw new Error(response.message || fallbackMessage);
  }
  return response.data;
}

export async function getOpenApiProjectsPaged(request: PagedRequest) {
  const query = toQuery(request);
  const response = await requestApi<ApiResponse<PagedResult<OpenApiProjectListItem>>>(`/open-api-projects?${query}`);
  return ensureData(response, "查询开放应用失败");
}

export async function createOpenApiProject(request: {
  name: string;
  description?: string;
  scopes: string[];
  expiresAt?: string | null;
}) {
  const response = await requestApi<ApiResponse<OpenApiProjectCreateResult>>(
    "/open-api-projects",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  return ensureData(response, "创建开放应用失败");
}

export async function updateOpenApiProject(
  id: number,
  request: {
    name: string;
    description?: string;
    scopes: string[];
    isActive: boolean;
    expiresAt?: string | null;
  }
) {
  const response = await requestApi<ApiResponse<IdPayload>>(
    `/open-api-projects/${id}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  ensureData(response, "更新开放应用失败");
}

export async function rotateOpenApiProjectSecret(id: number) {
  const response = await requestApi<ApiResponse<OpenApiProjectRotateSecretResult>>(
    `/open-api-projects/${id}/rotate-secret`,
    {
      method: "POST"
    }
  );
  return ensureData(response, "轮换密钥失败");
}

export async function deleteOpenApiProject(id: number) {
  const response = await requestApi<ApiResponse<IdPayload>>(
    `/open-api-projects/${id}`,
    { method: "DELETE" }
  );
  ensureData(response, "删除开放应用失败");
}

export async function exchangeOpenApiProjectToken(appId: string, appSecret: string) {
  const response = await requestApi<ApiResponse<OpenApiProjectTokenExchangeResult>>(
    "/open-api-projects/token",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ appId, appSecret })
    }
  );
  return ensureData(response, "交换访问令牌失败");
}

export async function getOpenApiStatsSummary(params: {
  projectId?: number;
  fromUtc?: string;
  toUtc?: string;
}) {
  const query = new URLSearchParams();
  if (params.projectId) {
    query.set("projectId", String(params.projectId));
  }
  if (params.fromUtc) {
    query.set("fromUtc", params.fromUtc);
  }
  if (params.toUtc) {
    query.set("toUtc", params.toUtc);
  }

  const response = await requestApi<ApiResponse<OpenApiCallStatsSummary>>(
    `/open-api-stats/summary?${query.toString()}`
  );
  return ensureData(response, "查询开放接口统计失败");
}

export async function listOpenApiWebhooks() {
  const response = await requestApi<ApiResponse<WebhookSubscription[]>>("/open-api-webhooks");
  return ensureData(response, "查询Webhook订阅失败");
}

export async function createOpenApiWebhook(request: {
  name: string;
  eventTypes: string[];
  targetUrl: string;
  secret: string;
  headers?: Record<string, string> | null;
}) {
  const response = await requestApi<ApiResponse<IdPayload>>(
    "/open-api-webhooks",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  return Number(ensureData(response, "创建Webhook订阅失败").id);
}

export async function deleteOpenApiWebhook(id: number) {
  const response = await requestApi<ApiResponse<IdPayload>>(
    `/open-api-webhooks/${id}`,
    { method: "DELETE" }
  );
  ensureData(response, "删除Webhook订阅失败");
}

export async function testOpenApiWebhook(id: number) {
  const response = await requestApi<ApiResponse<IdPayload>>(
    `/open-api-webhooks/${id}/test`,
    { method: "POST" }
  );
  ensureData(response, "测试Webhook订阅失败");
}

export async function getOpenApiWebhookDeliveries(id: number, pageSize = 20) {
  const response = await requestApi<ApiResponse<WebhookDeliveryLog[]>>(
    `/open-api-webhooks/${id}/deliveries?pageSize=${pageSize}`
  );
  return ensureData(response, "查询Webhook投递记录失败");
}

export async function downloadOpenApiSdk(language: "typescript" | "csharp") {
  return requestApiBlob(`/open-api-sdk/download?language=${language}`, { method: "GET" });
}

export async function downloadOpenApiSpec() {
  return requestApiBlob("/open-api-sdk/openapi.json", { method: "GET" });
}
