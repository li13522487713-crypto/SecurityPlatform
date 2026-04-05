import type { ApiResponse, PagedResult } from "@atlas/shared-core";
import { requestApi } from "./api-core";

export interface UserListItem {
  id: string;
  username: string;
  displayName: string;
  email: string;
  phoneNumber: string;
  isActive: boolean;
  lastLoginAt: string | null;
}

export async function searchTenantUsers(
  keyword: string,
  pageSize = 20
): Promise<PagedResult<UserListItem>> {
  const params = new URLSearchParams({
    PageIndex: "1",
    PageSize: pageSize.toString(),
    Keyword: keyword
  });
  const response = await requestApi<ApiResponse<PagedResult<UserListItem>>>(
    `/users?${params.toString()}`
  );
  if (!response.data) throw new Error(response.message || "搜索用户失败");
  return response.data;
}
