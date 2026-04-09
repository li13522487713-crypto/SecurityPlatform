import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import type {
  AppBridgeCommandCreateRequest,
  AppCommandDetail,
  AppCommandListItem,
  AppExposurePolicy,
  ExposedDataQueryResult,
  OnlineAppProjectionDetail,
  OnlineAppProjectionItem
} from "../types/index";

const APPBRIDGE_BASE = "/api/v2/appbridge";

export type RequestApi = <T>(path: string, init?: RequestInit) => Promise<T>;

export interface AppBridgeConsoleApi {
  getOnlineApps(request: PagedRequest): Promise<PagedResult<OnlineAppProjectionItem>>;
  getOnlineAppDetail(appInstanceId: string): Promise<OnlineAppProjectionDetail>;
  getExposurePolicy(appInstanceId: string): Promise<AppExposurePolicy>;
  updateExposurePolicy(
    appInstanceId: string,
    payload: Pick<AppExposurePolicy, "exposedDataSets" | "allowedCommands" | "maskPolicies">
  ): Promise<AppExposurePolicy>;
  createCommand(payload: AppBridgeCommandCreateRequest): Promise<string>;
  getCommands(
    request: PagedRequest,
    appInstanceId?: string
  ): Promise<PagedResult<AppCommandListItem>>;
  getCommandDetail(commandId: string): Promise<AppCommandDetail>;
  queryExposedData(
    appInstanceId: string,
    dataSet: string,
    paged: PagedRequest
  ): Promise<ExposedDataQueryResult>;
}

export function createAppBridgeConsoleApi(requestApi: RequestApi): AppBridgeConsoleApi {
  return {
    async getOnlineApps(request) {
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
    },

    async getOnlineAppDetail(appInstanceId) {
      const response = await requestApi<ApiResponse<OnlineAppProjectionDetail>>(
        `${APPBRIDGE_BASE}/online-apps/${encodeURIComponent(appInstanceId)}`
      );
      if (!response.data) {
        throw new Error(response.message || "获取在线应用详情失败");
      }
      return response.data;
    },

    async getExposurePolicy(appInstanceId) {
      const response = await requestApi<ApiResponse<AppExposurePolicy>>(
        `${APPBRIDGE_BASE}/apps/${encodeURIComponent(appInstanceId)}/exposure-policy`
      );
      if (!response.data) {
        throw new Error(response.message || "获取暴露策略失败");
      }
      return response.data;
    },

    async updateExposurePolicy(appInstanceId, payload) {
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
    },

    async createCommand(payload) {
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
    },

    async getCommands(request, appInstanceId) {
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
    },

    async getCommandDetail(commandId) {
      const response = await requestApi<ApiResponse<AppCommandDetail>>(
        `${APPBRIDGE_BASE}/commands/${encodeURIComponent(commandId)}`
      );
      if (!response.data) {
        throw new Error(response.message || "查询命令详情失败");
      }
      return response.data;
    },

    async queryExposedData(appInstanceId, dataSet, paged) {
      const response = await requestApi<ApiResponse<ExposedDataQueryResult>>(
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
  };
}
