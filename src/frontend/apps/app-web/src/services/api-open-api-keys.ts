import type { ApiResponse } from "@atlas/shared-react-core/types";
import { requestApi } from "./api-core";

export interface OpenApiKeyItem {
  id: string;
  alias: string;
  prefix: string;
  scopes: string[];
  createdAt: string;
  lastUsedAt?: string | null;
  expiresAt?: string | null;
}

interface OpenApiKeyCreateResponse {
  key: string;
  item: OpenApiKeyItem;
}

export async function listOpenApiKeys(keyword?: string): Promise<OpenApiKeyItem[]> {
  const params = new URLSearchParams({
    pageIndex: "1",
    pageSize: "50"
  });
  if (keyword?.trim()) {
    params.set("keyword", keyword.trim());
  }
  const response = await requestApi<ApiResponse<OpenApiKeyItem[]>>(`/open/api-keys?${params.toString()}`);
  return response.data ?? [];
}

export async function createOpenApiKey(alias: string, scopes?: string[]): Promise<OpenApiKeyCreateResponse> {
  const response = await requestApi<ApiResponse<OpenApiKeyCreateResponse>>("/open/api-keys", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ alias, scopes })
  });
  if (!response.data) {
    throw new Error(response.message || "Failed to create API key");
  }
  return response.data;
}

export async function deleteOpenApiKey(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>(`/open/api-keys/${encodeURIComponent(id)}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "Failed to delete API key");
  }
}
