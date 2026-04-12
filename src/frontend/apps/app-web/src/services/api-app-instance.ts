import type { ApiResponse } from "@atlas/shared-react-core";
import { requestApi } from "./api-core";

export interface TenantAppInstanceDetail {
  id: string;
  appKey: string;
  name: string;
  status: string;
  version: number;
  description: string;
  category: string;
  icon: string;
  publishedAt: string | null;
  dataSourceId: number | null;
  pageCount: number;
  currentArtifactId: number | null;
  runtimeStatus: string;
  healthStatus: string;
  assignedPort: number | null;
  currentPid: number | null;
  ingressUrl: string;
  loginUrl: string;
  instanceHome: string;
  lastStartedAt: string | null;
  lastHealthCheckedAt: string | null;
}

export interface AppInstanceUpdateRequest {
  name: string;
  description?: string;
  category?: string;
  icon?: string;
  dataSourceId?: number | null;
  unbindDataSource?: boolean;
}

export interface TenantAppInstanceRuntimeInfo {
  runtimeStatus: string;
  healthStatus: string;
  assignedPort: number | null;
  currentPid: number | null;
  lastStartedAt: string | null;
}

const V2_BASE = "/api/v2/tenant-app-instances";

export async function getAppInstanceDetail(appId: string): Promise<TenantAppInstanceDetail> {
  const response = await requestApi<ApiResponse<TenantAppInstanceDetail>>(
    `${V2_BASE}/${appId}`
  );
  if (!response.data) throw new Error(response.message || "Failed to fetch app detail");
  return response.data;
}

export async function updateAppInstance(
  appId: string,
  data: AppInstanceUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `${V2_BASE}/${appId}`,
    {
      method: "PUT",
      body: JSON.stringify(data)
    }
  );
  if (!response.success) throw new Error(response.message || "Failed to update app");
}

export async function publishApp(appId: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `${V2_BASE}/${appId}/publish`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "Failed to publish");
}

export async function startApp(appId: string): Promise<TenantAppInstanceRuntimeInfo> {
  const response = await requestApi<ApiResponse<TenantAppInstanceRuntimeInfo>>(
    `${V2_BASE}/${appId}/start`,
    { method: "POST" }
  );
  if (!response.data) throw new Error(response.message || "Failed to start app");
  return response.data;
}

export async function stopApp(appId: string): Promise<TenantAppInstanceRuntimeInfo> {
  const response = await requestApi<ApiResponse<TenantAppInstanceRuntimeInfo>>(
    `${V2_BASE}/${appId}/stop`,
    { method: "POST" }
  );
  if (!response.data) throw new Error(response.message || "Failed to stop app");
  return response.data;
}

export async function restartApp(appId: string): Promise<TenantAppInstanceRuntimeInfo> {
  const response = await requestApi<ApiResponse<TenantAppInstanceRuntimeInfo>>(
    `${V2_BASE}/${appId}/restart`,
    { method: "POST" }
  );
  if (!response.data) throw new Error(response.message || "Failed to restart app");
  return response.data;
}
