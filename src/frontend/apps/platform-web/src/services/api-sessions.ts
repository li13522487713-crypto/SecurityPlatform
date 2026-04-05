import type { ApiResponse, PagedResult } from "@atlas/shared-core";
import { requestApi } from "./api-core";

export interface OnlineUserDto {
  sessionId: string;
  userId: string;
  username: string;
  ipAddress: string;
  clientType: string;
  loginTime: string;
  lastSeenAt: string;
  expiresAt: string;
}

export async function getOnlineUsers(params: {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
}): Promise<PagedResult<OnlineUserDto>> {
  const query = new URLSearchParams();
  query.set("PageIndex", String(params.pageIndex ?? 1));
  query.set("PageSize", String(params.pageSize ?? 20));
  if (params.keyword) query.set("Keyword", params.keyword);
  const response = await requestApi<ApiResponse<PagedResult<OnlineUserDto>>>(`/sessions?${query}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function forceLogout(sessionId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ sessionId: string }>>(`/sessions/${sessionId}`, {
    method: "DELETE"
  });
  if (!response.success) throw new Error(response.message || "强制下线失败");
}
