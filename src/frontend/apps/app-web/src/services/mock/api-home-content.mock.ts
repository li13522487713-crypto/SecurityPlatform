import type { PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { matchKeyword, mockPaged, mockResolve } from "./mock-utils";

/**
 * Mock：工作空间首页内容（PRD 01-首页）。
 *
 * 路由：
 *   GET  /api/v1/workspaces/{workspaceId}/home/banner
 *   GET  /api/v1/workspaces/{workspaceId}/home/tutorials
 *   GET  /api/v1/workspaces/{workspaceId}/home/announcements
 *   GET  /api/v1/workspaces/{workspaceId}/home/recommended-agents
 *   GET  /api/v1/workspaces/{workspaceId}/home/recent-activities
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

export async function getHomeBanner(_workspaceId: string): Promise<HomeBanner> {
  return mockResolve({
    heroTitle: "扣子，让 AI 离应用更近一步",
    heroSubtitle: "新一代 AI 应用开发平台 — 无需代码，轻松创建，支持发布多平台、WebSDK 及 API。",
    ctaList: [
      { key: "create", label: "立即创建" },
      { key: "tutorial", label: "查看教程" },
      { key: "docs", label: "查看文档" }
    ]
  });
}

export async function getHomeTutorials(_workspaceId: string): Promise<TutorialCard[]> {
  return mockResolve([
    {
      id: "intro",
      title: "什么是扣子",
      description: "5 分钟了解平台基础概念。",
      iconKey: "intro",
      link: "/docs/welcome"
    },
    {
      id: "quickstart",
      title: "快速开始",
      description: "跟着指引创建你的第一个智能体。",
      iconKey: "quickstart",
      link: "/docs/quick-start"
    },
    {
      id: "release",
      title: "产品动态",
      description: "查看最新功能与版本更新。",
      iconKey: "release",
      link: "/docs/release-notes"
    }
  ]);
}

export async function getHomeAnnouncements(
  _workspaceId: string,
  request: PagedRequest & { tab?: AnnouncementTab; keyword?: string }
): Promise<PagedResult<AnnouncementItem>> {
  const all: AnnouncementItem[] = [
    {
      id: "ann-1",
      title: "扣子小助手工作流模板已上线",
      summary: "官方模板帮助你快速搭建客服 / 销售助手。",
      publisher: "扣子官方",
      publishedAt: "2026-04-12T10:00:00Z",
      tag: "公告",
      link: "/docs/release-notes#tpl"
    },
    {
      id: "ann-2",
      title: "DAG 工作流引擎升级：支持批处理与续跑",
      summary: "引擎能力对齐 Coze parity，新节点支持续跑能力。",
      publisher: "工作流团队",
      publishedAt: "2026-04-10T08:00:00Z",
      link: "/docs/release-notes#dag"
    },
    {
      id: "ann-3",
      title: "新版资源库已上线",
      summary: "知识库/插件/数据库统一入口，支持工作空间维度复用。",
      publisher: "平台团队",
      publishedAt: "2026-04-05T08:00:00Z",
      tag: "公告",
      link: "/docs/release-notes#library"
    }
  ];

  const filtered = all.filter(item => {
    if (request.tab === "notice" && item.tag !== "公告") {
      return false;
    }
    return matchKeyword(item.title, request.keyword) || matchKeyword(item.summary, request.keyword);
  });

  return mockPaged(filtered, request);
}

export async function getHomeRecommendedAgents(_workspaceId: string): Promise<RecommendedAgentItem[]> {
  return mockResolve([
    {
      id: "rec-1",
      name: "秒剪短视频",
      description: "一站式视频脚本生成助手。",
      publisherName: "淘宝智能型",
      views: 220000,
      likes: 1600,
      link: "/agent/rec-1/editor"
    },
    {
      id: "rec-2",
      name: "华泰股市助手",
      description: "实时行情解读 + 投研助手。",
      publisherName: "华泰证券",
      views: 158000,
      likes: 980,
      link: "/agent/rec-2/editor"
    }
  ]);
}

export async function getHomeRecentActivities(_workspaceId: string): Promise<RecentActivityItem[]> {
  return mockResolve([
    {
      id: "recent-agent-1",
      type: "agent",
      name: "客服智能体",
      description: "上次编辑：3 小时前",
      updatedAt: new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString(),
      entryRoute: "/agent/recent-agent-1/editor"
    },
    {
      id: "recent-workflow-1",
      type: "workflow",
      name: "意图识别工作流",
      description: "上次运行：昨天 14:20",
      updatedAt: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
      entryRoute: "/workflow/recent-workflow-1/editor"
    }
  ]);
}
