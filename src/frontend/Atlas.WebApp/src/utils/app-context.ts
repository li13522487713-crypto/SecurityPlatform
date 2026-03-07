const STORAGE_KEY = "atlas.currentAppId";

export function getCurrentAppIdFromStorage(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  const value = window.localStorage.getItem(STORAGE_KEY);
  if (!value) {
    return null;
  }
  return value.trim() || null;
}

export function setCurrentAppIdToStorage(appId: string | null | undefined): void {
  if (typeof window === "undefined") {
    return;
  }
  if (!appId || !appId.trim()) {
    window.localStorage.removeItem(STORAGE_KEY);
    return;
  }
  window.localStorage.setItem(STORAGE_KEY, appId.trim());
}

export function clearCurrentAppIdFromStorage(): void {
  if (typeof window === "undefined") {
    return;
  }
  window.localStorage.removeItem(STORAGE_KEY);
}
