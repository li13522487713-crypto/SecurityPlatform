import type { RouteLocationNormalizedLoaded } from "vue-router";

const APP_ID_STORAGE_KEY = "atlas_current_app_id";

export function getCurrentAppIdFromStorage(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  const match = window.location.pathname.match(/^\/apps\/([^/]+)/);
  if (match?.[1]) {
    return decodeURIComponent(match[1]);
  }
  const stored = localStorage.getItem(APP_ID_STORAGE_KEY);
  return stored && stored.trim() ? stored.trim() : null;
}

export function setCurrentAppIdToStorage(appId: string | null | undefined): void {
  if (typeof window === "undefined") {
    return;
  }
  const nextAppId = appId?.trim();
  if (!nextAppId) {
    localStorage.removeItem(APP_ID_STORAGE_KEY);
    return;
  }
  localStorage.setItem(APP_ID_STORAGE_KEY, nextAppId);
}

export function resolveCurrentAppId(route: RouteLocationNormalizedLoaded): string | null {
  const appId = typeof route.params.appId === "string" ? route.params.appId.trim() : "";
  if (appId) {
    return appId;
  }
  const appKey = typeof route.params.appKey === "string" ? route.params.appKey.trim() : "";
  if (appKey) {
    return appKey;
  }
  return getCurrentAppIdFromStorage();
}
