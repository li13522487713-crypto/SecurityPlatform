import type { ApiResponse } from "@/types/api";
import { requestApi } from "@/services/api-core";

export interface TemplateListItem {
  id: string;
  name: string;
  category: number;
  description: string;
  tags: string;
  version: string;
  isBuiltIn: boolean;
  updatedAt: string;
}

export async function searchTemplates(params: {
  keyword?: string;
  category?: number;
  tags?: string;
  version?: string;
  pageIndex?: number;
  pageSize?: number;
}) {
  const query = new URLSearchParams({
    pageIndex: String(params.pageIndex ?? 1),
    pageSize: String(params.pageSize ?? 20)
  });
  if (params.keyword) query.set("keyword", params.keyword);
  if (params.category !== undefined) query.set("category", String(params.category));
  if (params.tags) query.set("tags", params.tags);
  if (params.version) query.set("version", params.version);

  const response = await requestApi<ApiResponse<{
    pageIndex: number;
    pageSize: number;
    total: number;
    items: TemplateListItem[];
  }>>(`/templates?${query.toString()}`);
  if (!response.data) {
    throw new Error(response.message || "查询模板失败");
  }
  return response.data;
}

export async function instantiateTemplate(id: string) {
  const response = await requestApi<ApiResponse<{ schemaJson: string }>>(`/templates/${id}/instantiate`, {
    method: "POST"
  });
  if (!response.data) {
    throw new Error(response.message || "模板实例化失败");
  }
  return response.data;
}

export async function createTemplate(request: {
  name: string;
  category: number;
  schemaJson: string;
  description: string;
  tags: string;
  version: string;
}) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/templates", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "保存模板失败");
  }
  return response.data;
}
