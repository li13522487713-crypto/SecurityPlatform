import { createApiClient } from "@atlas/shared-core/api";
import type { RequestOptions } from "@atlas/shared-core/api";

export type AppRuntimeMode = "platform" | "direct";

const LAST_APP_KEY_STORAGE = "atlas_app_last_appkey";
let unauthorizedHandler: (() => void | Promise<void>) | null = null;

const APP_RUNTIME_MODE: AppRuntimeMode = (() => {
  const rawMode = String(import.meta.env.VITE_APP_RUNTIME_MODE ?? "platform")
    .trim()
    .toLowerCase();
  return rawMode === "direct" ? "direct" : "platform";
})();

const APP_HOST_TARGET = String(import.meta.env.VITE_APP_HOST_TARGET ?? "http://127.0.0.1:5002")
  .trim()
  .replace(/\/+$/, "");

export const API_BASE = import.meta.env.VITE_API_BASE ?? (
  APP_RUNTIME_MODE === "direct"
    ? `${APP_HOST_TARGET}/api/v1`
    : "/api/v1"
);

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
  return APP_RUNTIME_MODE === "direct";
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
  if (unauthorizedHandler) {
    await unauthorizedHandler();
    return;
  }

  if (typeof window !== "undefined") {
    const appKey = getCurrentRouteAppKey();
    if (appKey) {
      window.location.assign(`/apps/${encodeURIComponent(appKey)}/login`);
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

export function resolveAppHostPrefix(appKey?: string): string {
  if (isDirectRuntimeMode()) {
    return "";
  }

  const normalizedAppKey = appKey?.trim();
  if (normalizedAppKey) {
    return `/app-host/${encodeURIComponent(normalizedAppKey)}`;
  }

  if (typeof window === "undefined") return "";

  const appHostMatch = window.location.pathname.match(/^\/app-host\/([^/]+)/);
  if (appHostMatch) {
    return `/app-host/${appHostMatch[1]}`;
  }

  const appRouteMatch = window.location.pathname.match(/^\/apps\/([^/]+)/);
  if (appRouteMatch) {
    return `/app-host/${encodeURIComponent(decodeURIComponent(appRouteMatch[1]))}`;
  }

  return "";
}
