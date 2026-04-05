import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import { requestApi, toQuery } from "./api-core";

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
}

export interface SaveDashboardRequest {
  name: string;
  description?: string;
  isDefault: boolean;
}

export async function getReportsPaged(request: PagedRequest): Promise<PagedResult<ReportItem>> {
  const response = await requestApi<ApiResponse<PagedResult<ReportItem>>>(`/reports?${toQuery(request)}`);
  if (!response.data) {
    throw new Error(response.message || "Request failed");
  }
  return response.data;
}

export async function createReport(request: SaveReportRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/reports", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "Create failed");
  }
}

export async function updateReport(id: string, request: SaveReportRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/reports/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "Update failed");
  }
}

export async function deleteReport(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/reports/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "Delete failed");
  }
}

export async function getDashboardsPaged(request: PagedRequest): Promise<PagedResult<DashboardItem>> {
  const response = await requestApi<ApiResponse<PagedResult<DashboardItem>>>(`/dashboards?${toQuery(request)}`);
  if (!response.data) {
    throw new Error(response.message || "Request failed");
  }
  return response.data;
}

export async function createDashboard(request: SaveDashboardRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/dashboards", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "Create failed");
  }
}

export async function updateDashboard(id: string, request: SaveDashboardRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dashboards/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "Update failed");
  }
}

export async function deleteDashboard(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dashboards/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "Delete failed");
  }
}
