import type { RouteLocationNormalizedLoaded } from "vue-router";

/**
 * 从当前 URL 解析应用 ID（/apps/{appId}/...）。
 */
export function getCurrentAppIdFromStorage(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  const match = window.location.pathname.match(/^\/apps\/([^/]+)/);
  if (match?.[1]) {
    return decodeURIComponent(match[1]);
  }
  return null;
}

/**
 * 优先取 route.params.appId，否则从当前路径解析。
 */
export function resolveCurrentAppId(route: RouteLocationNormalizedLoaded): string | null {
  const routeAppId = typeof route.params.appId === "string" ? route.params.appId.trim() : "";
  if (routeAppId) {
    return routeAppId;
  }
  return getCurrentAppIdFromStorage();
}
