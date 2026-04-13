import type { ApiResponse, PagedResult } from "@atlas/shared-react-core/types";
import { createWorkflowApiFromRequest } from "@atlas/workflow-core-react/api";
import type {
  NodeDebugRequest,
  NodeDebugResponse,
  NodeExecutionDetailResponse,
  NodeTemplateMetadata,
  NodeTypeMetadata,
  RunTrace,
  WorkflowDetailQuery,
  WorkflowModelCatalogItem,
  WorkflowCreateRequest,
  WorkflowDetailResponse,
  WorkflowExecutionCheckpointResponse,
  WorkflowExecutionDebugViewResponse,
  WorkflowListItem,
  WorkflowProcessResponse,
  WorkflowPublishRequest,
  WorkflowResumeRequest,
  WorkflowRunRequest,
  WorkflowRunResponse,
  WorkflowSaveRequest,
  WorkflowValidateRequest,
  WorkflowUpdateMetaRequest,
  WorkflowVersionDiff,
  WorkflowVersionRollbackResult,
  WorkflowVersionItem
} from "@atlas/workflow-core-react/types";
import { API_BASE, requestApi } from "@/services/api-core";
import { getLowCodeAppByKey } from "@/services/api-lowcode-runtime";
import {
  getCurrentAppIdFromStorage,
  getCurrentAppKeyFromPath,
  setCurrentAppIdToStorage
} from "@/utils/app-context";
import type { StreamCallbacks, StreamRunHandle } from "@atlas/workflow-core-react/api";

type IdLike = string | number;
export type { StreamCallbacks, StreamRunHandle } from "@atlas/workflow-core-react/api";

export interface WorkflowDependencyItem {
  resourceType: string;
  resourceId: string;
  name: string;
  description?: string;
  sourceNodeKeys?: string[];
}

export interface WorkflowDependencies {
  workflowId: string;
  subWorkflows: WorkflowDependencyItem[];
  plugins: WorkflowDependencyItem[];
  knowledgeBases: WorkflowDependencyItem[];
  databases: WorkflowDependencyItem[];
  variables: WorkflowDependencyItem[];
  conversations: WorkflowDependencyItem[];
}

const APP_ID_REGEX = /^[1-9]\d*$/;
let appIdResolvingPromise: Promise<string | null> | null = null;

async function ensureCurrentAppId(): Promise<string | null> {
  const storedAppId = getCurrentAppIdFromStorage();
  if (storedAppId && APP_ID_REGEX.test(storedAppId)) {
    return storedAppId;
  }

  const appKey = getCurrentAppKeyFromPath();
  if (!appKey) {
    return null;
  }

  if (!appIdResolvingPromise) {
    appIdResolvingPromise = getLowCodeAppByKey(appKey)
      .then((detail) => {
        const resolved = String(detail.id ?? "").trim();
        if (APP_ID_REGEX.test(resolved)) {
          setCurrentAppIdToStorage(resolved);
          return resolved;
        }
        return null;
      })
      .catch(() => null)
      .finally(() => {
        appIdResolvingPromise = null;
      });
  }

  return appIdResolvingPromise;
}

function createWorkflowRequestHeaders(appId: string | null, init?: RequestInit): Headers {
  const headers = new Headers(init?.headers ?? {});
  if (appId && APP_ID_REGEX.test(appId)) {
    headers.set("X-App-Id", appId);
    headers.set("X-App-Workspace", "1");
  }
  return headers;
}

const workflowRequest = async <T>(path: string, init?: RequestInit) => {
  const appId = await ensureCurrentAppId();
  return requestApi<T>(path, {
    ...init,
    headers: createWorkflowRequestHeaders(appId, init)
  });
};

const baseWorkflowV2Api = createWorkflowApiFromRequest(workflowRequest, {
  resolveAbsoluteUrl: (path) => {
    if (path.startsWith("http://") || path.startsWith("https://")) {
      return path;
    }
    if (API_BASE.startsWith("http://") || API_BASE.startsWith("https://")) {
      const normalizedPath = path.startsWith("/") ? path : `/${path}`;
      return new URL(normalizedPath, API_BASE).toString();
    }
    return path;
  },
  resolveAppId: () => getCurrentAppIdFromStorage()
});

export async function getWorkflowModelCatalog(): Promise<ApiResponse<WorkflowModelCatalogItem[]>> {
  const response = await workflowRequest<ApiResponse<Array<{
    name: string;
    providerType: string;
    defaultModel: string;
    modelId?: string;
    systemPrompt?: string;
    enableStreaming: boolean;
    temperature?: number;
    maxTokens?: number;
  }>>>("/model-configs/enabled");

  return {
    ...response,
    data: (response.data ?? []).map((item) => ({
      provider: item.name,
      providerType: item.providerType,
      model: item.modelId || item.defaultModel,
      label: item.defaultModel || item.modelId || item.name,
      systemPrompt: item.systemPrompt,
      temperature: item.temperature,
      maxTokens: item.maxTokens,
      enableStreaming: item.enableStreaming
    }))
  };
}

export const workflowV2Api = {
  ...baseWorkflowV2Api,
  getModelCatalog: getWorkflowModelCatalog
};

export function createWorkflow(req: WorkflowCreateRequest): Promise<ApiResponse<string>> {
  return workflowV2Api.create(req).then((res) => ({
    ...res,
    data: res.data?.id
  }));
}

export function listWorkflows(
  pageIndex = 1,
  pageSize = 20,
  keyword?: string
): Promise<ApiResponse<PagedResult<WorkflowListItem>>> {
  return workflowV2Api.list(pageIndex, pageSize, keyword);
}

export function getWorkflowCanvas(id: IdLike): Promise<ApiResponse<WorkflowDetailResponse>> {
  return workflowV2Api.getDetail(id);
}

export function getWorkflowCanvasByQuery(
  id: IdLike,
  query?: WorkflowDetailQuery
): Promise<ApiResponse<WorkflowDetailResponse>> {
  return workflowV2Api.getDetail(id, query);
}

export function saveWorkflowDraft(id: IdLike, req: WorkflowSaveRequest): Promise<ApiResponse<boolean>> {
  return workflowV2Api.saveDraft(id, req);
}

export function updateWorkflowMeta(id: IdLike, req: WorkflowUpdateMetaRequest): Promise<ApiResponse<boolean>> {
  return workflowV2Api.updateMeta(id, req);
}

export function publishWorkflow(id: IdLike, req: WorkflowPublishRequest): Promise<ApiResponse<{ id: string }>> {
  return workflowV2Api.publish(id, req);
}

export function copyWorkflow(id: IdLike): Promise<ApiResponse<string>> {
  return workflowV2Api.copy(id).then((res) => ({
    ...res,
    data: res.data?.id
  }));
}

export function deleteWorkflow(id: IdLike): Promise<ApiResponse<boolean>> {
  return workflowV2Api.delete(id);
}

export function listWorkflowVersions(id: IdLike): Promise<ApiResponse<WorkflowVersionItem[]>> {
  return workflowV2Api.getVersions(id);
}

export function getWorkflowVersionDiff(
  id: IdLike,
  fromVersionId: IdLike,
  toVersionId: IdLike
): Promise<ApiResponse<WorkflowVersionDiff>> {
  return workflowV2Api.getVersionDiff(id, fromVersionId, toVersionId);
}

export function rollbackWorkflowVersion(
  id: IdLike,
  versionId: IdLike
): Promise<ApiResponse<WorkflowVersionRollbackResult>> {
  return workflowV2Api.rollbackVersion(id, versionId);
}

export function getNodeTypes(): Promise<ApiResponse<NodeTypeMetadata[]>> {
  return workflowV2Api.getNodeTypes();
}

export function getNodeTemplates(): Promise<ApiResponse<NodeTemplateMetadata[]>> {
  return workflowV2Api.getNodeTemplates();
}

export async function getWorkflowDependencies(id: IdLike): Promise<ApiResponse<WorkflowDependencies>> {
  return workflowRequest<ApiResponse<WorkflowDependencies>>(`/api/v2/workflows/${id}/dependencies`);
}

export function syncRunWorkflow(id: IdLike, req: WorkflowRunRequest): Promise<ApiResponse<WorkflowRunResponse>> {
  return workflowV2Api.runSync(id, req);
}

export function runWorkflowAndReturnExecutionId(id: IdLike, req: WorkflowRunRequest): Promise<ApiResponse<string>> {
  return workflowV2Api.runSync(id, req).then((res) => ({
    ...res,
    data: res.data?.executionId
  }));
}

export function cancelExecution(executionId: IdLike): Promise<ApiResponse<boolean>> {
  return workflowV2Api.cancel(executionId);
}

export function getExecutionProcess(executionId: IdLike): Promise<ApiResponse<WorkflowProcessResponse>> {
  return workflowV2Api.getProcess(executionId);
}

export function getExecutionTrace(executionId: IdLike): Promise<ApiResponse<RunTrace>> {
  return workflowV2Api.getTrace(executionId);
}

export function getExecutionCheckpoint(executionId: IdLike): Promise<ApiResponse<WorkflowExecutionCheckpointResponse>> {
  return workflowV2Api.getCheckpoint(executionId);
}

export function validateWorkflowCanvas(
  id: IdLike,
  req: WorkflowValidateRequest
): Promise<ApiResponse<{ isValid?: boolean; errors?: string[] }>> {
  return workflowV2Api.validate(id, req);
}

export function getNodeExecutionDetail(
  executionId: IdLike,
  nodeKey: string
): Promise<ApiResponse<NodeExecutionDetailResponse>> {
  return workflowV2Api.getNodeDetail(executionId, nodeKey);
}

export function resumeExecution(executionId: IdLike, req: WorkflowResumeRequest): Promise<ApiResponse<boolean>> {
  return workflowV2Api.resume(executionId, req);
}

export function streamResumeExecution(executionId: IdLike, callbacks: StreamCallbacks): StreamRunHandle {
  return workflowV2Api.streamResume(executionId, callbacks);
}

export function recoverExecution(executionId: IdLike): Promise<ApiResponse<WorkflowRunResponse>> {
  return workflowV2Api.recover(executionId);
}

export function getExecutionDebugView(executionId: IdLike): Promise<ApiResponse<WorkflowExecutionDebugViewResponse>> {
  return workflowV2Api.getDebugView(executionId);
}

export function listPublishedWorkflows(
  pageIndex = 1,
  pageSize = 20,
  keyword?: string
): Promise<ApiResponse<PagedResult<WorkflowListItem>>> {
  return workflowV2Api.listPublished(pageIndex, pageSize, keyword);
}

export function debugNode(
  workflowId: IdLike,
  nodeKey: string,
  req: NodeDebugRequest
): Promise<ApiResponse<NodeDebugResponse>> {
  return workflowV2Api.debugNode(workflowId, nodeKey, req);
}
