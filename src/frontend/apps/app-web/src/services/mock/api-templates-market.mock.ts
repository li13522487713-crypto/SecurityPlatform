import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi, toQuery } from "../api-core";

/**
 * 模板/插件商店分类摘要（PRD 02-7.7、7.8）。已切换为真实 REST：
 *   Atlas.PlatformHost/Controllers/MarketSummaryController.cs
 *   Atlas.Infrastructure/Services/Coze/InMemoryMarketSummaryService.cs
 *
 * 完整模板/插件搜索仍走 TemplatesController / AiMarketplaceController。
 */

export interface MarketCategorySummary {
  id: string;
  name: string;
  count: number;
  description?: string;
}

async function fetchSummary(
  endpoint: string,
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<MarketCategorySummary>> {
  const query = toQuery(
    {
      pageIndex: request.pageIndex ?? 1,
      pageSize: request.pageSize ?? 20
    },
    { keyword: request.keyword }
  );
  const response = await requestApi<ApiResponse<PagedResult<MarketCategorySummary>>>(`${endpoint}?${query}`);
  if (!response.data) {
    throw new Error(response.message || "Failed to load market summary");
  }
  return response.data;
}

export async function listTemplateCategories(
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<MarketCategorySummary>> {
  return fetchSummary("/market/templates/summary", request);
}

export async function listPluginCategories(
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<MarketCategorySummary>> {
  return fetchSummary("/market/plugins/summary", request);
}
