import type { ApiResponse, AuthProfile } from "@atlas/shared-core";
import { requestApi } from "./api-core";

export async function getCurrentUser(): Promise<AuthProfile> {
  const response = await requestApi<ApiResponse<AuthProfile>>("/auth/me");
  if (!response.data) throw new Error(response.message || "获取用户信息失败");
  return response.data;
}
