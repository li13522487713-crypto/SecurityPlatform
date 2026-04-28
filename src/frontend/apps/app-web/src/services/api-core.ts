import { createApiClient } from "@atlas/shared-react-core/api";
import type { RequestOptions } from "@atlas/shared-react-core/api";
import { signPath } from "@atlas/app-shell-shared";
import { setBotApiUnauthorizedHandler } from "@coze-arch/bot-api";
import { clearAuthStorage } from "@atlas/shared-react-core/utils";

const LAST_APP_KEY_STORAGE = "atlas_app_last_appkey";
let unauthorizedHandler: (() => void | Promise<void>) | null = null;

function readBuildEnv(key: string): string | undefined {
  try {
    const env = (import.meta as ImportMeta & {
      env?: Record<string, string | undefined>;
    }).env;
    const value = env?.[key];
    return typeof value === "string" && value.trim().length > 0 ? value : undefined;
  } catch {
    return undefined;
  }
}

export const API_BASE = readBuildEnv("VITE_API_BASE") ?? "/api/v1";

function normalizeApiPath(path: string): string {
  if (!path) return "/";
  if (path.startsWith("/api/v1/")) {
    return `/${path.slice("/api/v1/".length)}`;
  }
  if (path === "/api/v1") {
    return "/";
  }
  return path.startsWith("/") ? path : `/${path}`;
}

function resolveAbsoluteApiUrl(path: string): string | null {
  if (!/^https?:\/\//i.test(API_BASE)) {
    return null;
  }

  const normalizedPath = path.startsWith("/") ? path : `/${path}`;
  return new URL(normalizedPath, API_BASE).toString();
}

function resolveRequestUrl(path: string): string {
  if (path.startsWith("http://") || path.startsWith("https://")) return path;
  if (path.startsWith("/app-host/")) return path;
  if (path.startsWith("/api/")) return resolveAbsoluteApiUrl(path) ?? path;
  return `${API_BASE}${normalizeApiPath(path)}`;
}

export function resolveApiUrl(path: string): string {
  if (path.startsWith("http://") || path.startsWith("https://")) {
    return path;
  }

  if (path.startsWith("/api/")) {
    return resolveAbsoluteApiUrl(path) ?? path;
  }

  return resolveRequestUrl(path);
}

export function setUnauthorizedHandler(handler: (() => void | Promise<void>) | null) {
  unauthorizedHandler = handler;
  setBotApiUnauthorizedHandler(handler ?? forceLogout);
}

export function rememberConfiguredAppKey(appKey?: string | null) {
  const normalized = String(appKey ?? "").trim();
  if (!normalized || typeof window === "undefined") {
    return;
  }

  try {
    window.localStorage.setItem(LAST_APP_KEY_STORAGE, normalized);
  } catch {
    // Ignore storage write failures to avoid blocking runtime initialization.
  }
}

export function getConfiguredAppKey(): string {
  if (typeof window === "undefined") {
    return "";
  }

  try {
    return window.localStorage.getItem(LAST_APP_KEY_STORAGE) ?? "";
  } catch {
    return "";
  }
}

async function forceLogout() {
  clearAuthStorage();

  if (unauthorizedHandler) {
    await unauthorizedHandler();
    return;
  }

  if (typeof window !== "undefined") {
    window.location.assign(signPath());
  }
}

setBotApiUnauthorizedHandler(forceLogout);

export type { RequestOptions };

const apiClient = createApiClient({
  resolveRequestUrl,
  onUnauthorized: forceLogout
});

export const persistTokenResult = apiClient.persistTokenResult;
export const requestApi = apiClient.requestApi;
export const requestApiBlob = apiClient.requestApiBlob;
export const toQuery = apiClient.toQuery;
export const requestPagedApi = apiClient.requestPagedApi;
export const uploadFile = apiClient.uploadFile;
export const downloadFile = apiClient.downloadFile;

export function resolveAppHostPrefix(_appKey?: string): string {
  return "";
}

export interface ResourceIdPayload {
  id?: string | number | null;
  Id?: string | number | null;
}

export function extractResourceId(payload?: ResourceIdPayload | null): string | null {
  const rawValue = payload?.id ?? payload?.Id;
  if (rawValue === undefined || rawValue === null) {
    return null;
  }

  const normalized = String(rawValue).trim();
  return normalized.length > 0 ? normalized : null;
}
