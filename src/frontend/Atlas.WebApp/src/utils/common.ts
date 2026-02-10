import type { PagedRequest, PagedResult } from "@/types/api";
import { message } from "ant-design-vue";

export type FormMode = "create" | "edit";

/**
 * Format an ISO date string to a human-readable local format.
 * Returns "-" for null/undefined/empty values.
 */
export function formatDateTime(value?: string | null): string {
  if (!value) return "-";
  try {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return value;
    const yyyy = date.getFullYear();
    const mm = String(date.getMonth() + 1).padStart(2, "0");
    const dd = String(date.getDate()).padStart(2, "0");
    const hh = String(date.getHours()).padStart(2, "0");
    const mi = String(date.getMinutes()).padStart(2, "0");
    const ss = String(date.getSeconds()).padStart(2, "0");
    return `${yyyy}-${mm}-${dd} ${hh}:${mi}:${ss}`;
  } catch {
    return value;
  }
}

export interface SelectOption {
  label: string;
  value: number;
}

export function debounce<T extends (...args: any[]) => void>(handler: T, delay = 300): T {
  let timer: number | undefined;
  return ((...args: any[]) => {
    if (timer) window.clearTimeout(timer);
    timer = window.setTimeout(() => handler(...args), delay);
  }) as unknown as T;
}

export interface LoadSelectOptionsConfig<TItem> {
  fetcher: (params: PagedRequest) => Promise<PagedResult<TItem>>;
  mapItem: (item: TItem) => SelectOption;
  pageSize?: number;
  errorMessage?: string;
}

export async function loadSelectOptions<TItem>(
  config: LoadSelectOptionsConfig<TItem>,
  keyword?: string
): Promise<SelectOption[]> {
  const { fetcher, mapItem, pageSize = 20, errorMessage = "加载选项失败" } = config;
  try {
    const result = await fetcher({
      pageIndex: 1,
      pageSize,
      keyword: keyword?.trim() || undefined
    });
    return result.items.map(mapItem);
  } catch (error) {
    message.error((error as Error).message || errorMessage);
    return [];
  }
}
