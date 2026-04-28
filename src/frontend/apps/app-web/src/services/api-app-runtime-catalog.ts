/**
 * 应用运行时目录与页面 schema API（经 AppHost 运行时）。
 */
import type { ApiResponse, PagedResult } from "@atlas/shared-react-core/types";
import type { RuntimePageSchema } from "../types/runtime-page-schema";
import { requestApi } from "./api-core";

/** 与 `GET /api/v2/application-catalogs` 列表项对齐（camelCase）。 */
export interface ApplicationCatalogListItemDto {
  id: string;
  catalogKey: string;
  name: string;
  status: string;
  version: number;
  description?: string | null;
  category?: string | null;
  icon?: string | null;
  publishedAt?: string | null;
  isBound: boolean;
}

export async function getRuntimePageSchemaByAppAndPageKey(
  appKey: string,
  pageKey: string,
  environmentCode?: string
): Promise<RuntimePageSchema> {
  const query = new URLSearchParams();
  if (environmentCode) {
    query.set("environmentCode", environmentCode);
  }
  const queryText = query.toString();
  const response = await requestApi<ApiResponse<RuntimePageSchema>>(
    `/api/v1/runtime/apps/${encodeURIComponent(appKey)}/pages/${encodeURIComponent(pageKey)}/schema${
      queryText ? `?${queryText}` : ""
    }`
  );
  if (!response.data) {
    throw new Error(response.message || "Query failed");
  }
  return response.data;
}

export async function getAppInstanceIdByAppKey(appKey: string): Promise<string | null> {
  const normalized = appKey.trim();
  if (!normalized) {
    return null;
  }

  // Direct 模式下不使用应用目录实例绑定，直接返回 null。
  // 这样可避免运行时场景产生不必要的目录查询请求。
  try {
    const runtimeMode = (import.meta as ImportMeta & { env?: Record<string, string | undefined> }).env?.VITE_APP_RUNTIME_MODE;
    if (runtimeMode === "direct") {
      return null;
    }
  } catch {
    // Ignore env access failure.
  }

  try {
    const query = new URLSearchParams({
      pageIndex: "1",
      pageSize: "1",
      appKey: normalized
    });
    const response = await requestApi<ApiResponse<PagedResult<ApplicationCatalogListItemDto>>>(
      `/api/v2/application-catalogs?${query.toString()}`
    );
    const first = response.data?.items?.[0];
    const id = first?.id?.trim();
    return id && id.length > 0 ? id : null;
  } catch {
    // Endpoint may not exist on current host — gracefully return null.
    return null;
  }
}
