import { useAppContextStore } from '@/stores/appContext';
import type { RouteLocationNormalizedLoaded } from 'vue-router';

export function getCurrentAppIdFromStorage(): string | null {
  const store = useAppContextStore();
  return store.currentAppId;
}

export function setCurrentAppIdToStorage(appId: string | null | undefined): void {
  const store = useAppContextStore();
  store.setCurrentAppId(appId && appId.trim() ? appId.trim() : null);
}

export function clearCurrentAppIdFromStorage(): void {
  const store = useAppContextStore();
  store.clearCurrentAppId();
}

/**
 * 从路由参数或 storage 中解析当前 appId。
 * 优先取 route.params.appId，降级取 storage 中的上下文。
 */
export function resolveCurrentAppId(route: RouteLocationNormalizedLoaded): string | null {
  const routeAppId = typeof route.params.appId === 'string' ? route.params.appId.trim() : '';
  if (routeAppId) {
    return routeAppId;
  }
  return getCurrentAppIdFromStorage();
}
