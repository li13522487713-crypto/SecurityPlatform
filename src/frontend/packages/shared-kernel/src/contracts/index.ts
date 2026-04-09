export type JsonPrimitive = string | number | boolean | null;
export type JsonValue = JsonPrimitive | JsonObject | JsonValue[];

export interface JsonObject {
  [key: string]: JsonValue;
}

export interface ApiResponse<T> {
  success: boolean;
  code: string;
  message: string;
  traceId: string;
  data?: T;
}

export interface PagedRequest {
  pageIndex: number;
  pageSize: number;
  keyword?: string;
  sortBy?: string;
  sortDesc?: boolean;
  departmentId?: string | number;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  pageIndex: number;
  pageSize: number;
}

export enum QueryOperator {
  Equal = "eq",
  NotEqual = "ne",
  GreaterThan = "gt",
  GreaterThanOrEqual = "gte",
  LessThan = "lt",
  LessThanOrEqual = "lte",
  Like = "like",
  In = "in",
  Between = "between"
}

export interface QueryRule {
  id: string;
  field: string;
  operator: QueryOperator | string;
  value: unknown;
}

export interface QueryGroup {
  id: string;
  conjunction: "and" | "or";
  rules?: QueryRule[];
  groups?: QueryGroup[];
}

export interface AdvancedQueryConfig {
  rootGroup: QueryGroup;
}
