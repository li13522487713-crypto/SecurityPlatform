import { ref, computed } from 'vue';

export interface UseMasterDetailOptions<T> {
  defaultSelected?: T | null;
  onSelect?: (item: T) => void | Promise<void>;
  onClear?: () => void;
}

export function useMasterDetail<T>(options: UseMasterDetailOptions<T> = {}) {
  const selectedItem = ref<T | null>(options.defaultSelected ?? null) as import('vue').Ref<T | null>;
  const isDetailVisible = computed(() => selectedItem.value !== null);
  const detailLoading = ref(false);

  const selectItem = async (item: T) => {
    selectedItem.value = item;
    if (options.onSelect) {
      detailLoading.value = true;
      try {
        await options.onSelect(item);
      } finally {
        detailLoading.value = false;
      }
    }
  };

  const clearSelection = () => {
    selectedItem.value = null;
    if (options.onClear) {
      options.onClear();
    }
  };

  return {
    selectedItem,
    isDetailVisible,
    detailLoading,
    selectItem,
    clearSelection,
  };
}
