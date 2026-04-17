import type { PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { matchKeyword, mockPaged } from "./mock-utils";

/**
 * Mock：作品社区（PRD 02-左侧导航 7.9）。
 *
 * 路由：
 *   GET /api/v1/community/works
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

const WORKS: CommunityWorkItem[] = [
  {
    id: "work-1",
    title: "客服小助手最佳实践",
    summary: "如何用扣子搭建一个金融行业客服智能体。",
    authorDisplayName: "扣子官方",
    likes: 1234,
    views: 45678,
    publishedAt: "2026-04-10T10:00:00Z",
    tags: ["客服", "金融"]
  },
  {
    id: "work-2",
    title: "RAG 知识库实战",
    summary: "从 0 到 1 搭建一个企业知识检索智能体。",
    authorDisplayName: "社区作者 Alex",
    likes: 856,
    views: 23456,
    publishedAt: "2026-04-05T08:00:00Z",
    tags: ["RAG", "知识库"]
  }
];

export async function listCommunityWorks(
  request: PagedRequest & { keyword?: string }
): Promise<PagedResult<CommunityWorkItem>> {
  const items = WORKS.filter(item => matchKeyword(item.title, request.keyword));
  return mockPaged(items, request);
}
