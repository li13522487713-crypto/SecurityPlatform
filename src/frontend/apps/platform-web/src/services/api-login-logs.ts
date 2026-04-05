import type { ApiResponse, PagedResult } from "@atlas/shared-core";
import { requestApi } from "./api-core";

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
  const query = new URLSearchParams();
  query.set("PageIndex", String(params.pageIndex ?? 1));
  query.set("PageSize", String(params.pageSize ?? 20));
  if (params.username) query.set("Username", params.username);
  if (params.ipAddress) query.set("IpAddress", params.ipAddress);
  if (typeof params.loginStatus === "boolean") query.set("LoginStatus", String(params.loginStatus));
  if (params.from) query.set("From", params.from);
  if (params.to) query.set("To", params.to);
  const response = await requestApi<ApiResponse<PagedResult<LoginLogDto>>>(`/login-logs?${query}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}
