import type { NavigationProjectionResponse } from "../types/index";

interface ApiEnvelope<T> {
  success: boolean;
  code: string;
  message: string;
  data: T;
}

export type RequestApi = <T>(
  path: string,
  init?: RequestInit
) => Promise<T>;

export interface NavigationProjectionApi {
  getPlatform: () => Promise<NavigationProjectionResponse>;
  getWorkspace: (appInstanceId: string) => Promise<NavigationProjectionResponse>;
  getWorkspaceByAppKey: (appKey: string) => Promise<NavigationProjectionResponse>;
  getRuntime: () => Promise<NavigationProjectionResponse>;
}

function assertResponse<T>(response: ApiEnvelope<T>, fallbackMessage: string): T {
  if (!response.success || response.data == null) {
    throw new Error(response.message || fallbackMessage);
  }

  return response.data;
}

export function createNavigationProjectionApi(
  requestApi: RequestApi
): NavigationProjectionApi {
  return {
    async getPlatform() {
      const response = await requestApi<ApiEnvelope<NavigationProjectionResponse>>(
        "/api/v2/navigation/platform"
      );
      return assertResponse(response, "Load platform navigation projection failed.");
    },
    async getWorkspace(appInstanceId: string) {
      const normalized = appInstanceId.trim();
      if (!normalized) {
        throw new Error("appInstanceId is required.");
      }

      const response = await requestApi<ApiEnvelope<NavigationProjectionResponse>>(
        `/api/v2/navigation/apps/${encodeURIComponent(normalized)}/workspace`
      );
      return assertResponse(response, "Load workspace navigation projection failed.");
    },
    async getWorkspaceByAppKey(appKey: string) {
      const normalized = appKey.trim();
      if (!normalized) {
        throw new Error("appKey is required.");
      }

      const response = await requestApi<ApiEnvelope<NavigationProjectionResponse>>(
        `/api/v2/navigation/apps/by-key/${encodeURIComponent(normalized)}/workspace`
      );
      return assertResponse(response, "Load workspace navigation projection by appKey failed.");
    },
    async getRuntime() {
      const response = await requestApi<ApiEnvelope<NavigationProjectionResponse>>(
        "/api/v2/navigation/runtime"
      );
      return assertResponse(response, "Load runtime navigation projection failed.");
    }
  };
}
