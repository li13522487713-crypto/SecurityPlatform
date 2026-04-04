import type { ApiResponse, AuthTokenResult } from "@/types/api";
import { setAccessToken, setRefreshToken, setTenantId } from "@/utils/auth";

const TENANT_ID_REGEX =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

export async function loginByAppEntry(tenantId: string, username: string, password: string): Promise<void> {
  const normalizedTenantId = tenantId.trim();
  if (!TENANT_ID_REGEX.test(normalizedTenantId)) {
    throw new Error("请输入有效的租户 ID。");
  }

  const response = await fetch("/api/v1/auth/token", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": normalizedTenantId
    },
    credentials: "include",
    body: JSON.stringify({
      username,
      password
    })
  });

  const payload = await response.json() as ApiResponse<AuthTokenResult>;
  if (!response.ok || !payload.data) {
    throw new Error(payload.message || "登录失败");
  }

  setTenantId(normalizedTenantId);
  setAccessToken(payload.data.accessToken);
  setRefreshToken(payload.data.refreshToken);
}
