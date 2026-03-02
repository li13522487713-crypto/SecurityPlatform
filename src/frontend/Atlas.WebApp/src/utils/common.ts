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

/**
 * 构造树型结构数据
 * @param {*} data 数据源
 * @param {*} id id字段 默认 'id'
 * @param {*} parentId 父节点字段 默认 'parentId'
 * @param {*} children 孩子节点字段 默认 'children'
 */
export function handleTree(data: any[], id?: string, parentId?: string, children?: string) {
  const config = {
    id: id || 'id',
    parentId: parentId || 'parentId',
    childrenList: children || 'children'
  }

  const childrenListMap: any = {}
  const nodeIds: any = {}
  const tree: any[] = []

  for (const d of data) {
    const parentId = d[config.parentId]
    if (childrenListMap[parentId] == null) {
      childrenListMap[parentId] = []
    }
    nodeIds[d[config.id]] = d
    childrenListMap[parentId].push(d)
  }

  for (const d of data) {
    const parentId = d[config.parentId]
    if (nodeIds[parentId] == null) {
      tree.push(d)
    }
  }

  for (const t of tree) {
    adaptToChildrenList(t)
  }

  function adaptToChildrenList(o: any) {
    if (childrenListMap[o[config.id]] !== null) {
      o[config.childrenList] = childrenListMap[o[config.id]]
    }
    if (o[config.childrenList]) {
      for (const c of o[config.childrenList]) {
        adaptToChildrenList(c)
      }
    }
  }
  return tree
}

/**
 * 添加日期范围
 * @param params
 * @param dateRange
 * @param propName
 */
export function addDateRange(params: any, dateRange: any[], propName?: string) {
  const search = params
  search.params = typeof search.params === 'object' && search.params !== null && !Array.isArray(search.params) ? search.params : {}
  dateRange = Array.isArray(dateRange) ? dateRange : []
  if (typeof propName === 'undefined') {
    search.params['beginTime'] = dateRange[0]
    search.params['endTime'] = dateRange[1]
  } else {
    search.params['begin' + propName] = dateRange[0]
    search.params['end' + propName] = dateRange[1]
  }
  return search
}

/**
 * 回显数据字典
 * @param datas
 * @param value
 */
export function selectDictLabel(datas: any, value: any) {
  if (value === undefined) {
    return ''
  }
  const actions = []
  Object.keys(datas).some((key) => {
    if (datas[key].dictValue === '' + value) {
      actions.push(datas[key].dictLabel)
      return true
    }
  })
  if (actions.length === 0) {
    actions.push(value)
  }
  return actions.join('')
}

/**
 * 回显数据字典（字符串数组）
 * @param datas
 * @param value
 * @param separator
 */
export function selectDictLabels(datas: any, value: any, separator?: string) {
  if (value === undefined || value.length === 0) {
    return ''
  }
  if (Array.isArray(value)) {
    value = value.join(',')
  }
  const actions: any[] = []
  const currentSeparator = separator === undefined ? ',' : separator
  const temp = value.split(currentSeparator)
  Object.keys(value.split(currentSeparator)).some((val) => {
    let match = false
    Object.keys(datas).some((key) => {
      if (datas[key].dictValue === '' + temp[val]) {
        actions.push(datas[key].dictLabel + currentSeparator)
        match = true
      }
    })
    if (!match) {
      actions.push(temp[val] + currentSeparator)
    }
  })
  return actions.join('').substring(0, actions.join('').length - 1)
}
