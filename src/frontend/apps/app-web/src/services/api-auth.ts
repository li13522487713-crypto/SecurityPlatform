import type { ApiResponse, AuthTokenResult } from "@atlas/shared-core";
import {
  setAccessToken,
  setRefreshToken,
  setTenantId,
  getRefreshToken,
  getTenantId,
  clearAuthStorage
} from "@atlas/shared-core";
import { requestApi, persistTokenResult } from "./api-core";

const TENANT_ID_REGEX =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

function assertTenantId(tenantId: string): void {
  if (!TENANT_ID_REGEX.test(tenantId)) {
    throw new Error("请输入有效的租户 ID。");
  }
}

export async function loginByAppEntry(
  tenantId: string,
  username: string,
  password: string
): Promise<void> {
  const normalizedTenantId = tenantId.trim();
  assertTenantId(normalizedTenantId);

  const response = await fetch("/api/v1/auth/token", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": normalizedTenantId
    },
    credentials: "include",
    body: JSON.stringify({ username, password })
  });

  const payload = (await response.json()) as ApiResponse<AuthTokenResult>;
  if (!response.ok || !payload.data) {
    throw new Error(payload.message || "登录失败");
  }

  setTenantId(normalizedTenantId);
  setAccessToken(payload.data.accessToken);
  setRefreshToken(payload.data.refreshToken);
}

export async function refreshToken(): Promise<boolean> {
  const refreshTokenValue = getRefreshToken();
  const tenantId = getTenantId();
  if (!refreshTokenValue || !tenantId) return false;

  try {
    const response = await requestApi<ApiResponse<AuthTokenResult>>("/auth/refresh", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken: refreshTokenValue })
    }, { disableAutoRefresh: true, suppressErrorMessage: true });

    if (!response.data) return false;
    persistTokenResult(response.data);
    return true;
  } catch {
    clearAuthStorage();
    return false;
  }
}

export async function logout(): Promise<void> {
  try {
    await requestApi("/auth/logout", { method: "POST" }, { suppressErrorMessage: true });
  } catch {
    // best-effort
  } finally {
    clearAuthStorage();
  }
}
