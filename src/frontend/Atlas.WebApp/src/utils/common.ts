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

export function debounce<T extends (...args: Parameters<T>) => void>(handler: T, delay = 300): T {
  let timer: number | undefined;
  return ((...args: Parameters<T>) => {
    if (timer) window.clearTimeout(timer);
    timer = window.setTimeout(() => handler(...args), delay);
  }) as T;
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

export interface TreeNode {
  [key: string]: unknown;
}

/**
 * 构造树型结构数据
 * @param data 数据源
 * @param id id字段 默认 'id'
 * @param parentId 父节点字段 默认 'parentId'
 * @param children 孩子节点字段 默认 'children'
 */
export function handleTree<T extends TreeNode>(data: T[], id?: string, parentId?: string, children?: string): T[] {
  const config = {
    id: id || 'id',
    parentId: parentId || 'parentId',
    childrenList: children || 'children'
  };

  const childrenListMap: Record<string, T[]> = {};
  const nodeIds: Record<string, T> = {};
  const tree: T[] = [];

  for (const d of data) {
    const pid = d[config.parentId] as string;
    if (childrenListMap[pid] == null) {
      childrenListMap[pid] = [];
    }
    nodeIds[d[config.id] as string] = d;
    childrenListMap[pid].push(d);
  }

  for (const d of data) {
    const pid = d[config.parentId] as string;
    if (nodeIds[pid] == null) {
      tree.push(d);
    }
  }

  for (const t of tree) {
    adaptToChildrenList(t);
  }

  function adaptToChildrenList(o: T) {
    const nodeId = o[config.id] as string;
    if (childrenListMap[nodeId] != null) {
      (o as TreeNode)[config.childrenList] = childrenListMap[nodeId];
    }
    const childList = o[config.childrenList] as T[] | undefined;
    if (childList) {
      for (const c of childList) {
        adaptToChildrenList(c);
      }
    }
  }
  return tree;
}

export interface DateRangeParams {
  params?: Record<string, string | undefined>;
  [key: string]: unknown;
}

/**
 * 添加日期范围
 * @param params 查询参数对象
 * @param dateRange 日期范围数组 [beginDate, endDate]
 * @param propName 字段名前缀（可选，默认写入 beginTime/endTime）
 */
export function addDateRange<T extends DateRangeParams>(params: T, dateRange: string[], propName?: string): T {
  const search = params;
  search.params = typeof search.params === 'object' && search.params !== null && !Array.isArray(search.params)
    ? search.params
    : {};
  const range = Array.isArray(dateRange) ? dateRange : [];
  if (typeof propName === 'undefined') {
    search.params['beginTime'] = range[0];
    search.params['endTime'] = range[1];
  } else {
    search.params['begin' + propName] = range[0];
    search.params['end' + propName] = range[1];
  }
  return search;
}

export interface DictItem {
  dictValue: string;
  dictLabel: string;
}

/**
 * 回显数据字典
 * @param datas 字典数据
 * @param value 当前值
 */
export function selectDictLabel(datas: Record<string, DictItem>, value: string | number | undefined): string {
  if (value === undefined) {
    return '';
  }
  const actions: string[] = [];
  Object.keys(datas).some((key) => {
    if (datas[key].dictValue === '' + value) {
      actions.push(datas[key].dictLabel);
      return true;
    }
    return false;
  });
  if (actions.length === 0) {
    actions.push(String(value));
  }
  return actions.join('');
}

/**
 * 回显数据字典（字符串数组）
 * @param datas 字典数据
 * @param value 当前值（字符串或数组）
 * @param separator 分隔符（默认逗号）
 */
export function selectDictLabels(
  datas: Record<string, DictItem>,
  value: string | string[] | undefined,
  separator?: string
): string {
  if (value === undefined || (Array.isArray(value) && value.length === 0)) {
    return '';
  }
  const valueStr = Array.isArray(value) ? value.join(',') : value;
  const actions: string[] = [];
  const currentSeparator = separator === undefined ? ',' : separator;
  const temp = valueStr.split(currentSeparator);
  temp.forEach((item) => {
    let match = false;
    Object.keys(datas).some((key) => {
      if (datas[key].dictValue === '' + item) {
        actions.push(datas[key].dictLabel + currentSeparator);
        match = true;
      }
      return match;
    });
    if (!match) {
      actions.push(item + currentSeparator);
    }
  });
  return actions.join('').substring(0, actions.join('').length - 1);
}
