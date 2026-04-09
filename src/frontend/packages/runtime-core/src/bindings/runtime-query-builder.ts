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
