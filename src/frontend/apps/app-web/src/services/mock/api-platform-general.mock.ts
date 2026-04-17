import { mockResolve } from "./mock-utils";

/**
 * Mock：通用管理（PRD 02-左侧导航 7.12）。
 *
 * 路由：
 *   GET /api/v1/platform/general/notices
 *   GET /api/v1/platform/general/branding
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
  return mockResolve<PlatformNoticeItem[]>([
    {
      id: "notice-maintenance",
      title: "系统例行维护通知",
      message: "本周日 02:00-04:00 将进行例行维护，可能短暂不可用。",
      level: "info",
      publishedAt: new Date().toISOString()
    }
  ]);
}

export async function getPlatformBranding(): Promise<PlatformBranding> {
  return mockResolve({
    productName: "Atlas Coze",
    productSlogan: "你的 AI 应用开发伙伴"
  });
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
