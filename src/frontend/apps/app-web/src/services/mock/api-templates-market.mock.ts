import type { PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { matchKeyword, mockPaged } from "./mock-utils";

/**
 * Mock：模板/插件商店一级导航的“摘要列表”（PRD 02-左侧导航 7.7、7.8）。
 *
 * 注意：进入完整模板/插件库仍走真实 `getTemplatesPaged` / `getMarketplaceProductsPaged`，
 * 这里仅提供新一级菜单页面的“分类摘要 + 推荐入口”所需数据。
 *
 * 路由（mock 阶段）：
 *   GET /api/v1/market/templates/summary
 *   GET /api/v1/market/plugins/summary
 */

export interface MarketCategorySummary {
  id: string;
  name: string;
  count: number;
  description?: string;
}

const TEMPLATE_CATEGORIES: MarketCategorySummary[] = [
  { id: "agent", name: "智能体模板", count: 12, description: "客服/营销/咨询场景" },
  { id: "workflow", name: "工作流模板", count: 28, description: "RAG / 多轮问答 / 数据处理" },
  { id: "app", name: "应用模板", count: 5, description: "面向终端用户的应用模板" }
];

const PLUGIN_CATEGORIES: MarketCategorySummary[] = [
  { id: "search", name: "搜索类", count: 6 },
  { id: "office", name: "办公类", count: 9 },
  { id: "data", name: "数据类", count: 11 }
];

export async function listTemplateCategories(
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<MarketCategorySummary>> {
  const items = TEMPLATE_CATEGORIES.filter(item => matchKeyword(item.name, request.keyword));
  return mockPaged(items, request);
}

export async function listPluginCategories(
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<MarketCategorySummary>> {
  const items = PLUGIN_CATEGORIES.filter(item => matchKeyword(item.name, request.keyword));
  return mockPaged(items, request);
}
