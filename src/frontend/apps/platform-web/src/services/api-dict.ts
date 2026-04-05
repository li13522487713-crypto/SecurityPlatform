import type { ApiResponse, PagedResult } from "@atlas/shared-core";
import { requestApi } from "./api-core";

export interface DictTypeDto {
  id: string;
  code: string;
  name: string;
  status: boolean;
  remark?: string;
}

export interface DictDataDto {
  id: string;
  dictTypeCode: string;
  label: string;
  value: string;
  sortOrder: number;
  status: boolean;
  cssClass?: string;
  listClass?: string;
}

export async function getDictTypesPaged(params: {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
}): Promise<PagedResult<DictTypeDto>> {
  const query = new URLSearchParams();
  query.set("PageIndex", String(params.pageIndex ?? 1));
  query.set("PageSize", String(params.pageSize ?? 20));
  if (params.keyword) query.set("Keyword", params.keyword);
  const response = await requestApi<ApiResponse<PagedResult<DictTypeDto>>>(`/dict-types?${query}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function createDictType(request: { code: string; name: string; status: boolean; remark?: string }): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/dict-types", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "创建失败");
}

export async function updateDictType(id: string, request: { name: string; status: boolean; remark?: string }): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dict-types/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function deleteDictType(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dict-types/${id}`, { method: "DELETE" });
  if (!response.success) throw new Error(response.message || "删除失败");
}

export async function getDictDataPaged(
  code: string,
  params: { pageIndex?: number; pageSize?: number; keyword?: string }
): Promise<PagedResult<DictDataDto>> {
  const query = new URLSearchParams();
  query.set("PageIndex", String(params.pageIndex ?? 1));
  query.set("PageSize", String(params.pageSize ?? 20));
  if (params.keyword) query.set("Keyword", params.keyword);
  const response = await requestApi<ApiResponse<PagedResult<DictDataDto>>>(`/dict-types/${code}/data?${query}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function createDictData(code: string, request: {
  label: string; value: string; sortOrder: number; status: boolean; cssClass?: string; listClass?: string;
}): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dict-types/${code}/data`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "创建失败");
}

export async function updateDictData(id: string, request: {
  label: string; value: string; sortOrder: number; status: boolean; cssClass?: string; listClass?: string;
}): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dict-data/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function deleteDictData(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dict-data/${id}`, { method: "DELETE" });
  if (!response.success) throw new Error(response.message || "删除失败");
}
