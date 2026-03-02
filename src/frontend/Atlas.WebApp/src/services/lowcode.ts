import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import type {
  FormDefinitionListItem,
  FormDefinitionDetail,
  FormDefinitionCreateRequest,
  FormDefinitionUpdateRequest,
  LowCodeAppListItem,
  LowCodeAppDetail,
  LowCodeAppCreateRequest,
  LowCodeAppUpdateRequest,
  LowCodePageCreateRequest,
  LowCodePageUpdateRequest
} from "@/types/lowcode";
import { requestApi } from "@/services/api";

// ─── 表单定义 API ───

export async function getFormDefinitionsPaged(
  params: PagedRequest & { category?: string }
): Promise<PagedResult<FormDefinitionListItem>> {
  const query = new URLSearchParams({
    pageIndex: params.pageIndex.toString(),
    pageSize: params.pageSize.toString(),
    keyword: params.keyword ?? ""
  });
  if (params.category) {
    query.set("category", params.category);
  }
  const response = await requestApi<ApiResponse<PagedResult<FormDefinitionListItem>>>(
    `/form-definitions?${query.toString()}`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getFormDefinitionDetail(id: string): Promise<FormDefinitionDetail> {
  const response = await requestApi<ApiResponse<FormDefinitionDetail>>(
    `/form-definitions/${id}`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function createFormDefinition(
  request: FormDefinitionCreateRequest
): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/form-definitions", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) throw new Error(response.message || "创建失败");
  return response.data;
}

export async function updateFormDefinition(
  id: string,
  request: FormDefinitionUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/form-definitions/${id}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function updateFormDefinitionSchema(
  id: string,
  schemaJson: string
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/form-definitions/${id}/schema`,
    {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ schemaJson })
    }
  );
  if (!response.success) throw new Error(response.message || "保存 Schema 失败");
}

export async function publishFormDefinition(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/form-definitions/${id}/publish`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "发布失败");
}

export async function disableFormDefinition(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/form-definitions/${id}/disable`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "停用失败");
}

export async function enableFormDefinition(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/form-definitions/${id}/enable`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "启用失败");
}

export async function deleteFormDefinition(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/form-definitions/${id}`,
    { method: "DELETE" }
  );
  if (!response.success) throw new Error(response.message || "删除失败");
}

// ─── 低代码应用 API ───

export async function getLowCodeAppsPaged(
  params: PagedRequest & { category?: string }
): Promise<PagedResult<LowCodeAppListItem>> {
  const query = new URLSearchParams({
    pageIndex: params.pageIndex.toString(),
    pageSize: params.pageSize.toString(),
    keyword: params.keyword ?? ""
  });
  if (params.category) {
    query.set("category", params.category);
  }
  const response = await requestApi<ApiResponse<PagedResult<LowCodeAppListItem>>>(
    `/lowcode-apps?${query.toString()}`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getLowCodeAppDetail(id: string): Promise<LowCodeAppDetail> {
  const response = await requestApi<ApiResponse<LowCodeAppDetail>>(
    `/lowcode-apps/${id}`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getLowCodeAppByKey(appKey: string): Promise<LowCodeAppDetail> {
  const response = await requestApi<ApiResponse<LowCodeAppDetail>>(
    `/lowcode-apps/by-key/${encodeURIComponent(appKey)}`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function createLowCodeApp(
  request: LowCodeAppCreateRequest
): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/lowcode-apps", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) throw new Error(response.message || "创建失败");
  return response.data;
}

export async function updateLowCodeApp(
  id: string,
  request: LowCodeAppUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/${id}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function publishLowCodeApp(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/${id}/publish`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "发布失败");
}

export async function deleteLowCodeApp(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/${id}`,
    { method: "DELETE" }
  );
  if (!response.success) throw new Error(response.message || "删除失败");
}

// ─── 低代码页面 API ───

export async function createLowCodePage(
  appId: string,
  request: LowCodePageCreateRequest
): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/${appId}/pages`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) throw new Error(response.message || "创建失败");
  return response.data;
}

export async function updateLowCodePage(
  pageId: string,
  request: LowCodePageUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/pages/${pageId}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function updateLowCodePageSchema(
  pageId: string,
  schemaJson: string
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/pages/${pageId}/schema`,
    {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ schemaJson })
    }
  );
  if (!response.success) throw new Error(response.message || "保存 Schema 失败");
}

export async function publishLowCodePage(pageId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/pages/${pageId}/publish`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "发布失败");
}

export async function deleteLowCodePage(pageId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/pages/${pageId}`,
    { method: "DELETE" }
  );
  if (!response.success) throw new Error(response.message || "删除失败");
}
