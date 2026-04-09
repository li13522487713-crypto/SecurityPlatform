/**
 * 数据绑定类型定义（统一模型）。
 */

export type RuntimeBindingKind = "list" | "record" | "form" | "query";

export type RuntimeKey = string;
export type RuntimeValueMap = Record<string, unknown>;

export interface ListBinding {
  kind: "list";
  key: RuntimeKey;
  entityKey: RuntimeKey;
  queryKey?: RuntimeKey;
  filters?: unknown;
  sort?: unknown;
  pageSize?: number;
  apiUrl: string;
}

export interface RecordBinding {
  kind: "record";
  key: RuntimeKey;
  entityKey: RuntimeKey;
  idExpr: string;
  apiUrl: string;
}

export interface FormBinding {
  kind: "form";
  key: RuntimeKey;
  entityKey: RuntimeKey;
  mode: "create" | "edit" | "view";
  recordIdExpr?: string;
  initialValueExpr?: string;
  submitUrl: string;
  initUrl?: string;
}

export interface QueryBinding {
  kind: "query";
  key: RuntimeKey;
  source: "api" | "flow" | "workflow" | "agent";
  sourceKey: RuntimeKey;
  inputExpr?: string;
}

export type DataBinding = ListBinding | RecordBinding | FormBinding | QueryBinding;

export interface SchemaBindingMap {
  bindings: DataBinding[];
}

export interface RuntimeContextForBinding {
  appKey?: string;
  runtime?: RuntimeValueMap;
}
