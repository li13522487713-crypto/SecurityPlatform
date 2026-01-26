import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";

const API_BASE = import.meta.env.VITE_API_BASE ?? "";

interface TokenResult {
  accessToken: string;
  expiresAt: string;
}

export interface AssetListItem {
  id: string;
  name: string;
}

export async function createToken(tenantId: string, username: string, password: string) {
  const response = await request<ApiResponse<TokenResult>>("/auth/token", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": tenantId
    },
    body: JSON.stringify({ username, password })
  });

  if (!response.data) {
    throw new Error(response.message || "登录失败");
  }

  return response.data;
}

export async function getAssetsPaged(request: PagedRequest) {
  const query = new URLSearchParams({
    pageIndex: request.pageIndex.toString(),
    pageSize: request.pageSize.toString(),
    keyword: request.keyword ?? "",
    sortBy: request.sortBy ?? "",
    sortDesc: request.sortDesc ? "true" : "false"
  });

  const response = await request<ApiResponse<PagedResult<AssetListItem>>>(`/assets?${query.toString()}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers ?? {});
  const token = localStorage.getItem("access_token");
  const tenantId = localStorage.getItem("tenant_id");

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  if (tenantId && !headers.has("X-Tenant-Id")) {
    headers.set("X-Tenant-Id", tenantId);
  }

  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers
  });

  if (!response.ok) {
    throw new Error("网络请求失败");
  }

  return (await response.json()) as T;
}