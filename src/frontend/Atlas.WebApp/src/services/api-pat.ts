import { requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";

export interface PersonalAccessTokenListItem {
  id: number;
  name: string;
  tokenPrefix: string;
  scopes: string[];
  createdByUserId: number;
  expiresAt?: string;
  lastUsedAt?: string;
  revokedAt?: string;
  createdAt: string;
}

export interface PersonalAccessTokenCreateRequest {
  name: string;
  scopes: string[];
  expiresAt?: string;
}

export interface PersonalAccessTokenCreateResult {
  id: number;
  name: string;
  tokenPrefix: string;
  plainTextToken: string;
  expiresAt?: string;
  scopes: string[];
}

export interface PersonalAccessTokenUpdateRequest {
  name: string;
  scopes: string[];
  expiresAt?: string;
}

export async function getPersonalAccessTokensPaged(request: PagedRequest, keyword?: string) {
  const query = toQuery(request, { keyword });
  const response = await requestApi<ApiResponse<PagedResult<PersonalAccessTokenListItem>>>(
    `/personal-access-tokens?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询 PAT 列表失败");
  }

  return response.data;
}

export async function createPersonalAccessToken(request: PersonalAccessTokenCreateRequest) {
  const response = await requestApi<ApiResponse<PersonalAccessTokenCreateResult>>("/personal-access-tokens", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建 PAT 失败");
  }

  return response.data;
}

export async function updatePersonalAccessToken(id: number, request: PersonalAccessTokenUpdateRequest) {
  const response = await requestApi<ApiResponse<object>>(`/personal-access-tokens/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新 PAT 失败");
  }
}

export async function revokePersonalAccessToken(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/personal-access-tokens/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "撤销 PAT 失败");
  }
}
