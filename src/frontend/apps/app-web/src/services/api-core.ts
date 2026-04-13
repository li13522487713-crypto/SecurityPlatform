import { createApiClient } from "@atlas/shared-react-core/api";
import type { RequestOptions } from "@atlas/shared-react-core/api";
import { appSignPath } from "@atlas/app-shell-shared";
import { clearAuthStorage } from "@atlas/shared-react-core/utils";

export type AppRuntimeMode = "app";

const LAST_APP_KEY_STORAGE = "atlas_app_last_appkey";
let unauthorizedHandler: (() => void | Promise<void>) | null = null;

const APP_RUNTIME_MODE: AppRuntimeMode = "app";

export const API_BASE = import.meta.env.VITE_API_BASE ?? "/api/v1";

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

function resolveRequestUrl(path: string): string {
  if (path.startsWith("http://") || path.startsWith("https://")) return path;
  if (path.startsWith("/app-host/")) return path;
  if (path.startsWith("/api/")) return path;
  return `${API_BASE}${normalizeApiPath(path)}`;
}

export function getAppRuntimeMode(): AppRuntimeMode {
  return APP_RUNTIME_MODE;
}

export function isDirectRuntimeMode(): boolean {
  return true;
}

export function setUnauthorizedHandler(handler: (() => void | Promise<void>) | null) {
  unauthorizedHandler = handler;
}

export function rememberConfiguredAppKey(appKey?: string | null) {
  const normalized = String(appKey ?? "").trim();
  if (!normalized || typeof window === "undefined") {
    return;
  }

  localStorage.setItem(LAST_APP_KEY_STORAGE, normalized);
}

export function getConfiguredAppKey(): string {
  if (typeof window === "undefined") {
    return "";
  }

  return localStorage.getItem(LAST_APP_KEY_STORAGE) ?? "";
}

function getCurrentRouteAppKey(): string {
  if (typeof window === "undefined") {
    return "";
  }

  const directMatch = window.location.pathname.match(/^\/apps\/([^/]+)/);
  if (directMatch) {
    return decodeURIComponent(directMatch[1]);
  }

  return getConfiguredAppKey();
}

async function forceLogout() {
  clearAuthStorage();

  if (unauthorizedHandler) {
    await unauthorizedHandler();
    return;
  }

  if (typeof window !== "undefined") {
    const appKey = getCurrentRouteAppKey();
    if (appKey) {
      window.location.assign(appSignPath(appKey));
      return;
    }
    window.location.assign("/");
  }
}

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
