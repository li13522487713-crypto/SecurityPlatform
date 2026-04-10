const APP_ID_STORAGE_KEY = "atlas_current_app_id";
const APP_KEY_PATH_REGEX = /^\/apps\/([^/]+)/;

export function getCurrentAppIdFromStorage(): string | null {
  if (typeof window === "undefined") {
    return null;
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

export function getCurrentAppKeyFromPath(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  const match = window.location.pathname.match(APP_KEY_PATH_REGEX);
  const appKey = match?.[1] ? decodeURIComponent(match[1]) : "";
  return appKey.trim() || null;
}
