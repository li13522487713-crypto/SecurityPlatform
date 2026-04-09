import { createApiClient } from "@atlas/shared-core/api";
import type { RequestOptions } from "@atlas/shared-core/api";
import { router } from "@/router";

export type AppRuntimeMode = "platform" | "direct";
const LAST_APP_KEY_STORAGE = "atlas_app_last_appkey";

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
export type { RequestOptions };

function forceLogout() {
  if (router.currentRoute.value.name !== "app-login") {
    void router.push({ name: "app-login", params: { appKey: getAppKeyFromRoute() } });
  }
}

function getAppKeyFromRoute(): string {
  const params = router.currentRoute.value.params;
  if (typeof params.appKey === "string" && params.appKey.trim()) {
    return params.appKey;
  }

  if (typeof window !== "undefined") {
    return localStorage.getItem(LAST_APP_KEY_STORAGE) ?? "";
  }

  return "";
}

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
