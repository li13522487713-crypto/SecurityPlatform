import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import { requestApi } from "@/services/api-core";
import type { LowCodeAppDetail, TenantAppInstanceListItem } from "@/types/platform-console";
import type {
  LowCodePageCreateRequest,
  LowCodePageDetail,
  LowCodePageRuntimeSchema,
  LowCodePageTreeNode,
  LowCodePageUpdateRequest,
} from "@/types/lowcode";

const V2_APP_BASE = "/api/v2/tenant-app-instances";

export async function getLowCodeAppsPaged(
  params: PagedRequest & { category?: string }
): Promise<PagedResult<TenantAppInstanceListItem>> {
  const query = new URLSearchParams({
    PageIndex: params.pageIndex.toString(),
    PageSize: params.pageSize.toString(),
    Keyword: params.keyword ?? ""
  });
  if (params.category) {
    query.set("category", params.category);
  }
  const response = await requestApi<ApiResponse<PagedResult<TenantAppInstanceListItem>>>(
    `${V2_APP_BASE}?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getLowCodeAppDetail(id: string): Promise<LowCodeAppDetail> {
  const response = await requestApi<ApiResponse<LowCodeAppDetail>>(`${V2_APP_BASE}/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getLowCodePageTree(appId: string): Promise<LowCodePageTreeNode[]> {
  const response = await requestApi<ApiResponse<LowCodePageTreeNode[]>>(
    `/lowcode-apps/${encodeURIComponent(appId)}/pages/tree`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getLowCodePageDetail(pageId: string): Promise<LowCodePageDetail> {
  const response = await requestApi<ApiResponse<LowCodePageDetail>>(
    `/lowcode-apps/pages/${encodeURIComponent(pageId)}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getLowCodeRuntimePageSchema(
  pageId: string,
  mode: "draft" | "published" = "draft",
  environmentCode?: string
): Promise<LowCodePageRuntimeSchema> {
  const query = new URLSearchParams({ mode });
  if (environmentCode) {
    query.set("environmentCode", environmentCode);
  }
  const response = await requestApi<ApiResponse<LowCodePageRuntimeSchema>>(
    `/lowcode-apps/pages/${encodeURIComponent(pageId)}/runtime?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function createLowCodePage(
  appId: string,
  request: LowCodePageCreateRequest
): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/${encodeURIComponent(appId)}/pages`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "创建失败");
  }
  return response.data;
}

export async function updateLowCodePage(pageId: string, request: LowCodePageUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/pages/${encodeURIComponent(pageId)}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function updateLowCodePageSchema(pageId: string, schemaJson: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/pages/${encodeURIComponent(pageId)}/schema`,
    {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ schemaJson })
    }
  );
  if (!response.success) {
    throw new Error(response.message || "保存失败");
  }
}

export async function publishLowCodePage(pageId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/pages/${encodeURIComponent(pageId)}/publish`,
    { method: "POST" }
  );
  if (!response.success) {
    throw new Error(response.message || "发布失败");
  }
}

export async function deleteLowCodePage(pageId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/pages/${encodeURIComponent(pageId)}`,
    { method: "DELETE" }
  );
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}
