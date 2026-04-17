import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi, toQuery } from "../api-core";

/**
 * 工作空间首页内容（PRD 01）。
 *
 * 第三阶段 M1（2026-04）切换为真实 REST 接口，后台实现：
 *   Atlas.PlatformHost/Controllers/HomeContentController.cs
 *   Atlas.Infrastructure/Services/Coze/InMemoryHomeContentService.cs
 *
 * 后端尚为 in-memory 数据源；待 PlatformContent 表落地后无需改前端。
 */

export interface HomeBannerCta {
  key: "create" | "tutorial" | "docs";
  label: string;
}

export interface HomeBanner {
  heroTitle: string;
  heroSubtitle: string;
  ctaList: HomeBannerCta[];
  backgroundImageUrl?: string;
}

export interface TutorialCard {
  id: string;
  title: string;
  description: string;
  iconKey: "intro" | "quickstart" | "release";
  link: string;
}

export type AnnouncementTab = "all" | "notice";

export interface AnnouncementItem {
  id: string;
  title: string;
  summary: string;
  publisher: string;
  publishedAt: string;
  tag?: string;
  link: string;
}

export interface RecommendedAgentItem {
  id: string;
  name: string;
  description: string;
  iconUrl?: string;
  publisherName: string;
  views: number;
  likes: number;
  link: string;
}

export type RecentActivityType = "agent" | "app" | "workflow";

export interface RecentActivityItem {
  id: string;
  type: RecentActivityType;
  name: string;
  description?: string;
  updatedAt: string;
  entryRoute: string;
}

function homeBase(workspaceId: string): string {
  return `/workspaces/${encodeURIComponent(workspaceId)}/home`;
}

export async function getHomeBanner(workspaceId: string): Promise<HomeBanner> {
  const response = await requestApi<ApiResponse<HomeBanner>>(`${homeBase(workspaceId)}/banner`);
  if (!response.data) {
    throw new Error(response.message || "Failed to load banner");
  }
  return response.data;
}

export async function getHomeTutorials(workspaceId: string): Promise<TutorialCard[]> {
  const response = await requestApi<ApiResponse<TutorialCard[]>>(`${homeBase(workspaceId)}/tutorials`);
  if (!response.data) {
    throw new Error(response.message || "Failed to load tutorials");
  }
  return response.data;
}

export async function getHomeAnnouncements(
  workspaceId: string,
  request: PagedRequest & { tab?: AnnouncementTab; keyword?: string }
): Promise<PagedResult<AnnouncementItem>> {
  const query = toQuery(
    {
      pageIndex: request.pageIndex ?? 1,
      pageSize: request.pageSize ?? 10
    },
    {
      tab: request.tab,
      keyword: request.keyword
    }
  );
  const response = await requestApi<ApiResponse<PagedResult<AnnouncementItem>>>(
    `${homeBase(workspaceId)}/announcements?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "Failed to load announcements");
  }
  return response.data;
}

export async function getHomeRecommendedAgents(workspaceId: string): Promise<RecommendedAgentItem[]> {
  const response = await requestApi<ApiResponse<RecommendedAgentItem[]>>(
    `${homeBase(workspaceId)}/recommended-agents`
  );
  if (!response.data) {
    throw new Error(response.message || "Failed to load recommended agents");
  }
  return response.data;
}

export async function getHomeRecentActivities(workspaceId: string): Promise<RecentActivityItem[]> {
  const response = await requestApi<ApiResponse<RecentActivityItem[]>>(
    `${homeBase(workspaceId)}/recent-activities`
  );
  return response.data ?? [];
}
