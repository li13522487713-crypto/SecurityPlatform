/**
 * 【数据展示 III-3.3 CRUD】
 * CRUD 全特性 Schema 工厂：sortable/filterable/分页/批量/quickEdit/导出/列显隐/headerToolbar+footerToolbar
 */
import type { AmisSchema } from "@/types/amis";

/** CRUD 列定义 */
export interface CrudColumn {
  name: string;
  label: string;
  type?: string;
  sortable?: boolean;
  searchable?: boolean;
  toggled?: boolean;
  width?: number | string;
  remark?: string;
  quickEdit?: boolean | Record<string, unknown>;
  copyable?: boolean;
  filterable?: boolean | Record<string, unknown>;
  fixed?: "left" | "right";
  className?: string;
  [key: string]: unknown;
}

/** CRUD Schema 选项 */
export interface CrudSchemaOptions {
  /** 数据接口 */
  api: string | Record<string, unknown>;
  /** 列定义 */
  columns: CrudColumn[];
  /** 主键字段（默认 'id'） */
  primaryField?: string;
  /** 筛选条件表单 */
  filter?: AmisSchema;
  /** 是否开启分页（默认 true） */
  pagination?: boolean;
  /** 每页条数（默认 20） */
  perPage?: number;
  /** 是否默认可排序 */
  defaultSortField?: string;
  defaultSortDir?: "asc" | "desc";
  /** 是否开启批量操作 */
  bulkActions?: AmisSchema[];
  /** 行操作按钮 */
  itemActions?: AmisSchema[];
  /** 是否可勾选行 */
  checkOnItemClick?: boolean;
  /** 是否保持选中项（翻页后） */
  keepItemSelectionOnPageChange?: boolean;
  /** 顶部工具栏 */
  headerToolbar?: Array<string | AmisSchema>;
  /** 底部工具栏 */
  footerToolbar?: Array<string | AmisSchema>;
  /** 是否显示序号 */
  showIndex?: boolean;
  /** 自适应宽度 */
  autoFillHeight?: boolean;
  /** 额外属性 */
  [key: string]: unknown;
}

/**
 * 创建 CRUD Schema
 *
 * @example
 * ```ts
 * crudSchema({
 *   api: '/api/v1/users',
 *   columns: [
 *     { name: 'id', label: 'ID', sortable: true },
 *     { name: 'name', label: '姓名', searchable: true },
 *     { name: 'email', label: '邮箱', copyable: true },
 *     { name: 'status', label: '状态', quickEdit: { type: 'switch' } },
 *   ],
 *   filter: formSchema({ body: [inputText({ name: 'keyword', label: '搜索' })] }),
 *   bulkActions: [ajaxAction({ label: '批量删除', api: 'DELETE:/api/v1/users/${ids}' })],
 * })
 * ```
 */
export function crudSchema(opts: CrudSchemaOptions): AmisSchema {
  const { api, columns, primaryField, filter, pagination, perPage, defaultSortField, defaultSortDir, bulkActions, itemActions, checkOnItemClick, keepItemSelectionOnPageChange, headerToolbar, footerToolbar, showIndex, autoFillHeight, ...rest } = opts;

  // 如果有行操作按钮，自动添加操作列
  const finalColumns = [...columns];
  if (itemActions && itemActions.length > 0) {
    finalColumns.push({
      name: "_operation",
      label: "操作",
      type: "operation",
      buttons: itemActions,
    } as CrudColumn);
  }

  return {
    type: "crud",
    api,
    columns: finalColumns,
    ...(primaryField ? { primaryField } : {}),
    ...(filter ? { filter } : {}),
    ...(pagination !== false ? {} : { loadDataOnce: true }),
    ...(perPage ? { perPage } : { perPage: 20 }),
    ...(defaultSortField ? { defaultSort: { field: defaultSortField, dir: defaultSortDir ?? "asc" } } : {}),
    ...(bulkActions ? { bulkActions } : {}),
    ...(checkOnItemClick ? { checkOnItemClick: true } : {}),
    ...(keepItemSelectionOnPageChange ? { keepItemSelectionOnPageChange: true } : {}),
    headerToolbar: headerToolbar ?? ["bulkActions", "export-excel", "columns-toggler", "filter-toggler"],
    footerToolbar: footerToolbar ?? ["statistics", "switch-per-page", "pagination"],
    ...(showIndex ? { showIndex: true } : {}),
    ...(autoFillHeight ? { autoFillHeight: true } : {}),
    ...rest,
  };
}

/** 创建可排序列 */
export function sortableColumn(name: string, label: string, extra: Partial<CrudColumn> = {}): CrudColumn {
  return { name, label, sortable: true, ...extra };
}

/** 创建可搜索列 */
export function searchableColumn(name: string, label: string, extra: Partial<CrudColumn> = {}): CrudColumn {
  return { name, label, searchable: true, ...extra };
}

/** 创建快速编辑列 */
export function quickEditColumn(name: string, label: string, editConfig: Record<string, unknown> = { type: "input-text" }, extra: Partial<CrudColumn> = {}): CrudColumn {
  return { name, label, quickEdit: editConfig, ...extra };
}

/** 创建可复制列 */
export function copyableColumn(name: string, label: string, extra: Partial<CrudColumn> = {}): CrudColumn {
  return { name, label, copyable: true, ...extra };
}
