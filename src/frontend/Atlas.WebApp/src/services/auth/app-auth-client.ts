/**
 * App Auth Client
 *
 * 应用运行时 Token 管理。
 * Phase 3 填入完整实现：
 * - 应用登录页独立 Token 获取
 * - 应用级 Token 刷新
 * - 应用级注销
 */

const APP_TOKEN_KEY = "atlas_app_runtime_token";
const APP_REFRESH_TOKEN_KEY = "atlas_app_runtime_refresh_token";

export function getAppToken(): string | null {
  return localStorage.getItem(APP_TOKEN_KEY);
}

export function setAppToken(token: string): void {
  localStorage.setItem(APP_TOKEN_KEY, token);
}

export function getAppRefreshToken(): string | null {
  return localStorage.getItem(APP_REFRESH_TOKEN_KEY);
}

export function setAppRefreshToken(token: string): void {
  localStorage.setItem(APP_REFRESH_TOKEN_KEY, token);
}

export function clearAppAuth(): void {
  localStorage.removeItem(APP_TOKEN_KEY);
  localStorage.removeItem(APP_REFRESH_TOKEN_KEY);
}

export async function appLogin(
  baseUrl: string,
  username: string,
  password: string,
): Promise<{ accessToken: string; refreshToken: string }> {
  const response = await fetch(`${baseUrl}/auth/token`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, password }),
  });

  if (!response.ok) {
    throw new Error(`应用登录失败: ${response.status}`);
  }

  const result = (await response.json()) as {
    data: { accessToken: string; refreshToken: string };
  };
  setAppToken(result.data.accessToken);
  setAppRefreshToken(result.data.refreshToken);
  return result.data;
}

export async function appRefreshToken(baseUrl: string): Promise<boolean> {
  const refreshTokenValue = getAppRefreshToken();
  if (!refreshTokenValue) return false;

  try {
    const response = await fetch(`${baseUrl}/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken: refreshTokenValue }),
    });

    if (!response.ok) return false;

    const result = (await response.json()) as {
      data: { accessToken: string; refreshToken: string };
    };
    setAppToken(result.data.accessToken);
    setAppRefreshToken(result.data.refreshToken);
    return true;
  } catch {
    return false;
  }
}

export async function appLogout(baseUrl: string): Promise<void> {
  const token = getAppToken();
  if (!token) return;

  try {
    await fetch(`${baseUrl}/auth/logout`, {
      method: "POST",
      headers: { Authorization: `Bearer ${token}` },
    });
  } finally {
    clearAppAuth();
  }
}
