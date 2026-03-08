import { requestApi } from "@/services/api-core";
import type { ApiResponse, PagedResult } from "@/types/api";

export interface ToolAuthorizationPolicyItem {
  id: string;
  toolId: string;
  toolName: string;
  policyType: string;
  rateLimitQuota: number;
  auditEnabled: boolean;
}

export interface RuntimeMenuItem {
  pageKey: string;
  title: string;
  routePath: string;
  icon?: string | null;
  sortOrder: number;
}

export interface RuntimeMenuResponse {
  appKey: string;
  items: RuntimeMenuItem[];
}

export interface RuntimeTaskListItem {
  id: string;
  type: string;
  title: string;
  status: string;
  createdAt: string;
}

export async function getPlatformOverview() {
  const resp = await requestApi<ApiResponse<Record<string, unknown>>>("/platform/overview", {
    method: "GET",
  });
  return resp.data ?? {};
}

export async function getPlatformResources() {
  const resp = await requestApi<ApiResponse<Record<string, unknown>>>("/platform/resources", {
    method: "GET",
  });
  return resp.data ?? {};
}

export async function getPlatformReleases(pageIndex = 1, pageSize = 10) {
  const resp = await requestApi<ApiResponse<PagedResult<Record<string, unknown>>>>(
    `/platform/releases?pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { method: "GET" }
  );
  return (
    resp.data ?? {
      items: [],
      total: 0,
      pageIndex,
      pageSize,
    }
  );
}

export async function getToolAuthorizationPolicies(pageIndex = 1, pageSize = 10): Promise<PagedResult<ToolAuthorizationPolicyItem>> {
  const resp = await requestApi<ApiResponse<PagedResult<ToolAuthorizationPolicyItem>>>(
    `/tools/authorization-policies?pageIndex=${pageIndex}&pageSize=${pageSize}`,
    {
      method: "GET",
    }
  );
  return (
    resp.data ?? {
      items: [],
      total: 0,
      pageIndex,
      pageSize,
    }
  );
}

export async function getRuntimeMenu(appKey: string): Promise<RuntimeMenuResponse> {
  const resp = await requestApi<ApiResponse<RuntimeMenuResponse>>(`/runtime/apps/${encodeURIComponent(appKey)}/menu`, {
    method: "GET",
  });
  return resp.data ?? { appKey, items: [] };
}

export async function getRuntimeTasks(pageIndex = 1, pageSize = 10): Promise<PagedResult<RuntimeTaskListItem>> {
  const resp = await requestApi<ApiResponse<PagedResult<RuntimeTaskListItem>>>(
    `/runtime/tasks?pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { method: "GET" }
  );
  return (
    resp.data ?? {
      items: [],
      total: 0,
      pageIndex,
      pageSize,
    }
  );
}

export async function executeRuntimeTaskAction(taskId: string, action: string, comment?: string): Promise<boolean> {
  const resp = await requestApi<ApiResponse<{ success: boolean }>>(`/runtime/tasks/${taskId}/actions`, {
    method: "POST",
    body: JSON.stringify({ action, comment }),
  });
  return resp.data?.success ?? false;
}
