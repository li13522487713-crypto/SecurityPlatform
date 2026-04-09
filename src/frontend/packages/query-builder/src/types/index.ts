export type QueryOperator =
  | 'eq'
  | 'neq'
  | 'contains'
  | 'startsWith'
  | 'endsWith'
  | 'gt'
  | 'gte'
  | 'lt'
  | 'lte'
  | 'in'
  | 'between'

export interface QueryRule {
  field: string
  operator: QueryOperator
  value: unknown
}

export interface QueryGroup {
  relation: 'and' | 'or'
  rules: Array<QueryRule | QueryGroup>
}
