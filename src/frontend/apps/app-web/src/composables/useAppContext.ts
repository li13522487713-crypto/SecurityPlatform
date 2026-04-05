import { ref, computed, watch } from "vue";
import { useRoute } from "vue-router";
import { getLowCodeAppByKey } from "@/services/api-lowcode-runtime";

const appIdCache = new Map<string, string>();
const appId = ref<string | null>(null);
const loading = ref(false);
const error = ref<string | null>(null);

export function useAppContext() {
  const route = useRoute();
  const appKey = computed(() => String(route.params.appKey ?? ""));

  async function resolveAppId(key: string): Promise<string | null> {
    if (!key) return null;

    const cached = appIdCache.get(key);
    if (cached) {
      appId.value = cached;
      return cached;
    }

    loading.value = true;
    error.value = null;
    try {
      const detail = await getLowCodeAppByKey(key);
      appIdCache.set(key, detail.id);
      appId.value = detail.id;
      return detail.id;
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
        void resolveAppId(key);
      }
    },
    { immediate: true }
  );

  return {
    appKey,
    appId: computed(() => appId.value),
    loading: computed(() => loading.value),
    error: computed(() => error.value),
    resolveAppId
  };
}
