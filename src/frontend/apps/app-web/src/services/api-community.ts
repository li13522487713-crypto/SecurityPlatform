import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi, toQuery } from "./api-core";

/**
 * 作品社区真实服务。
 */

export interface CommunityWorkItem {
  id: string;
  title: string;
  summary: string;
  authorDisplayName: string;
  coverUrl?: string;
  likes: number;
  views: number;
  publishedAt: string;
  tags: string[];
}

export async function listCommunityWorks(
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<CommunityWorkItem>> {
  const query = toQuery(
    {
      pageIndex: request.pageIndex ?? 1,
      pageSize: request.pageSize ?? 20
    },
    { keyword: request.keyword }
  );
  const response = await requestApi<ApiResponse<PagedResult<CommunityWorkItem>>>(
    `/community/works?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "Failed to load community works");
  }
  return response.data;
}
