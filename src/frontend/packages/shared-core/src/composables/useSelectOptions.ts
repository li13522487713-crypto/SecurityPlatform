import { message } from "ant-design-vue";
import { onMounted, onUnmounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { debounce, type SelectOption } from "../utils/index";
import type { PagedRequest, PagedResult } from "../types/index";

export interface UseSelectOptionsConfig<TItem> {
  fetcher: (params: PagedRequest) => Promise<PagedResult<TItem>>;
  mapItem: (item: TItem) => SelectOption;
  pageSize?: number;
  errorLabel?: string;
}

export function useSelectOptions<TItem>(config: UseSelectOptionsConfig<TItem>) {
  const { fetcher, mapItem, pageSize = 20, errorLabel } = config;
  const { t } = useI18n();

  const options = ref<SelectOption[]>([]);
  const loading = ref(false);
  const isMounted = ref(false);

  onMounted(() => {
    isMounted.value = true;
  });

  onUnmounted(() => {
    isMounted.value = false;
  });

  const load = async (keyword?: string) => {
    loading.value = true;
    try {
      const result = await fetcher({
        pageIndex: 1,
        pageSize,
        keyword: keyword?.trim() || undefined
      });
      if (!isMounted.value) return;
      options.value = result.items.map(mapItem);
    } catch (error) {
      if (!isMounted.value) return;
      const fallbackLabel = errorLabel ?? t("selectOptions.loadPrefix");
      message.error(
        (error as Error).message || `${fallbackLabel}${t("selectOptions.loadFailedSuffix")}`
      );
    } finally {
      if (isMounted.value) {
        loading.value = false;
      }
    }
  };

  const search = debounce((value: string) => {
    void load(value);
  });

  return {
    options,
    loading,
    load,
    search
  };
}
