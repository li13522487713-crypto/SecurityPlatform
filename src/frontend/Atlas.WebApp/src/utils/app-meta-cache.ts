interface AppMetaCacheItem {
  id: string;
  name: string;
  appKey?: string;
  expiresAt: number;
}

const APP_META_CACHE_KEY = "atlas_app_meta_cache";
const APP_META_CACHE_TTL = 60_000;
const APP_META_CACHE_MAX = 64;

function readCache(): Record<string, AppMetaCacheItem> {
  if (typeof localStorage === "undefined") {
    return {};
  }

  try {
    const raw = localStorage.getItem(APP_META_CACHE_KEY);
    if (!raw) {
      return {};
    }
    return JSON.parse(raw) as Record<string, AppMetaCacheItem>;
  } catch {
    return {};
  }
}

function writeCache(cache: Record<string, AppMetaCacheItem>): void {
  if (typeof localStorage === "undefined") {
    return;
  }

  try {
    localStorage.setItem(APP_META_CACHE_KEY, JSON.stringify(cache));
  } catch {
    // 忽略缓存写入失败
  }
}

export function rememberAppMeta(items: Array<{ id: string; name: string; appKey?: string }>, ttlMs = APP_META_CACHE_TTL): void {
  const now = Date.now();
  const cache = readCache();

  Object.keys(cache).forEach((key) => {
    if (cache[key].expiresAt <= now) {
      delete cache[key];
    }
  });

  items.forEach((item) => {
    if (!item.id || !item.name) {
      return;
    }
    cache[item.id] = {
      id: item.id,
      name: item.name,
      appKey: item.appKey,
      expiresAt: now + ttlMs
    };
  });

  const entries = Object.entries(cache)
    .sort((left, right) => right[1].expiresAt - left[1].expiresAt)
    .slice(0, APP_META_CACHE_MAX);
  writeCache(Object.fromEntries(entries));
}

export function getCachedAppMeta(appId: string): AppMetaCacheItem | null {
  if (!appId) {
    return null;
  }

  const now = Date.now();
  const cache = readCache();
  const item = cache[appId];
  if (!item) {
    return null;
  }
  if (item.expiresAt <= now) {
    delete cache[appId];
    writeCache(cache);
    return null;
  }
  return item;
}
