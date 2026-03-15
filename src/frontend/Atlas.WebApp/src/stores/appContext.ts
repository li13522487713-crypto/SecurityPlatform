import { defineStore } from 'pinia';
import { ref, watch } from 'vue';

const APP_CONTEXT_KEY = 'atlas_current_app_id';

export const useAppContextStore = defineStore('appContext', () => {
  const currentAppId = ref<string | null>(localStorage.getItem(APP_CONTEXT_KEY));

  const setCurrentAppId = (appId: string | null) => {
    currentAppId.value = appId;
  };

  const clearCurrentAppId = () => {
    currentAppId.value = null;
  };

  // Sync to localStorage for persistence across reloads
  watch(currentAppId, (newVal) => {
    if (newVal) {
      localStorage.setItem(APP_CONTEXT_KEY, newVal);
    } else {
      localStorage.removeItem(APP_CONTEXT_KEY);
    }
  });

  return {
    currentAppId,
    setCurrentAppId,
    clearCurrentAppId,
  };
});
