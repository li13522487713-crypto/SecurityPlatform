import type { ApiResponse, PagedResult } from "@atlas/shared-core";
import { createWorkflowApiFromRequest } from "@atlas/workflow-editor/api";
import type {
  NodeDebugRequest,
  NodeDebugResponse,
  NodeExecutionDetailResponse,
  NodeTemplateMetadata,
  NodeTypeMetadata,
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
  WorkflowUpdateMetaRequest,
  WorkflowVersionItem
} from "@/types/workflow-v2";
import { API_BASE, requestApi } from "@/services/api-core";
import { getCurrentAppIdFromStorage } from "@/utils/app-context";

type IdLike = string | number;

export type { StreamCallbacks, StreamRunHandle } from "@atlas/workflow-editor/api";
import type { StreamCallbacks, StreamRunHandle } from "@atlas/workflow-editor/api";

export const workflowV2Api = createWorkflowApiFromRequest(requestApi, {
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

export function getNodeTypes(): Promise<ApiResponse<NodeTypeMetadata[]>> {
  return workflowV2Api.getNodeTypes();
}

export function getNodeTemplates(): Promise<ApiResponse<NodeTemplateMetadata[]>> {
  return workflowV2Api.getNodeTemplates();
}

export function syncRunWorkflow(id: IdLike, req: WorkflowRunRequest): Promise<ApiResponse<WorkflowRunResponse>> {
  return workflowV2Api.runSync(id, req);
}

export function asyncRunWorkflow(id: IdLike, req: WorkflowRunRequest): Promise<ApiResponse<string>> {
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

export function getExecutionCheckpoint(executionId: IdLike): Promise<ApiResponse<WorkflowExecutionCheckpointResponse>> {
  return workflowV2Api.getCheckpoint(executionId);
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
