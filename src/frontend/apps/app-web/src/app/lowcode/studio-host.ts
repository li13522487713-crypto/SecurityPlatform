import {
  createLowcodeApi,
  type LowcodeStudioHostConfig
} from "@atlas/lowcode-studio-react";
import {
  getAccessToken,
  getAuthProfile,
  getTenantId
} from "@atlas/shared-react-core/utils";
import type { JsonValue } from "@atlas/shared-react-core/types";
import { requestApi } from "../../services/api-core";

interface ApiResponse<T> {
  success: boolean;
  code?: string;
  message?: string;
  traceId?: string;
  data: T;
}

const DEFAULT_TENANT_ID = "00000000-0000-0000-0000-000000000001";

const appWebLowcodeApi = createLowcodeApi(async <T>(method: string, path: string, body?: JsonValue) => {
  const response = await requestApi<ApiResponse<T>>(`/lowcode${path}`, {
    method,
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  if (!response.success) {
    throw new Error(response.message || response.code || `低代码请求失败: ${method} ${path}`);
  }
  return response.data;
});

export function createAppWebLowcodeStudioHost(): LowcodeStudioHostConfig {
  return {
    api: appWebLowcodeApi,
    auth: {
      accessTokenFactory: () => getAccessToken() ?? "",
      tenantIdFactory: () => getTenantId() ?? DEFAULT_TENANT_ID,
      userIdFactory: () => getAuthProfile()?.id ?? "me"
    }
  };
}
