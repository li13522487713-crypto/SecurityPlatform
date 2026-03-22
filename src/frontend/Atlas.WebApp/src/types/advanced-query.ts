// src/types/advanced-query.ts

export enum QueryOperator {
  Equal = 'eq',
  NotEqual = 'ne',
  GreaterThan = 'gt',
  GreaterThanOrEqual = 'gte',
  LessThan = 'lt',
  LessThanOrEqual = 'lte',
  Like = 'like',
  In = 'in',
  Between = 'between'
}

export interface QueryRule {
  id: string;      // Frontend only unique identifier for v-for loops
  field: string;
  operator: QueryOperator | string;
  value: any;
}

export interface QueryGroup {
  id: string;      // Frontend only unique identifier
  conjunction: 'and' | 'or';
  rules?: QueryRule[];
  groups?: QueryGroup[];
}

export interface AdvancedQueryConfig {
  rootGroup: QueryGroup;
}
