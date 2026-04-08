/**
 * 查询参数构建器（Phase 2 扩展用）。
 *
 * 从 binding 声明 + RuntimeContext 构建 API 查询参数。
 */

import type { ListBinding } from "./binding-types";

export interface ResolvedQuery {
  url: string;
  params: Record<string, string>;
}

export function buildQueryFromBinding(binding: ListBinding): ResolvedQuery {
  const params: Record<string, string> = {};
  if (binding.pageSize) {
    params.pageSize = binding.pageSize.toString();
  }
  return {
    url: binding.apiUrl,
    params,
  };
}
