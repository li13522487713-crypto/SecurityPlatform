/**
 * 数据绑定类型定义。
 *
 * 从"字符串 URL 注入"升级为模型驱动的 Binding 声明。
 * Phase 1 先实现基础 List/Record/Form binding，
 * Phase 2 扩展 filter/sort/pagination 参数化。
 */

export interface ListBinding {
  kind: "list";
  entityKey: string;
  queryKey?: string;
  filters?: unknown;
  pageSize?: number;
  apiUrl: string;
}

export interface RecordBinding {
  kind: "record";
  entityKey: string;
  idExpr: string;
  apiUrl: string;
}

export interface FormBinding {
  kind: "form";
  entityKey: string;
  mode: "create" | "edit" | "view";
  recordIdExpr?: string;
  submitUrl: string;
  initUrl?: string;
}

export type DataBinding = ListBinding | RecordBinding | FormBinding;

export interface SchemaBindingMap {
  bindings: DataBinding[];
}
