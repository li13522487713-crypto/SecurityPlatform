import { useAppContextStore } from '@/stores/appContext';

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
