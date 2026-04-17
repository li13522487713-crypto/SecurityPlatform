/**
 * 控制台 token 的会话级存储（sessionStorage）。
 *
 * - 与 JWT 完全独立：不进 localStorage，不与登录态冲突。
 * - 30 分钟过期；过期后强制重新二次认证。
 * - SSR 兼容：检测到 `typeof window === "undefined"` 时全部 no-op。
 */

const TOKEN_STORAGE_KEY = "atlas_setup_console_token";
const EXPIRES_STORAGE_KEY = "atlas_setup_console_token_expires";

export interface ConsoleTokenSnapshot {
  token: string;
  expiresAt: string;
}

function safeReadItem(key: string): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  try {
    return window.sessionStorage.getItem(key);
  } catch {
    return null;
  }
}

function safeWriteItem(key: string, value: string): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.sessionStorage.setItem(key, value);
  } catch {
    // 忽略写入异常（隐私模式 / 容量限制等），避免阻断 UI。
  }
}

function safeRemoveItem(key: string): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.sessionStorage.removeItem(key);
  } catch {
    // 忽略
  }
}

export function readConsoleToken(): ConsoleTokenSnapshot | null {
  const token = safeReadItem(TOKEN_STORAGE_KEY);
  const expiresAt = safeReadItem(EXPIRES_STORAGE_KEY);
  if (!token || !expiresAt) {
    return null;
  }
  const expiresMs = Date.parse(expiresAt);
  if (Number.isNaN(expiresMs) || expiresMs <= Date.now()) {
    clearConsoleToken();
    return null;
  }
  return { token, expiresAt };
}

export function writeConsoleToken(snapshot: ConsoleTokenSnapshot): void {
  safeWriteItem(TOKEN_STORAGE_KEY, snapshot.token);
  safeWriteItem(EXPIRES_STORAGE_KEY, snapshot.expiresAt);
}

export function clearConsoleToken(): void {
  safeRemoveItem(TOKEN_STORAGE_KEY);
  safeRemoveItem(EXPIRES_STORAGE_KEY);
}
