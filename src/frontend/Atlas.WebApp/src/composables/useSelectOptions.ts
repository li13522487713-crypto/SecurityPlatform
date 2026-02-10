import { ref } from "vue";
import { message } from "ant-design-vue";
import type { PagedRequest, PagedResult } from "@/types/api";
import { debounce, type SelectOption } from "@/utils/common";

export interface UseSelectOptionsConfig<TItem> {
  /** API function to fetch paged items */
  fetcher: (params: PagedRequest) => Promise<PagedResult<TItem>>;
  /** Map a single item to { label, value } */
  mapItem: (item: TItem) => SelectOption;
  /** Page size per fetch, default 20 */
  pageSize?: number;
  /** Error message prefix */
  errorLabel?: string;
}

/**
 * Reusable composable for loading select options from a paged API.
 * Replaces the duplicated loadRoleOptions / loadDepartmentOptions / loadPositionOptions pattern.
 */
export function useSelectOptions<TItem>(config: UseSelectOptionsConfig<TItem>) {
  const { fetcher, mapItem, pageSize = 20, errorLabel = "加载选项" } = config;

  const options = ref<SelectOption[]>([]);
  const loading = ref(false);

  const load = async (keyword?: string) => {
    loading.value = true;
    try {
      const result = await fetcher({
        pageIndex: 1,
        pageSize,
        keyword: keyword?.trim() || undefined
      });
      options.value = result.items.map(mapItem);
    } catch (error) {
      message.error((error as Error).message || `${errorLabel}失败`);
    } finally {
      loading.value = false;
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
