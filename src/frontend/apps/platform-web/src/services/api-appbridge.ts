import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import type {
  AppCommandDetail,
  AppCommandListItem,
  AppExposurePolicy,
  OnlineAppProjectionDetail,
  OnlineAppProjectionItem
} from "@/types/platform-console";
import { requestApi } from "@/services/api-core";

const APPBRIDGE_BASE = "/api/v2/appbridge";

export async function getOnlineApps(
  request: PagedRequest
): Promise<PagedResult<OnlineAppProjectionItem>> {
  const query = new URLSearchParams({
    pageIndex: request.pageIndex.toString(),
    pageSize: request.pageSize.toString()
  });
  if (request.keyword) {
    query.set("keyword", request.keyword);
  }

  const response = await requestApi<ApiResponse<PagedResult<OnlineAppProjectionItem>>>(
    `${APPBRIDGE_BASE}/online-apps?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取在线应用列表失败");
  }

  return response.data;
}

export async function getOnlineAppDetail(appInstanceId: string): Promise<OnlineAppProjectionDetail> {
  const response = await requestApi<ApiResponse<OnlineAppProjectionDetail>>(
    `${APPBRIDGE_BASE}/online-apps/${encodeURIComponent(appInstanceId)}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取在线应用详情失败");
  }

  return response.data;
}

export async function getExposurePolicy(appInstanceId: string): Promise<AppExposurePolicy> {
  const response = await requestApi<ApiResponse<AppExposurePolicy>>(
    `${APPBRIDGE_BASE}/apps/${encodeURIComponent(appInstanceId)}/exposure-policy`
  );
  if (!response.data) {
    throw new Error(response.message || "获取暴露策略失败");
  }

  return response.data;
}

export async function updateExposurePolicy(
  appInstanceId: string,
  payload: Pick<AppExposurePolicy, "exposedDataSets" | "allowedCommands" | "maskPolicies">
): Promise<AppExposurePolicy> {
  const response = await requestApi<ApiResponse<AppExposurePolicy>>(
    `${APPBRIDGE_BASE}/apps/${encodeURIComponent(appInstanceId)}/exposure-policy`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "更新暴露策略失败");
  }

  return response.data;
}

export async function createAppBridgeCommand(payload: {
  appInstanceId: string;
  commandType: string;
  payloadJson: string;
  dryRun: boolean;
  reason?: string;
}): Promise<string> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `${APPBRIDGE_BASE}/commands`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    }
  );
  if (!response.data?.id) {
    throw new Error(response.message || "创建命令失败");
  }

  return response.data.id;
}

export async function getAppBridgeCommands(
  request: PagedRequest,
  appInstanceId?: string
): Promise<PagedResult<AppCommandListItem>> {
  const query = new URLSearchParams({
    pageIndex: request.pageIndex.toString(),
    pageSize: request.pageSize.toString()
  });
  if (request.keyword) {
    query.set("keyword", request.keyword);
  }
  if (appInstanceId) {
    query.set("appInstanceId", appInstanceId);
  }

  const response = await requestApi<ApiResponse<PagedResult<AppCommandListItem>>>(
    `${APPBRIDGE_BASE}/commands?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询命令列表失败");
  }

  return response.data;
}

export async function getAppBridgeCommandDetail(commandId: string): Promise<AppCommandDetail> {
  const response = await requestApi<ApiResponse<AppCommandDetail>>(
    `${APPBRIDGE_BASE}/commands/${encodeURIComponent(commandId)}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询命令详情失败");
  }

  return response.data;
}

export async function queryExposedData(
  appInstanceId: string,
  dataSet: string,
  paged: PagedRequest
): Promise<{ dataSet: string; result: PagedResult<Record<string, unknown>> }> {
  const response = await requestApi<ApiResponse<{ dataSet: string; result: PagedResult<Record<string, unknown>> }>>(
    `${APPBRIDGE_BASE}/apps/${encodeURIComponent(appInstanceId)}/exposed-data/query`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ dataSet, paged })
    }
  );
  if (!response.data) {
    throw new Error(response.message || "查询暴露数据失败");
  }

  return response.data;
}
