import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core/types";
import { requestApi, toQuery, resolveAppHostPrefix } from "./api-core";

export interface ReportItem {
  id: string;
  name: string;
  description?: string | null;
  createdAt: string;
}

export interface DashboardItem {
  id: string;
  name: string;
  description?: string | null;
  isDefault: boolean;
  createdAt: string;
}

export interface SaveReportRequest {
  name: string;
  description?: string;
  category?: string;
  configJson: string;
  dataSourceJson?: string;
}

export interface SaveDashboardRequest {
  name: string;
  description?: string;
  category?: string;
  layoutJson: string;
  isDefault: boolean;
  isLargeScreen: boolean;
  canvasWidth?: number;
  canvasHeight?: number;
  themeJson?: string;
}

function reportsBase(appKey: string): string {
  return `${resolveAppHostPrefix(appKey)}/api/v1/reports`;
}

function dashboardsBase(appKey: string): string {
  return `${resolveAppHostPrefix(appKey)}/api/v1/dashboards`;
}

export async function getReportsPaged(appKey: string, request: PagedRequest): Promise<PagedResult<ReportItem>> {
  const response = await requestApi<ApiResponse<PagedResult<ReportItem>>>(`${reportsBase(appKey)}?${toQuery(request)}`);
  if (!response.data) throw new Error(response.message || "Request failed");
  return response.data;
}

export async function createReport(appKey: string, request: SaveReportRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(reportsBase(appKey), {
    method: "POST",
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "Create failed");
}

export async function updateReport(appKey: string, id: string, request: SaveReportRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`${reportsBase(appKey)}/${id}`, {
    method: "PUT",
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "Update failed");
}

export async function deleteReport(appKey: string, id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`${reportsBase(appKey)}/${id}`, {
    method: "DELETE"
  });
  if (!response.success) throw new Error(response.message || "Delete failed");
}

export async function getDashboardsPaged(appKey: string, request: PagedRequest): Promise<PagedResult<DashboardItem>> {
  const response = await requestApi<ApiResponse<PagedResult<DashboardItem>>>(`${dashboardsBase(appKey)}?${toQuery(request)}`);
  if (!response.data) throw new Error(response.message || "Request failed");
  return response.data;
}

export async function createDashboard(appKey: string, request: SaveDashboardRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(dashboardsBase(appKey), {
    method: "POST",
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "Create failed");
}

export async function updateDashboard(appKey: string, id: string, request: SaveDashboardRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`${dashboardsBase(appKey)}/${id}`, {
    method: "PUT",
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "Update failed");
}

export async function deleteDashboard(appKey: string, id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`${dashboardsBase(appKey)}/${id}`, {
    method: "DELETE"
  });
  if (!response.success) throw new Error(response.message || "Delete failed");
}
