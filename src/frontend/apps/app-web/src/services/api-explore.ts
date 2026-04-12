import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi, toQuery } from "./api-core";

export type AiPluginType = 0 | 1;
export type AiPluginStatus = 0 | 1;
export type AiPluginSourceType = 0 | 1 | 2;
export type AiPluginAuthType = 0 | 1 | 2 | 3 | 4;

export interface AiPluginListItem {
  id: number;
  name: string;
  description?: string;
  icon?: string;
  category?: string;
  type: AiPluginType;
  sourceType: AiPluginSourceType;
  authType: AiPluginAuthType;
  status: AiPluginStatus;
  isLocked: boolean;
  createdAt: string;
  updatedAt?: string;
  publishedAt?: string;
}

export interface AiPluginBuiltInMetaItem {
  code: string;
  name: string;
  description: string;
  category: string;
  version: string;
  tags: string[];
}

export interface TemplateListItem {
  id: string;
  name: string;
  category: number;
  schemaJson: string;
  description: string;
  tags: string;
  isBuiltIn: boolean;
  version: string;
  createdAt: string;
  updatedAt: string;
}

export interface TemplateSearchResponse {
  pageIndex: number;
  pageSize: number;
  total: number;
  items: TemplateListItem[];
}

export interface AiSearchResultItem {
  resourceType: string;
  resourceId: number;
  title: string;
  description?: string;
  path: string;
  updatedAt?: string;
}

export interface AiRecentEditItem {
  id: number;
  resourceType: string;
  resourceId: number;
  title: string;
  path: string;
  updatedAt: string;
}

export interface AiSearchResponse {
  items: AiSearchResultItem[];
  recentEdits: AiRecentEditItem[];
}

export async function getAiPluginsPaged(request: PagedRequest, keyword?: string): Promise<PagedResult<AiPluginListItem>> {
  const response = await requestApi<ApiResponse<PagedResult<AiPluginListItem>>>(`/ai-plugins?${toQuery(request, { keyword })}`);
  if (!response.data) {
    throw new Error(response.message || "查询插件失败");
  }

  return response.data;
}

export async function getAiPluginBuiltInMetadata(): Promise<AiPluginBuiltInMetaItem[]> {
  const response = await requestApi<ApiResponse<AiPluginBuiltInMetaItem[]>>("/ai-plugins/built-in-metadata");
  if (!response.data) {
    throw new Error(response.message || "查询插件内置元数据失败");
  }

  return response.data;
}

export async function getTemplatesPaged(
  request: PagedRequest,
  filters?: { keyword?: string; category?: number }
): Promise<TemplateSearchResponse> {
  const response = await requestApi<ApiResponse<{
    pageIndex: number;
    pageSize: number;
    total: number;
    items: TemplateListItem[];
  }>>(`/templates?${toQuery(request, {
    keyword: filters?.keyword,
    category: filters?.category !== undefined ? String(filters.category) : undefined
  })}`);

  if (!response.data) {
    throw new Error(response.message || "查询模板失败");
  }

  return {
    pageIndex: response.data.pageIndex,
    pageSize: response.data.pageSize,
    total: response.data.total,
    items: response.data.items
  };
}

export async function searchAi(keyword: string, limit = 20): Promise<AiSearchResponse> {
  const response = await requestApi<ApiResponse<AiSearchResponse>>(`/ai-search?${toQuery({ pageIndex: 1, pageSize: limit }, {
    keyword,
    limit: String(limit)
  })}`);
  if (!response.data) {
    throw new Error(response.message || "搜索失败");
  }

  return response.data;
}

export async function getRecentAiEdits(limit = 20): Promise<AiRecentEditItem[]> {
  const response = await requestApi<ApiResponse<AiRecentEditItem[]>>(`/ai-search/recent?limit=${limit}`);
  if (!response.data) {
    throw new Error(response.message || "查询最近编辑失败");
  }

  return response.data;
}
