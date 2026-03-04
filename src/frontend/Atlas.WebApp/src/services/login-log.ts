import { requestApi } from "@/services/api-core";
import { API_BASE } from "@/services/api-core";
import type { ApiResponse, PagedResult } from "@/types/api";
import { getAccessToken, getTenantId } from "@/utils/auth";

export interface LoginLogDto {
  id: string;
  username: string;
  ipAddress: string;
  browser?: string;
  operatingSystem?: string;
  loginStatus: boolean;
  message?: string;
  loginTime: string;
}

export interface LoginLogQueryParams {
  pageIndex?: number;
  pageSize?: number;
  username?: string;
  ipAddress?: string;
  loginStatus?: boolean;
  from?: string;
  to?: string;
}

export async function getLoginLogsPaged(params: LoginLogQueryParams): Promise<PagedResult<LoginLogDto>> {
  const query = buildQuery(params);
  const response = await requestApi<ApiResponse<PagedResult<LoginLogDto>>>(`/login-logs?${query}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function exportLoginLogs(params: LoginLogQueryParams): Promise<Blob> {
  const query = buildQuery(params);
  const headers = new Headers();
  const token = getAccessToken();
  const tenantId = getTenantId();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }
  if (tenantId) {
    headers.set("X-Tenant-Id", tenantId);
  }
  const response = await fetch(`${API_BASE}/login-logs/export?${query}`, {
    method: "GET",
    headers,
    credentials: "include"
  });
  if (!response.ok) {
    throw new Error("导出失败");
  }
  return response.blob();
}

function buildQuery(params: LoginLogQueryParams): string {
  const query = new URLSearchParams();
  query.set("pageIndex", String(params.pageIndex ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  if (params.username) query.set("username", params.username);
  if (params.ipAddress) query.set("ipAddress", params.ipAddress);
  if (typeof params.loginStatus === "boolean") query.set("loginStatus", String(params.loginStatus));
  if (params.from) query.set("from", params.from);
  if (params.to) query.set("to", params.to);
  return query.toString();
}
