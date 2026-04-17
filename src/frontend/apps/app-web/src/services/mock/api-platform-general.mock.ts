import type { ApiResponse } from "@atlas/shared-react-core/types";
import { requestApi } from "../api-core";
import { mockResolve } from "./mock-utils";

/**
 * 通用管理（PRD 02-7.12）。
 *
 * - 平台公告与品牌：已切换为真实 REST（PlatformGeneralController）。
 * - OpenAPI 密钥：仍为 mock，等 M1 后续 milestone 把
 *   `OpenApiKeysController` 落地后切换。
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
}

const API_KEYS: OpenApiKeyItem[] = [];

export async function listOpenApiKeys(): Promise<OpenApiKeyItem[]> {
  return mockResolve<OpenApiKeyItem[]>(API_KEYS.slice());
}

export async function createOpenApiKey(alias: string): Promise<{ key: string; item: OpenApiKeyItem }> {
  const item: OpenApiKeyItem = {
    id: `key-${Date.now()}`,
    alias: alias.trim(),
    prefix: `pat_${Math.random().toString(36).slice(2, 10)}`,
    scopes: ["agent:read", "workflow:run"],
    createdAt: new Date().toISOString()
  };
  API_KEYS.push(item);
  return mockResolve({
    key: `${item.prefix}.${Math.random().toString(36).slice(2, 18)}`,
    item
  });
}

export async function deleteOpenApiKey(keyId: string): Promise<void> {
  const index = API_KEYS.findIndex(item => item.id === keyId);
  if (index >= 0) {
    API_KEYS.splice(index, 1);
  }
  return mockResolve(undefined);
}
