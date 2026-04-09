import { createApiClient } from "@atlas/shared-core/api";
import type { RequestOptions } from "@atlas/shared-core/api";
import { router } from "@/router";

export const API_BASE = import.meta.env.VITE_API_BASE ?? "/api/v1";

function resolveRequestUrl(path: string): string {
  if (path.startsWith("http://") || path.startsWith("https://")) return path;
  if (path.startsWith("/api/")) return path;
  return `${API_BASE}${path}`;
}

export type { RequestOptions };

function forceLogout() {
  if (router.currentRoute.value.name !== "login") {
    void router.push({ name: "login" });
  }
}

const apiClient = createApiClient({
  resolveRequestUrl,
  includeProjectScopeHeader: true,
  onUnauthorized: forceLogout
});

export const persistTokenResult = apiClient.persistTokenResult;
export const requestApi = apiClient.requestApi;
export const requestApiBlob = apiClient.requestApiBlob;
export const toQuery = apiClient.toQuery;
export const requestPagedApi = apiClient.requestPagedApi;
export const uploadFile = apiClient.uploadFile;
export const downloadFile = apiClient.downloadFile;
