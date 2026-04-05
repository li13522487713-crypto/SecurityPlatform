import type { ApiResponse } from "@atlas/shared-core";
import { requestApi } from "@/services/api-core";
import type {
  PluginDescriptor,
  PluginMarketEntry,
  PluginMarketSearchResult,
  PluginMarketVersion,
  PublishPluginRequest
} from "@/types/plugin";

export function getInstalledPlugins() {
  return requestApi<ApiResponse<PluginDescriptor[]>>("/plugins");
}

export function reloadPlugins() {
  return requestApi<ApiResponse<{ count: number }>>("/plugins/reload", {
    method: "POST"
  });
}

export function enablePlugin(code: string) {
  return requestApi<ApiResponse<null>>(`/plugins/${encodeURIComponent(code)}/enable`, {
    method: "POST"
  });
}

export function disablePlugin(code: string) {
  return requestApi<ApiResponse<null>>(`/plugins/${encodeURIComponent(code)}/disable`, {
    method: "POST"
  });
}

export function unloadPlugin(code: string) {
  return requestApi<ApiResponse<null>>(`/plugins/${encodeURIComponent(code)}/unload`, {
    method: "POST"
  });
}

export function installPluginPackage(file: File) {
  const form = new FormData();
  form.append("package", file);
  return requestApi<ApiResponse<{ code: string; name: string; version: string }>>("/plugins/install", {
    method: "POST",
    body: form
  });
}

export function getPluginConfig(code: string, tenantId?: string, appId?: string) {
  const params = new URLSearchParams();
  if (tenantId) params.set("tenantId", tenantId);
  if (appId) params.set("appId", appId);
  const query = params.toString();
  return requestApi<ApiResponse<{ configJson: string }>>(
    `/plugins/${encodeURIComponent(code)}/config${query ? `?${query}` : ""}`
  );
}

export function savePluginConfig(
  code: string,
  scope: "Global" | "Tenant" | "App",
  configJson: string,
  scopeId?: string
) {
  return requestApi<ApiResponse<null>>(`/plugins/${encodeURIComponent(code)}/config`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ scope, scopeId, configJson })
  });
}

export function searchPluginMarket(params: {
  keyword?: string;
  category?: string;
  pageIndex?: number;
  pageSize?: number;
}) {
  const query = new URLSearchParams();
  if (params.keyword) query.set("keyword", params.keyword);
  if (params.category) query.set("category", params.category);
  query.set("pageIndex", String(params.pageIndex ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  return requestApi<ApiResponse<PluginMarketSearchResult>>(`/plugin-market?${query.toString()}`);
}

export function getPluginMarketEntry(code: string) {
  return requestApi<ApiResponse<PluginMarketEntry>>(`/plugin-market/${encodeURIComponent(code)}`);
}

export function getPluginMarketVersions(code: string) {
  return requestApi<ApiResponse<PluginMarketVersion[]>>(`/plugin-market/${encodeURIComponent(code)}/versions`);
}

export function publishPlugin(data: PublishPluginRequest) {
  return requestApi<ApiResponse<{ id: number }>>("/plugin-market", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data)
  });
}

export function updatePluginMarketEntry(
  id: number,
  data: { name: string; description: string; iconUrl?: string }
) {
  return requestApi<ApiResponse<null>>(`/plugin-market/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data)
  });
}

export function deprecatePlugin(id: number) {
  return requestApi<ApiResponse<null>>(`/plugin-market/${id}/deprecate`, {
    method: "POST"
  });
}
