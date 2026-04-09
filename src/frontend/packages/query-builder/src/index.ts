export * from './types'

import type { QueryGroup, QueryRule } from './types'

function normalizeRule(rule: QueryRule): QueryRule {
  return {
    field: rule.field.trim(),
    operator: rule.operator,
    value: rule.value
  }
}

export function normalizeQueryGroup(group: QueryGroup): QueryGroup {
  return {
    relation: group.relation,
    rules: group.rules.map((item) => {
      if ('rules' in item) {
        return normalizeQueryGroup(item)
      }
      return normalizeRule(item)
    })
  }
}
