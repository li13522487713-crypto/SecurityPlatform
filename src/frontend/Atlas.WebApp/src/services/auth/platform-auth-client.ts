/**
 * Platform Auth Client
 *
 * 平台登录、Token 刷新、注销。
 * 从 api-auth.ts 提取平台认证相关函数。
 */
import type { ApiResponse, AuthTokenResult, AuthProfile } from "@/types/api";
import { requestApi, persistTokenResult, type RequestOptions } from "@/services/api-core";
import { getRefreshToken, getTenantId } from "@/utils/auth";

const TENANT_ID_REGEX =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

function assertTenantId(tenantId: string) {
  if (!tenantId || !TENANT_ID_REGEX.test(tenantId.trim())) {
    throw new Error("请输入有效的租户ID（GUID）");
  }
}

export async function platformLogin(
  tenantId: string,
  username: string,
  password: string,
  options?: RequestOptions,
  extra?: {
    totpCode?: string;
    captchaKey?: string;
    captchaCode?: string;
    rememberMe?: boolean;
  },
): Promise<AuthTokenResult> {
  assertTenantId(tenantId);
  const normalizedTenantId = tenantId.trim();
  const response = await requestApi<ApiResponse<AuthTokenResult>>("/auth/token", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": normalizedTenantId,
    },
    body: JSON.stringify({
      username,
      password,
      totpCode: extra?.totpCode,
      captchaKey: extra?.captchaKey,
      captchaCode: extra?.captchaCode,
      rememberMe: extra?.rememberMe,
    }),
  }, { ...options, disableAutoRefresh: true });
  if (!response.data) throw new Error(response.message || "登录失败");
  persistTokenResult(response.data);
  return response.data;
}

export async function platformRefreshToken(): Promise<boolean> {
  const refreshTokenValue = getRefreshToken();
  const tenantId = getTenantId();
  if (!refreshTokenValue || !tenantId) return false;
  const response = await requestApi<ApiResponse<AuthTokenResult>>("/auth/refresh", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken: refreshTokenValue }),
  }, { disableAutoRefresh: true, suppressErrorMessage: true });
  if (!response.data) return false;
  persistTokenResult(response.data);
  return true;
}

export async function platformLogout(): Promise<void> {
  await requestApi<ApiResponse<unknown>>("/auth/logout", { method: "POST" });
}

export async function getPlatformProfile(): Promise<AuthProfile> {
  const response = await requestApi<ApiResponse<AuthProfile>>("/auth/profile");
  if (!response.data) throw new Error(response.message || "获取用户信息失败");
  return response.data;
}
