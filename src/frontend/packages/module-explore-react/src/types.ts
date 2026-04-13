import type { PagedRequest, PagedResult } from "@atlas/shared-react-core/types";

export type ExploreLocale = "zh-CN" | "en-US";

export interface MarketplacePluginItem {
  id: number;
  name: string;
  description?: string;
  categoryName?: string;
  status: number;
  version: string;
  downloadCount: number;
  favoriteCount: number;
  isFavorited: boolean;
  sourceResourceId?: number;
  publishedAt?: string;
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
  id: number;
  name: string;
  category: number;
  description: string;
  tags: string;
  version: string;
  schemaJson?: string;
  updatedAt: string;
}

export interface MarketplacePluginDetail extends MarketplacePluginItem {
  summary?: string;
  icon?: string;
  tags: string[];
  categoryId: number;
  productType: number;
  createdAt: string;
  publisherUserId: number;
  sourcePluginName?: string;
  sourcePluginCategory?: string;
  sourcePluginApiCount?: number;
}

export interface TemplateDetail extends TemplateItem {
  isBuiltIn: boolean;
  createdAt: string;
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
  listPlugins: (request: PagedRequest, keyword?: string) => Promise<PagedResult<MarketplacePluginItem>>;
  getPluginDetail: (productId: number) => Promise<MarketplacePluginDetail>;
  favoritePlugin: (productId: number) => Promise<void>;
  unfavoritePlugin: (productId: number) => Promise<void>;
  importPluginToStudio: (productId: number) => Promise<{ importedPluginId: number; route: string }>;
  listBuiltInPlugins: () => Promise<BuiltInPluginMeta[]>;
  listTemplates: (request: PagedRequest, filters?: { keyword?: string; category?: number }) => Promise<{
    pageIndex: number;
    pageSize: number;
    total: number;
    items: TemplateItem[];
  }>;
  getTemplateDetail: (templateId: number) => Promise<TemplateDetail>;
  createWorkflowFromTemplate: (templateId: number) => Promise<{ workflowId: string; mode: "workflow" | "chatflow"; route: string }>;
  search: (keyword: string, limit?: number) => Promise<{ items: SearchItem[]; recentEdits: RecentEditItem[] }>;
  recent: (limit?: number) => Promise<RecentEditItem[]>;
}

export interface ExplorePageProps {
  api: ExploreModuleApi;
  locale: ExploreLocale;
}
