import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";

type ResolveAppId = (appKey: string) => Promise<string | null>;

const appIdCache = new Map<string, string>();

export function useAppContext(resolveAppId?: ResolveAppId) {
  const route = useRoute();
  const appId = ref<string | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);
  const appKey = computed(() => String(route.params.appKey ?? ""));

  async function resolve(key: string): Promise<string | null> {
    if (!key) return null;

    const cached = appIdCache.get(key);
    if (cached) {
      appId.value = cached;
      return cached;
    }

    if (!resolveAppId) {
      appId.value = null;
      return null;
    }

    loading.value = true;
    error.value = null;
    try {
      const result = await resolveAppId(key);
      if (!result) {
        appId.value = null;
        return null;
      }
      appIdCache.set(key, result);
      appId.value = result;
      return result;
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Failed to resolve appId";
      appId.value = null;
      return null;
    } finally {
      loading.value = false;
    }
  }

  watch(
    appKey,
    (key) => {
      if (key) {
        void resolve(key);
      }
    },
    { immediate: true }
  );

  return {
    appKey,
    appId: computed(() => appId.value),
    loading: computed(() => loading.value),
    error: computed(() => error.value),
    resolveAppId: resolve
  };
}
