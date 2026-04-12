import type { ApiResponse, PagedResult } from "@atlas/shared-react-core";
import { requestApi, resolveAppHostPrefix } from "./api-core";

export async function getApprovalPendingCount(): Promise<number> {
  try {
    const params = new URLSearchParams({
      PageIndex: "1",
      PageSize: "1",
      status: "Pending"
    });
    const response = await requestApi<ApiResponse<PagedResult<unknown>>>(
      `/approval/tasks/my?${params.toString()}`
    );
    return response.data?.total ?? 0;
  } catch {
    return 0;
  }
}

export async function getReportCount(appKey: string): Promise<number> {
  try {
    const prefix = resolveAppHostPrefix(appKey);
    const params = new URLSearchParams({ PageIndex: "1", PageSize: "1" });
    const response = await requestApi<ApiResponse<PagedResult<unknown>>>(
      `${prefix}/api/v1/reports?${params.toString()}`
    );
    return response.data?.total ?? 0;
  } catch {
    return 0;
  }
}

export async function getDashboardCount(appKey: string): Promise<number> {
  try {
    const prefix = resolveAppHostPrefix(appKey);
    const params = new URLSearchParams({ PageIndex: "1", PageSize: "1" });
    const response = await requestApi<ApiResponse<PagedResult<unknown>>>(
      `${prefix}/api/v1/dashboards?${params.toString()}`
    );
    return response.data?.total ?? 0;
  } catch {
    return 0;
  }
}
