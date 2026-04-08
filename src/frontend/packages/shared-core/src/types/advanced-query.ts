export enum QueryOperator {
  Equal = "eq",
  NotEqual = "ne",
  GreaterThan = "gt",
  GreaterThanOrEqual = "gte",
  LessThan = "lt",
  LessThanOrEqual = "lte",
  Like = "like",
  In = "in",
  Between = "between",
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
