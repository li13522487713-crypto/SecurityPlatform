import { computed, ref } from "vue";
import type { CapabilityManifest } from "../types/index";
import type { CapabilityApi } from "../services/index";

export function useCapabilityRegistry(capabilityApi: CapabilityApi) {
  const items = ref<CapabilityManifest[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);

  async function refresh() {
    loading.value = true;
    error.value = null;
    try {
      const result = await capabilityApi.list();
      items.value = result;
    } catch (err) {
      items.value = [];
      error.value = err instanceof Error ? err.message : "Load capability list failed.";
      throw err;
    } finally {
      loading.value = false;
    }
  }

  function getByKey(capabilityKey: string) {
    return items.value.find(
      (item) => item.capabilityKey.toLowerCase() === capabilityKey.trim().toLowerCase()
    ) ?? null;
  }

  const enabledItems = computed(() =>
    items.value.filter((item) => item.isEnabled)
  );

  return {
    items,
    enabledItems,
    loading,
    error,
    refresh,
    getByKey
  };
}
