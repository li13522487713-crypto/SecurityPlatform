import { requestApi } from "@/services/api-core";
import type { ApiResponse, PagedResult } from "@/types/api";

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

export interface DictTypeCreateRequest {
  code: string;
  name: string;
  status: boolean;
  remark?: string;
}

export interface DictTypeUpdateRequest {
  name: string;
  status: boolean;
  remark?: string;
}

export interface DictDataCreateRequest {
  label: string;
  value: string;
  sortOrder: number;
  status: boolean;
  cssClass?: string;
  listClass?: string;
}

export interface DictDataUpdateRequest {
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
  query.set("pageIndex", String(params.pageIndex ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  if (params.keyword) query.set("keyword", params.keyword);
  const response = await requestApi<ApiResponse<PagedResult<DictTypeDto>>>(`/dict-types?${query}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getAllActiveDictTypes(): Promise<DictTypeDto[]> {
  const response = await requestApi<ApiResponse<DictTypeDto[]>>("/dict-types/all");
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getDictDataPaged(
  code: string,
  params: { pageIndex?: number; pageSize?: number; keyword?: string }
): Promise<PagedResult<DictDataDto>> {
  const query = new URLSearchParams();
  query.set("pageIndex", String(params.pageIndex ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  if (params.keyword) query.set("keyword", params.keyword);
  const response = await requestApi<ApiResponse<PagedResult<DictDataDto>>>(`/dict-types/${code}/data?${query}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getActiveDictDataByCode(code: string): Promise<DictDataDto[]> {
  const response = await requestApi<ApiResponse<DictDataDto[]>>(`/dict-data/by-code/${code}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function createDictType(request: DictTypeCreateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/dict-types", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "创建失败");
}

export async function updateDictType(id: string, request: DictTypeUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dict-types/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function deleteDictType(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dict-types/${id}`, {
    method: "DELETE"
  });
  if (!response.success) throw new Error(response.message || "删除失败");
}

export async function createDictData(code: string, request: DictDataCreateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dict-types/${code}/data`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "创建失败");
}

export async function updateDictData(id: string, request: DictDataUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dict-data/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function deleteDictData(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dict-data/${id}`, {
    method: "DELETE"
  });
  if (!response.success) throw new Error(response.message || "删除失败");
}
