import type { ApiResponse } from "@atlas/shared-react-core/types";
import { requestApi } from "../api-core";

/**
 * 通用管理（PRD 02-7.12） + API 管理（PRD 02-7.10）。
 *
 * - 平台公告与品牌：真实 REST（PlatformGeneralController）。
 * - OpenAPI 密钥：M4.1 已切换为真实 REST（OpenApiKeysController），
 *   底层复用 IPersonalAccessTokenService（PAT 服务）。
 */

export interface PlatformNoticeItem {
  id: string;
  title: string;
  message: string;
  level: "info" | "warning" | "error";
  publishedAt: string;
}

export interface PlatformBranding {
  logoUrl?: string;
  productName: string;
  productSlogan: string;
}

export async function listPlatformNotices(): Promise<PlatformNoticeItem[]> {
  const response = await requestApi<ApiResponse<PlatformNoticeItem[]>>("/platform/general/notices");
  return response.data ?? [];
}

export async function getPlatformBranding(): Promise<PlatformBranding> {
  const response = await requestApi<ApiResponse<PlatformBranding>>("/platform/general/branding");
  if (!response.data) {
    throw new Error(response.message || "Failed to load branding");
  }
  return response.data;
}

export interface OpenApiKeyItem {
  id: string;
  alias: string;
  prefix: string;
  scopes: string[];
  createdAt: string;
  lastUsedAt?: string;
  expiresAt?: string;
}

interface OpenApiKeyCreateResponse {
  key: string;
  item: OpenApiKeyItem;
}

export async function listOpenApiKeys(): Promise<OpenApiKeyItem[]> {
  const response = await requestApi<ApiResponse<OpenApiKeyItem[]>>("/open/api-keys");
  return response.data ?? [];
}

export async function createOpenApiKey(alias: string): Promise<{ key: string; item: OpenApiKeyItem }> {
  const response = await requestApi<ApiResponse<OpenApiKeyCreateResponse>>("/open/api-keys", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ alias })
  });
  if (!response.data?.key || !response.data.item) {
    throw new Error(response.message || "Failed to create API key");
  }
  return response.data;
}

export async function deleteOpenApiKey(keyId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>(
    `/open/api-keys/${encodeURIComponent(keyId)}`,
    { method: "DELETE" }
  );
  if (!response.success) {
    throw new Error(response.message || "Failed to delete API key");
  }
}
