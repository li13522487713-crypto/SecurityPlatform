import { computed, ref, watch } from "vue";
import type { NavigationHostMode, NavigationProjectionGroup } from "../types/index";
import type { NavigationProjectionApi } from "../services/index";

type MaybeValue<T> = T | (() => T);

export interface UseNavigationProjectionOptions {
  hostMode: NavigationHostMode;
  api: NavigationProjectionApi;
  appInstanceId?: MaybeValue<string | undefined>;
  appKey?: MaybeValue<string | undefined>;
  enabled?: MaybeValue<boolean | undefined>;
}

export function useNavigationProjection(options: UseNavigationProjectionOptions) {
  const groups = ref<NavigationProjectionGroup[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);
  const generatedAt = ref<string | null>(null);

  function readValue<T>(value: MaybeValue<T> | undefined): T | undefined {
    if (typeof value === "function") {
      return (value as () => T)();
    }

    return value;
  }

  async function refresh() {
    if (readValue(options.enabled) === false) {
      groups.value = [];
      generatedAt.value = null;
      return;
    }

    loading.value = true;
    error.value = null;

    try {
      let response;
      if (options.hostMode === "platform") {
        response = await options.api.getPlatform();
      } else if (options.hostMode === "app") {
        const appInstanceId = readValue(options.appInstanceId)?.trim();
        const appKey = readValue(options.appKey)?.trim();
        if (appInstanceId) {
          response = await options.api.getWorkspace(appInstanceId);
        } else if (appKey) {
          response = await options.api.getWorkspaceByAppKey(appKey);
        } else {
          groups.value = [];
          generatedAt.value = null;
          return;
        }
      } else {
        response = await options.api.getRuntime();
      }

      groups.value = response.groups ?? [];
      generatedAt.value = response.generatedAt;
    } catch (err) {
      groups.value = [];
      generatedAt.value = null;
      error.value = err instanceof Error ? err.message : "Load navigation projection failed.";
    } finally {
      loading.value = false;
    }
  }

  watch(
    () => `${options.hostMode}:${readValue(options.appInstanceId) ?? ""}:${readValue(options.appKey) ?? ""}:${readValue(options.enabled) !== false}`,
    () => {
      void refresh();
    },
    { immediate: true }
  );

  const items = computed(() =>
    groups.value.flatMap((group) => group.items)
  );

  return {
    groups,
    items,
    loading,
    error,
    generatedAt,
    refresh
  };
}
