import type { PagedRequest, PagedResult } from "@atlas/shared-react-core/types";

export type ExploreLocale = "zh-CN" | "en-US";

export interface PluginItem {
  id: number;
  name: string;
  description?: string;
  category?: string;
  status: number;
  updatedAt?: string;
}

export interface BuiltInPluginMeta {
  code: string;
  name: string;
  description: string;
  category: string;
  version: string;
  tags: string[];
}

export interface TemplateItem {
  id: string;
  name: string;
  category: number;
  description: string;
  tags: string;
  version: string;
  updatedAt: string;
}

export interface SearchItem {
  resourceType: string;
  resourceId: number;
  title: string;
  description?: string;
  path: string;
  updatedAt?: string;
}

export interface RecentEditItem {
  id: number;
  resourceType: string;
  resourceId: number;
  title: string;
  path: string;
  updatedAt: string;
}

export interface ExploreModuleApi {
  listPlugins: (request: PagedRequest, keyword?: string) => Promise<PagedResult<PluginItem>>;
  listBuiltInPlugins: () => Promise<BuiltInPluginMeta[]>;
  listTemplates: (request: PagedRequest, filters?: { keyword?: string; category?: number }) => Promise<{
    pageIndex: number;
    pageSize: number;
    total: number;
    items: TemplateItem[];
  }>;
  search: (keyword: string, limit?: number) => Promise<{ items: SearchItem[]; recentEdits: RecentEditItem[] }>;
  recent: (limit?: number) => Promise<RecentEditItem[]>;
}

export interface ExplorePageProps {
  api: ExploreModuleApi;
  locale: ExploreLocale;
}
