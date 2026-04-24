import {
  createLowcodeApi,
  createRuntimeSessionApi,
  type LowCodeAssetDescriptor,
  type ProjectIdeBootstrap,
  type ProjectIdeGraph,
  type ProjectIdePublishPreview,
  type ProjectIdePublishRequest,
  type ProjectIdePublishResult,
  type ProjectIdeValidationResult,
  type RuntimeDispatchResponse,
  type RuntimeTrace,
  type LowcodeStudioHostConfig
} from "@atlas/lowcode-studio-react/services";
import { createElement } from "react";
import { studioPluginDetailPath } from "@atlas/app-shell-shared";
import { LowcodeWorkflowEmbed } from "./workflow-embed";
import { createWorkflow } from "../../services/api-workflow";
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

async function requestJson<T>(path: string, method: string, body?: unknown): Promise<T> {
  const response = await requestApi<ApiResponse<T>>(path, {
    method,
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  if (!response.success) {
    throw new Error(response.message || response.code || `请求失败: ${method} ${path}`);
  }
  return response.data;
}

export function createAppWebLowcodeStudioHost(appKey: string): LowcodeStudioHostConfig {
  return {
    api: appWebLowcodeApi,
    bootstrapApi: {
      getBootstrap: (appId: string) => requestJson<ProjectIdeBootstrap>(`/project-ide/apps/${encodeURIComponent(appId)}/bootstrap`, "GET"),
      getGraph: (appId: string) => requestJson<ProjectIdeGraph>(`/project-ide/apps/${encodeURIComponent(appId)}/graph`, "GET")
    },
    validationApi: {
      validate: (appId: string, schemaJson?: string) =>
        requestJson<ProjectIdeValidationResult>(`/project-ide/apps/${encodeURIComponent(appId)}/validate`, "POST", { schemaJson })
    },
    publishApi: {
      listArtifacts: async (appId: string) => {
        const bootstrap = await requestJson<ProjectIdeBootstrap>(`/project-ide/apps/${encodeURIComponent(appId)}/bootstrap`, "GET");
        return bootstrap.artifacts ?? [];
      },
      getPreview: (appId: string) =>
        requestJson<ProjectIdePublishPreview>(`/project-ide/apps/${encodeURIComponent(appId)}/publish-preview`, "GET"),
      publish: (appId: string, request: ProjectIdePublishRequest) =>
        requestJson<ProjectIdePublishResult>(`/project-ide/apps/${encodeURIComponent(appId)}/publish`, "POST", request)
    },
    assetApi: {
      prepareUpload: (request) =>
        requestJson(`/lowcode/assets/upload-session`, "POST", request),
      getAsset: (id: string) =>
        requestJson<LowCodeAssetDescriptor>(`/lowcode/assets/${encodeURIComponent(id)}`, "GET"),
      deleteAsset: async (id: string) => {
        await requestJson(`/lowcode/assets/${encodeURIComponent(id)}`, "DELETE");
      }
    },
    dispatchApi: {
      dispatch: (request) =>
        requestJson<RuntimeDispatchResponse>(`/api/runtime/dispatch`, "POST", request),
      getTrace: (traceId: string) =>
        requestJson<RuntimeTrace>(`/api/runtime/traces/${encodeURIComponent(traceId)}`, "GET"),
      queryTraces: (query) => {
        const params = new URLSearchParams();
        Object.entries(query ?? {}).forEach(([key, value]) => {
          if (value !== undefined && value !== null && String(value).trim().length > 0) {
            params.set(key, String(value));
          }
        });
        const suffix = params.size > 0 ? `?${params.toString()}` : "";
        return requestJson<RuntimeTrace[]>(`/api/runtime/traces${suffix}`, "GET");
      }
    },
    runtimeSessions: createRuntimeSessionApi((method, path, body) => requestJson(path, method, body)),
    collabConfig: {
      hubUrl: "/hubs/lowcode-collab",
      reconnectDelaysMs: [0, 1000, 3000, 5000]
    },
    openPluginDetail: (pluginId) => {
      if (typeof window !== "undefined") {
        window.location.assign(studioPluginDetailPath(appKey, pluginId));
      }
    },
    renderWorkflowEditor: (props) => createElement(LowcodeWorkflowEmbed, props),
    createWorkflow: async ({ appId, name, description, workspaceId }) => {
      const response = await createWorkflow({
        name,
        description,
        mode: 0,
        workspaceId
      });
      if (!response.success || !response.data) {
        throw new Error(response.message || "创建工作流失败");
      }
      const workflowId = response.data;
      // 绑定工作流到当前低代码 App，使其出现在 resources.search(appId) 结果中
      // 否则新建的 WorkflowMeta 行缺少 AiAppResourceBinding，左侧面板刷新后看不到
      await requestJson(
        `/api/v1/lowcode/apps/${encodeURIComponent(appId)}/resources/bindings`,
        "POST",
        // Snowflake IDs exceed JS safe integer; send as string — FlexibleLongJsonConverter on the API reads string into long.
        { resourceType: "workflow", resourceId: String(workflowId) }
      );
      return { workflowId };
    },
    auth: {
      accessTokenFactory: () => getAccessToken() ?? "",
      tenantIdFactory: () => getTenantId() ?? DEFAULT_TENANT_ID,
      userIdFactory: () => getAuthProfile()?.id ?? "me"
    }
  };
}
