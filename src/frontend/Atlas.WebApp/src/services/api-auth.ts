// 认证模块 API：登录、注销、令牌刷新、密码、用户资料、验证码、文件上传
import type {
  ApiResponse,
  AuthTokenResult,
  AuthProfile,
  ChangePasswordRequest,
  UserProfileDetail,
  UserProfileUpdateRequest,
  FileUploadResult,
  FileRecordDto,
  RegisterRequest,
  RouterVo
} from "@/types/api";
import { requestApi, persistTokenResult, type RequestOptions } from "@/services/api-core";
import { getRefreshToken, getTenantId } from "@/utils/auth";

export interface AssetListItem {
  id: string;
  name: string;
}

export interface AlertListItem {
  id: string;
  title: string;
  createdAt: string;
}

export interface CaptchaResult {
  captchaKey: string;
  captchaImage: string;
}

export interface MfaSetupResult {
  secretKey: string;
  provisioningUri: string;
}

export interface MfaStatusResult {
  mfaEnabled: boolean;
}

const TENANT_ID_REGEX =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

function assertTenantId(tenantId: string) {
  if (!tenantId || !TENANT_ID_REGEX.test(tenantId.trim())) {
    throw new Error("请输入有效的租户ID（GUID）");
  }
}

export async function getCaptcha(tenantId: string): Promise<CaptchaResult> {
  assertTenantId(tenantId);
  const normalizedTenantId = tenantId.trim();
  const response = await requestApi<ApiResponse<CaptchaResult>>("/auth/captcha", {
    headers: { "X-Tenant-Id": normalizedTenantId }
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
  const normalizedTenantId = tenantId.trim();
  const response = await requestApi<ApiResponse<AuthTokenResult>>("/auth/token", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": normalizedTenantId
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

export async function revokeToken(): Promise<void> {
  await requestApi<ApiResponse<object>>("/auth/logout", { method: "POST" });
}

export async function getAuthProfile(): Promise<AuthProfile> {
  const response = await requestApi<ApiResponse<AuthProfile>>("/auth/profile");
  if (!response.data) throw new Error(response.message || "获取用户信息失败");
  return response.data;
}

export async function getUserProfile(): Promise<UserProfileDetail> {
  const response = await requestApi<ApiResponse<UserProfileDetail>>("/auth/profile");
  if (!response.data) throw new Error(response.message || "获取用户详情失败");
  return response.data;
}

export async function updateUserProfile(request: UserProfileUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>("/auth/profile", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function changePassword(request: ChangePasswordRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>("/auth/password", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "修改密码失败");
  }
}

export async function getMfaStatus(): Promise<MfaStatusResult> {
  const response = await requestApi<ApiResponse<MfaStatusResult>>("/mfa/status");
  if (!response.data) throw new Error(response.message || "获取 MFA 状态失败");
  return response.data;
}

export async function setupMfa(): Promise<MfaSetupResult> {
  const response = await requestApi<ApiResponse<MfaSetupResult>>("/mfa/setup", { method: "POST" });
  if (!response.data) throw new Error(response.message || "设置 MFA 失败");
  return response.data;
}

export async function verifyMfaSetup(code: string): Promise<boolean> {
  const response = await requestApi<ApiResponse<{ mfaEnabled: boolean }>>("/mfa/verify-setup", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ code })
  });
  return response.success;
}

export async function disableMfa(code: string): Promise<boolean> {
  const response = await requestApi<ApiResponse<{ mfaEnabled: boolean }>>("/mfa/disable", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ code })
  });
  return response.success;
}

export async function getCurrentUser(): Promise<AuthProfile> {
  const response = await requestApi<ApiResponse<AuthProfile>>("/auth/me");
  if (!response.data) {
    throw new Error(response.message || "获取用户信息失败");
  }
  return response.data;
}

export async function getProfileDetail(): Promise<UserProfileDetail> {
  const response = await requestApi<ApiResponse<UserProfileDetail>>("/auth/profile");
  if (!response.data) {
    throw new Error(response.message || "获取个人资料失败");
  }
  return response.data;
}

export async function updateProfile(request: UserProfileUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>("/auth/profile", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新个人资料失败");
  }
}

export async function getRouters(): Promise<RouterVo[]> {
  const response = await requestApi<ApiResponse<RouterVo[]>>("/auth/routers");
  if (!response.data) {
    throw new Error(response.message || "获取路由失败");
  }
  return response.data;
}

export async function register(
  tenantId: string,
  request: RegisterRequest,
  requestOptions?: RequestOptions
): Promise<{ id: string }> {
  assertTenantId(tenantId);
  const normalizedTenantId = tenantId.trim();
  const response = await requestApi<ApiResponse<{ id: string }>>("/auth/register", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": normalizedTenantId
    },
    body: JSON.stringify(request)
  }, { ...requestOptions, disableAutoRefresh: true });
  if (!response.data) {
    throw new Error(response.message || "注册失败");
  }
  return response.data;
}

export async function logout(): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>("/auth/logout", {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "退出失败");
  }
}

export async function deleteFile(id: string | number): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/files/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function uploadFile(file: File): Promise<FileUploadResult> {
  const formData = new FormData();
  formData.append("file", file);
  const response = await requestApi<ApiResponse<FileUploadResult>>("/files", {
    method: "POST",
    body: formData
  });
  if (!response.data) {
    throw new Error(response.message || "上传失败");
  }
  return response.data;
}

export async function getFileInfo(id: string | number): Promise<FileRecordDto> {
  const response = await requestApi<ApiResponse<FileRecordDto>>(`/files/${id}/info`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}
