import { defineStore } from "pinia";
import { ref, watch, computed } from "vue";

const APP_RUNTIME_CONTEXT_KEY = "atlas_app_runtime_context";

interface AppRuntimeContextData {
  appKey: string;
  instanceId: string;
  runtimeToken: string | null;
  baseUrl: string | null;
}

function loadContext(): AppRuntimeContextData | null {
  try {
    const raw = localStorage.getItem(APP_RUNTIME_CONTEXT_KEY);
    if (raw) {
      return JSON.parse(raw) as AppRuntimeContextData;
    }
  } catch {
    // ignore
  }
  return null;
}

export const useAppRuntimeContextStore = defineStore("appRuntimeContext", () => {
  const saved = loadContext();

  const appKey = ref<string | null>(saved?.appKey ?? null);
  const instanceId = ref<string | null>(saved?.instanceId ?? null);
  const runtimeToken = ref<string | null>(saved?.runtimeToken ?? null);
  const baseUrl = ref<string | null>(saved?.baseUrl ?? null);

  const isActive = computed(() => !!appKey.value && !!instanceId.value);

  function setContext(ctx: {
    appKey: string;
    instanceId: string;
    runtimeToken?: string | null;
    baseUrl?: string | null;
  }) {
    appKey.value = ctx.appKey;
    instanceId.value = ctx.instanceId;
    runtimeToken.value = ctx.runtimeToken ?? null;
    baseUrl.value = ctx.baseUrl ?? null;
  }

  function clearContext() {
    appKey.value = null;
    instanceId.value = null;
    runtimeToken.value = null;
    baseUrl.value = null;
  }

  watch(
    [appKey, instanceId, runtimeToken, baseUrl],
    ([key, id, token, url]) => {
      if (key && id) {
        localStorage.setItem(
          APP_RUNTIME_CONTEXT_KEY,
          JSON.stringify({ appKey: key, instanceId: id, runtimeToken: token, baseUrl: url }),
        );
      } else {
        localStorage.removeItem(APP_RUNTIME_CONTEXT_KEY);
      }
    },
  );

  return {
    appKey,
    instanceId,
    runtimeToken,
    baseUrl,
    isActive,
    setContext,
    clearContext,
  };
});
