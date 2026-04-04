/**
 * Platform API Core
 *
 * 平台级 API 客户端，baseURL = /api/v1，面向 PlatformHost 接口。
 * 复用 api-core.ts 中的底层请求能力，限定平台上下文。
 */
import { requestApi, type RequestOptions, API_BASE } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";

export const PLATFORM_API_BASE = API_BASE;

export async function platformRequest<T>(
  path: string,
  init?: RequestInit,
  options?: RequestOptions,
): Promise<T> {
  return requestApi<T>(path, init, options);
}

export async function platformGet<T>(
  path: string,
  options?: RequestOptions,
): Promise<T> {
  const response = await platformRequest<ApiResponse<T>>(path, undefined, options);
  if (!response.data) {
    throw new Error(response.message || "请求失败");
  }
  return response.data;
}

export async function platformPost<T>(
  path: string,
  body: unknown,
  options?: RequestOptions,
): Promise<T> {
  const response = await platformRequest<ApiResponse<T>>(
    path,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    },
    options,
  );
  if (!response.data) {
    throw new Error(response.message || "请求失败");
  }
  return response.data;
}

export async function platformPut<T>(
  path: string,
  body: unknown,
  options?: RequestOptions,
): Promise<T> {
  const response = await platformRequest<ApiResponse<T>>(
    path,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    },
    options,
  );
  if (!response.data) {
    throw new Error(response.message || "请求失败");
  }
  return response.data;
}

export async function platformDelete(
  path: string,
  options?: RequestOptions,
): Promise<void> {
  await platformRequest<ApiResponse<unknown>>(path, { method: "DELETE" }, options);
}

export async function platformPagedQuery<T>(
  path: string,
  pagedRequest: PagedRequest,
  extra?: Record<string, string | undefined>,
  options?: RequestOptions,
): Promise<PagedResult<T>> {
  const params = new URLSearchParams();
  params.set("pageIndex", String(pagedRequest.pageIndex ?? 1));
  params.set("pageSize", String(pagedRequest.pageSize ?? 10));
  if (extra) {
    for (const [key, value] of Object.entries(extra)) {
      if (value !== undefined) {
        params.set(key, value);
      }
    }
  }

  const url = `${path}?${params.toString()}`;
  const response = await platformRequest<ApiResponse<PagedResult<T>>>(url, undefined, options);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}
