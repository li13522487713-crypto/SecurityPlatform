import type { ApiResponse, AuthTokenResult, AuthProfile } from "@atlas/shared-core";
import { getRefreshToken, getTenantId } from "@atlas/shared-core";
import { requestApi, persistTokenResult, type RequestOptions } from "./api-core";
import type { CaptchaResult, RouterVo, RegisterRequest } from "@/types/api";

const TENANT_ID_REGEX =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

function assertTenantId(tenantId: string) {
  if (!tenantId || !TENANT_ID_REGEX.test(tenantId.trim())) {
    throw new Error("请输入有效的租户ID（GUID）");
  }
}

export async function getCaptcha(tenantId: string): Promise<CaptchaResult> {
  assertTenantId(tenantId);
  const normalized = tenantId.trim();
  const response = await requestApi<ApiResponse<CaptchaResult>>("/auth/captcha", {
    headers: { "X-Tenant-Id": normalized }
  }, { disableAutoRefresh: true });
  if (!response.data) throw new Error("获取验证码失败");
  return response.data;
}

export async function createToken(
  tenantId: string,
  username: string,
  password: string,
  requestOptions?: RequestOptions,
  extra?: {
    totpCode?: string;
    captchaKey?: string;
    captchaCode?: string;
    rememberMe?: boolean;
  }
) {
  assertTenantId(tenantId);
  const normalized = tenantId.trim();
  const response = await requestApi<ApiResponse<AuthTokenResult>>("/auth/token", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": normalized
    },
    body: JSON.stringify({
      username,
      password,
      totpCode: extra?.totpCode,
      captchaKey: extra?.captchaKey,
      captchaCode: extra?.captchaCode,
      rememberMe: extra?.rememberMe
    })
  }, { ...requestOptions, disableAutoRefresh: true });
  if (!response.data) throw new Error(response.message || "登录失败");
  persistTokenResult(response.data);
  return response.data;
}

export async function refreshToken(): Promise<boolean> {
  const refreshTokenValue = getRefreshToken();
  const tenantId = getTenantId();
  if (!refreshTokenValue || !tenantId) return false;
  const response = await requestApi<ApiResponse<AuthTokenResult>>("/auth/refresh", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken: refreshTokenValue })
  }, { disableAutoRefresh: true, suppressErrorMessage: true });
  if (!response.data) return false;
  persistTokenResult(response.data);
  return true;
}

export async function getCurrentUser(): Promise<AuthProfile> {
  const response = await requestApi<ApiResponse<AuthProfile>>("/auth/me");
  if (!response.data) throw new Error(response.message || "获取用户信息失败");
  return response.data;
}

export async function getRouters(): Promise<RouterVo[]> {
  const response = await requestApi<ApiResponse<RouterVo[]>>("/auth/routers");
  if (!response.data) throw new Error(response.message || "获取路由失败");
  return response.data;
}

export async function logout(): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>("/auth/logout", {
    method: "POST"
  });
  if (!response.success) throw new Error(response.message || "退出失败");
}

export async function register(
  tenantId: string,
  request: RegisterRequest,
  requestOptions?: RequestOptions
): Promise<{ id: string }> {
  assertTenantId(tenantId);
  const normalized = tenantId.trim();
  const response = await requestApi<ApiResponse<{ id: string }>>("/auth/register", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": normalized
    },
    body: JSON.stringify(request)
  }, { ...requestOptions, disableAutoRefresh: true });
  if (!response.data) throw new Error(response.message || "注册失败");
  return response.data;
}
