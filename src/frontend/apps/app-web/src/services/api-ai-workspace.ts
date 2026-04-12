import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi, toQuery } from "./api-core";
import type { AiLibraryItem, ResourceType } from "@atlas/library-module-react";

interface AiWorkspaceLibraryResult {
  items: AiLibraryItem[];
  total: number;
  pageIndex: number;
  pageSize: number;
}

export async function getLibraryPaged(request: PagedRequest, resourceType?: ResourceType) {
  const query = toQuery(request, { resourceType });
  const response = await requestApi<ApiResponse<AiWorkspaceLibraryResult>>(`/ai-workspaces/library?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询资源库失败");
  }

  return {
    items: response.data.items,
    total: response.data.total,
    pageIndex: response.data.pageIndex,
    pageSize: response.data.pageSize
  } satisfies PagedResult<AiLibraryItem>;
}
