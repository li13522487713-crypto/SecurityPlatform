/**
 * 数据绑定类型定义。
 *
 * 从"字符串 URL 注入"升级为模型驱动的 Binding 声明。
 */

import type { Key, RuntimePageMode } from "../types/base-types";

export interface ListBinding {
  kind: "list";
  key: Key;
  entityKey: Key;
  queryKey?: Key;
  filters?: unknown;
  sort?: unknown;
  pageSize?: number;
  apiUrl: string;
}

export interface RecordBinding {
  kind: "record";
  key: Key;
  entityKey: Key;
  idExpr: string;
  apiUrl: string;
}

export interface FormBinding {
  kind: "form";
  key: Key;
  entityKey: Key;
  mode: RuntimePageMode;
  recordIdExpr?: string;
  initialValueExpr?: string;
  submitUrl: string;
  initUrl?: string;
}

export interface QueryBinding {
  kind: "query";
  key: Key;
  source: "api" | "flow" | "workflow" | "agent";
  sourceKey: Key;
  inputExpr?: string;
}

export type DataBinding = ListBinding | RecordBinding | FormBinding | QueryBinding;

export interface SchemaBindingMap {
  bindings: DataBinding[];
}
