import { getAppInstanceIdByAppKey } from "./api-lowcode-runtime";
import {
  clearAppInstanceStorage,
  getAppInstanceIdFromStorage,
  getCurrentAppKeyFromPath,
  setAppInstanceIdToStorage
} from "../utils/app-context";

const APP_INSTANCE_ID_REGEX = /^[1-9]\d*$/;
const pendingByAppKey = new Map<string, Promise<string | null>>();

function normalizeAppKey(appKey?: string | null): string {
  const normalizedAppKey = appKey?.trim();
  if (normalizedAppKey) {
    return normalizedAppKey;
  }

  return getCurrentAppKeyFromPath() ?? "";
}

function normalizeAppInstanceId(appInstanceId?: string | null): string | null {
  const normalizedAppInstanceId = appInstanceId?.trim() ?? "";
  return APP_INSTANCE_ID_REGEX.test(normalizedAppInstanceId) ? normalizedAppInstanceId : null;
}

export function getCachedAppInstanceId(appKey?: string | null): string | null {
  const normalizedAppKey = normalizeAppKey(appKey);
  if (!normalizedAppKey) {
    return null;
  }

  const cachedAppInstanceId = normalizeAppInstanceId(getAppInstanceIdFromStorage(normalizedAppKey));
  if (!cachedAppInstanceId) {
    setAppInstanceIdToStorage(normalizedAppKey, null);
    return null;
  }

  return cachedAppInstanceId;
}

export async function resolveAppInstanceId(appKey?: string | null): Promise<string | null> {
  const normalizedAppKey = normalizeAppKey(appKey);
  if (!normalizedAppKey) {
    return null;
  }

  const cachedAppInstanceId = getCachedAppInstanceId(normalizedAppKey);
  if (cachedAppInstanceId) {
    return cachedAppInstanceId;
  }

  const pending = pendingByAppKey.get(normalizedAppKey);
  if (pending) {
    return pending;
  }

  const resolution = getAppInstanceIdByAppKey(normalizedAppKey)
    .then((appInstanceId) => {
      const normalizedAppInstanceId = normalizeAppInstanceId(appInstanceId);
      setAppInstanceIdToStorage(normalizedAppKey, normalizedAppInstanceId);
      return normalizedAppInstanceId;
    })
    .catch(() => {
      setAppInstanceIdToStorage(normalizedAppKey, null);
      return null;
    })
    .finally(() => {
      pendingByAppKey.delete(normalizedAppKey);
    });

  pendingByAppKey.set(normalizedAppKey, resolution);
  return resolution;
}

export function resetAppInstanceContextForTests(): void {
  pendingByAppKey.clear();
  clearAppInstanceStorage();
}
