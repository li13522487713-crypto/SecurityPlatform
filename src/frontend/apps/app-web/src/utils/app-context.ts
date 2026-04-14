const LEGACY_APP_ID_STORAGE_KEY = "atlas_current_app_id";
const APP_INSTANCE_STORAGE_KEY = "atlas_app_instance_ids";
const APP_KEY_PATH_REGEX = /^\/apps\/([^/]+)/;

type AppInstanceStorageMap = Record<string, string>;

function normalizeValue(value?: string | null): string {
  return value?.trim() ?? "";
}

function clearLegacyAppIdStorage(): void {
  if (typeof window === "undefined") {
    return;
  }

  localStorage.removeItem(LEGACY_APP_ID_STORAGE_KEY);
}

function readAppInstanceStorage(): AppInstanceStorageMap {
  if (typeof window === "undefined") {
    return {};
  }

  const raw = localStorage.getItem(APP_INSTANCE_STORAGE_KEY);
  if (!raw) {
    return {};
  }

  try {
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    return Object.entries(parsed).reduce<AppInstanceStorageMap>((result, [appKey, appInstanceId]) => {
      const normalizedAppKey = normalizeValue(appKey);
      const normalizedAppInstanceId = typeof appInstanceId === "string" ? normalizeValue(appInstanceId) : "";
      if (normalizedAppKey && normalizedAppInstanceId) {
        result[normalizedAppKey] = normalizedAppInstanceId;
      }

      return result;
    }, {});
  } catch {
    return {};
  }
}

function writeAppInstanceStorage(storage: AppInstanceStorageMap): void {
  if (typeof window === "undefined") {
    return;
  }

  clearLegacyAppIdStorage();
  const entries = Object.entries(storage).filter(([, appInstanceId]) => normalizeValue(appInstanceId));
  if (entries.length === 0) {
    localStorage.removeItem(APP_INSTANCE_STORAGE_KEY);
    return;
  }

  localStorage.setItem(APP_INSTANCE_STORAGE_KEY, JSON.stringify(Object.fromEntries(entries)));
}

export function getAppInstanceIdFromStorage(appKey?: string | null): string | null {
  const normalizedAppKey = normalizeValue(appKey);
  if (!normalizedAppKey) {
    return null;
  }

  const stored = readAppInstanceStorage()[normalizedAppKey];
  const normalizedAppInstanceId = normalizeValue(stored);
  return normalizedAppInstanceId || null;
}

export function setAppInstanceIdToStorage(appKey: string | null | undefined, appInstanceId: string | null | undefined): void {
  const normalizedAppKey = normalizeValue(appKey);
  if (!normalizedAppKey) {
    clearLegacyAppIdStorage();
    return;
  }

  const storage = readAppInstanceStorage();
  const normalizedAppInstanceId = normalizeValue(appInstanceId);
  if (!normalizedAppInstanceId) {
    delete storage[normalizedAppKey];
  } else {
    storage[normalizedAppKey] = normalizedAppInstanceId;
  }

  writeAppInstanceStorage(storage);
}

export function clearAppInstanceStorage(appKey?: string | null): void {
  if (typeof window === "undefined") {
    return;
  }

  if (!appKey) {
    clearLegacyAppIdStorage();
    localStorage.removeItem(APP_INSTANCE_STORAGE_KEY);
    return;
  }

  const storage = readAppInstanceStorage();
  delete storage[normalizeValue(appKey)];
  writeAppInstanceStorage(storage);
}

export function getCurrentAppKeyFromPath(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  const match = window.location.pathname.match(APP_KEY_PATH_REGEX);
  const appKey = match?.[1] ? decodeURIComponent(match[1]) : "";
  return appKey.trim() || null;
}
