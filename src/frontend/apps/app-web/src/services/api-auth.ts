import type { ApiResponse, AuthTokenResult } from "@atlas/shared-react-core/types";
import {
  setAccessToken,
  setRefreshToken,
  setTenantId,
  getRefreshToken,
  getTenantId,
  clearAuthStorage
} from "@atlas/shared-react-core/utils";
import { API_BASE, requestApi, persistTokenResult } from "./api-core";

const TENANT_ID_REGEX =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

function assertTenantId(tenantId: string): void {
  if (!TENANT_ID_REGEX.test(tenantId)) {
    throw new Error("Invalid tenant ID format");
  }
}

export async function loginByAppEntry(
  tenantId: string,
  username: string,
  password: string,
  totpCode?: string
): Promise<void> {
  const normalizedTenantId = tenantId.trim();
  assertTenantId(normalizedTenantId);
  const tokenEndpoint = `${API_BASE}/auth/token`;

  const response = await fetch(tokenEndpoint, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": normalizedTenantId
    },
    credentials: "include",
    body: JSON.stringify({ username, password, totpCode: totpCode?.trim() || undefined })
  });

  const payload = await parseApiResponse<AuthTokenResult>(response);
  if (!response.ok || !payload.data) {
    const error = new Error(payload.message || `Login failed (HTTP ${response.status})`) as Error & { code?: string };
    error.code = payload.code;
    throw error;
  }

  setTenantId(normalizedTenantId);
  setAccessToken(payload.data.accessToken);
  setRefreshToken(payload.data.refreshToken);
}

async function parseApiResponse<T>(response: Response): Promise<ApiResponse<T>> {
  if (response.status === 204 || response.status === 205) {
    return {} as ApiResponse<T>;
  }

  const raw = await response.text();
  if (!raw.trim()) {
    return {} as ApiResponse<T>;
  }

  try {
    return JSON.parse(raw) as ApiResponse<T>;
  } catch {
    return { message: raw } as ApiResponse<T>;
  }
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
