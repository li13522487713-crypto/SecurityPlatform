import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import { requestApi } from "@/services/api-core";
import type {
  ReleaseCenterDetail,
  ReleaseCenterListItem,
  ReleaseDiffSummary,
  ReleaseImpactSummary,
  ReleaseRollbackResult
} from "@/types/platform-console";

const RELEASE_CENTER_BASE = "/api/v2/release-center/releases";

export async function getReleaseCenterPaged(
  request: PagedRequest & { status?: string; appKey?: string; manifestId?: number }
): Promise<PagedResult<ReleaseCenterListItem>> {
  const query = new URLSearchParams({
    pageIndex: request.pageIndex.toString(),
    pageSize: request.pageSize.toString()
  });
  if (request.keyword) {
    query.set("keyword", request.keyword);
  }
  if (request.status) {
    query.set("status", request.status);
  }
  if (request.appKey) {
    query.set("appKey", request.appKey);
  }
  if (request.manifestId) {
    query.set("manifestId", request.manifestId.toString());
  }

  const response = await requestApi<ApiResponse<PagedResult<ReleaseCenterListItem>>>(
    `${RELEASE_CENTER_BASE}?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询发布中心列表失败");
  }

  return response.data;
}

export async function getReleaseCenterDetail(releaseId: string): Promise<ReleaseCenterDetail> {
  const response = await requestApi<ApiResponse<ReleaseCenterDetail>>(`${RELEASE_CENTER_BASE}/${releaseId}`);
  if (!response.data) {
    throw new Error(response.message || "查询发布详情失败");
  }

  return response.data;
}

export async function getReleaseCenterDiff(releaseId: string): Promise<ReleaseDiffSummary> {
  const response = await requestApi<ApiResponse<ReleaseDiffSummary>>(`${RELEASE_CENTER_BASE}/${releaseId}/diff`);
  if (!response.data) {
    throw new Error(response.message || "查询发布差异失败");
  }

  return response.data;
}

export async function getReleaseCenterImpact(releaseId: string): Promise<ReleaseImpactSummary> {
  const response = await requestApi<ApiResponse<ReleaseImpactSummary>>(`${RELEASE_CENTER_BASE}/${releaseId}/impact`);
  if (!response.data) {
    throw new Error(response.message || "查询发布影响范围失败");
  }

  return response.data;
}

export async function rollbackReleaseCenter(releaseId: string): Promise<ReleaseRollbackResult> {
  const response = await requestApi<ApiResponse<ReleaseRollbackResult>>(`${RELEASE_CENTER_BASE}/${releaseId}/rollback`, {
    method: "POST"
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "发布回滚失败");
  }

  return response.data;
}
