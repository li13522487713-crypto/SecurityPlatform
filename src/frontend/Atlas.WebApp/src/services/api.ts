import type {
  ApiResponse,
  PagedRequest,
  PagedResult,
  ApprovalFlowDefinitionListItem,
  ApprovalFlowDefinitionResponse,
  ApprovalFlowDefinitionCreateRequest,
  ApprovalFlowDefinitionUpdateRequest,
  ApprovalFlowPublishRequest,
  ApprovalStartRequest,
  ApprovalTaskResponse,
  ApprovalTaskDecideRequest,
  ApprovalInstanceListItem,
  ApprovalInstanceResponse,
  ApprovalHistoryEventResponse,
  StepTypeMetadata,
  RegisterWorkflowDefinitionRequest,
  ExecutionPointerResponse,
  WorkflowInstanceResponse,
  WorkflowInstanceListItem,
  VisualizationOverview,
  VisualizationProcessSummary,
  VisualizationProcessDetail,
  VisualizationInstanceSummary,
  VisualizationInstanceDetail,
  PublishVisualizationRequest,
  ValidateVisualizationRequest,
  VisualizationValidationResult,
  VisualizationPublishResult,
  SaveVisualizationProcessRequest,
  SaveVisualizationProcessResult,
  VisualizationMetricsResponse,
  AuditListItem
} from "@/types/api";
import { message } from "ant-design-vue";

const API_BASE = import.meta.env.VITE_API_BASE ?? "/api";

interface TokenResult {
  accessToken: string;
  expiresAt: string;
}

export interface AssetListItem {
  id: string;
  name: string;
}

export interface AlertListItem {
  id: string;
  title: string;
  createdAt: string;
}

export async function createToken(tenantId: string, username: string, password: string) {
  const response = await requestApi<ApiResponse<TokenResult>>("/auth/token", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": tenantId
    },
    body: JSON.stringify({ username, password })
  });

  if (!response.data) {
    throw new Error(response.message || "登录失败");
  }

  return response.data;
}

export async function getAssetsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<AssetListItem>>>(`/assets?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function getAuditsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<AuditListItem>>>(`/audit?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function getAlertsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<AlertListItem>>>(`/alert?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

function toQuery(pagedRequest: PagedRequest) {
  const query = new URLSearchParams({
    pageIndex: pagedRequest.pageIndex.toString(),
    pageSize: pagedRequest.pageSize.toString(),
    keyword: pagedRequest.keyword ?? "",
    sortBy: pagedRequest.sortBy ?? "",
    sortDesc: pagedRequest.sortDesc ? "true" : "false"
  });

  return query.toString();
}

// 审批流 API
export async function getApprovalFlowsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<ApprovalFlowDefinitionListItem>>>(
    `/approval/flows?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getApprovalFlowById(id: string) {
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>(`/approval/flows/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function createApprovalFlow(request: ApprovalFlowDefinitionCreateRequest) {
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>("/approval/flows", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建失败");
  }
  return response.data;
}

export async function updateApprovalFlow(id: string, request: ApprovalFlowDefinitionUpdateRequest) {
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>(`/approval/flows/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "更新失败");
  }
  return response.data;
}

export async function deleteApprovalFlow(id: string) {
  const response = await requestApi<ApiResponse<void>>(`/approval/flows/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function publishApprovalFlow(id: string, request?: ApprovalFlowPublishRequest) {
  const response = await requestApi<ApiResponse<void>>(`/approval/flows/${id}/publish`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request || {})
  });
  if (!response.success) {
    throw new Error(response.message || "发布失败");
  }
}

export async function disableApprovalFlow(id: string) {
  const response = await requestApi<ApiResponse<void>>(`/approval/flows/${id}/disable`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "停用失败");
  }
}

export async function startApprovalInstance(request: ApprovalStartRequest) {
  const response = await requestApi<ApiResponse<ApprovalInstanceResponse>>("/approval/instances", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "发起失败");
  }
  return response.data;
}

export async function getMyInstancesPaged(pagedRequest: PagedRequest, status?: number) {
  const params = new URLSearchParams(toQuery(pagedRequest));
  if (status !== undefined) {
    params.append("status", status.toString());
  }
  const response = await requestApi<ApiResponse<PagedResult<ApprovalInstanceListItem>>>(
    `/approval/instances/my?${params.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getApprovalInstanceById(id: string) {
  const response = await requestApi<ApiResponse<ApprovalInstanceResponse>>(`/approval/instances/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getApprovalInstanceHistory(id: string, pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<ApprovalHistoryEventResponse>>>(
    `/approval/instances/${id}/history?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function cancelApprovalInstance(id: string) {
  const response = await requestApi<ApiResponse<void>>(`/approval/instances/${id}/cancel`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "取消失败");
  }
}

export async function getMyTasksPaged(pagedRequest: PagedRequest, status?: number) {
  const params = new URLSearchParams(toQuery(pagedRequest));
  if (status !== undefined) {
    params.append("status", status.toString());
  }
  const response = await requestApi<ApiResponse<PagedResult<ApprovalTaskResponse>>>(
    `/approval/tasks/my?${params.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getApprovalTasksByInstance(instanceId: string, pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<ApprovalTaskResponse>>>(
    `/approval/tasks/instance/${instanceId}?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function decideApprovalTask(request: ApprovalTaskDecideRequest) {
  const response = await requestApi<ApiResponse<void>>("/approval/tasks/decide", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "操作失败");
  }
}

// WorkflowCore 相关 API

export async function getWorkflowStepTypes(): Promise<StepTypeMetadata[]> {
  const response = await requestApi<ApiResponse<StepTypeMetadata[]>>("/workflows/step-types");
  if (!response.data) {
    throw new Error(response.message || "获取步骤类型失败");
  }
  return response.data;
}

export async function registerWorkflow(request: RegisterWorkflowDefinitionRequest) {
  const response = await requestApi<ApiResponse<{ success: boolean; workflowId: string; version: number }>>(
    "/workflows/definitions",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "注册工作流失败");
  }
  return response.data;
}

export async function startWorkflow(data: { workflowId: string; version?: number; data?: any; reference?: string }) {
  const response = await requestApi<ApiResponse<{ instanceId: string }>>("/workflows/instances", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data)
  });
  if (!response.data) {
    throw new Error(response.message || "启动工作流失败");
  }
  return response.data.instanceId;
}

export async function getWorkflowInstance(instanceId: string): Promise<WorkflowInstanceResponse> {
  const response = await requestApi<ApiResponse<WorkflowInstanceResponse>>(`/workflows/instances/${instanceId}`);
  if (!response.data) {
    throw new Error(response.message || "获取工作流实例失败");
  }
  return response.data;
}

export async function getWorkflowInstances(pagedRequest: PagedRequest): Promise<PagedResult<WorkflowInstanceListItem>> {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<WorkflowInstanceListItem>>>(
    `/workflows/instances?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询工作流实例失败");
  }
  return response.data;
}

export async function getExecutionPointers(instanceId: string): Promise<ExecutionPointerResponse[]> {
  const response = await requestApi<ApiResponse<ExecutionPointerResponse[]>>(
    `/workflows/instances/${instanceId}/pointers`
  );
  if (!response.data) {
    throw new Error(response.message || "获取执行指针失败");
  }
  return response.data;
}

export async function suspendWorkflow(instanceId: string) {
  const response = await requestApi<ApiResponse<{ success: boolean }>>(
    `/workflows/instances/${instanceId}/suspend`,
    { method: "POST" }
  );
  if (!response.success) {
    throw new Error(response.message || "挂起工作流失败");
  }
}

export async function resumeWorkflow(instanceId: string) {
  const response = await requestApi<ApiResponse<{ success: boolean }>>(
    `/workflows/instances/${instanceId}/resume`,
    { method: "POST" }
  );
  if (!response.success) {
    throw new Error(response.message || "恢复工作流失败");
  }
}

export async function terminateWorkflow(instanceId: string) {
  const response = await requestApi<ApiResponse<{ success: boolean }>>(
    `/workflows/instances/${instanceId}/terminate`,
    { method: "POST" }
  );
  if (!response.success) {
    throw new Error(response.message || "终止工作流失败");
  }
}

// Visualization module
export async function getVisualizationOverview(params?: {
  department?: string;
  flowType?: string;
  from?: string;
  to?: string;
}): Promise<VisualizationOverview> {
  const query = params
    ? new URLSearchParams(
        Object.entries(params).reduce((acc, [k, v]) => {
          if (v) acc[k] = v;
          return acc;
        }, {} as Record<string, string>)
      ).toString()
    : "";
  const response = await requestApi<ApiResponse<VisualizationOverview>>(
    `/visualization/overview${query ? `?${query}` : ""}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取概览失败");
  }
  return response.data;
}

export async function getVisualizationProcesses(
  pagedRequest: PagedRequest
): Promise<PagedResult<VisualizationProcessSummary>> {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<VisualizationProcessSummary>>>(
    `/visualization/processes?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取流程列表失败");
  }
  return response.data;
}

export async function getVisualizationInstances(
  pagedRequest: PagedRequest,
  params?: { processId?: string; status?: string }
): Promise<PagedResult<VisualizationInstanceSummary>> {
  const queryParams = new URLSearchParams(toQuery(pagedRequest));
  if (params?.processId) {
    queryParams.append("processId", params.processId);
  }
  if (params?.status) {
    queryParams.append("status", params.status);
  }
  const query = queryParams.toString();
  const response = await requestApi<ApiResponse<PagedResult<VisualizationInstanceSummary>>>(
    `/visualization/instances?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取实例列表失败");
  }
  return response.data;
}

export async function validateVisualizationProcess(
  request: ValidateVisualizationRequest
): Promise<VisualizationValidationResult> {
  const response = await requestApi<ApiResponse<VisualizationValidationResult>>(
    "/visualization/processes/validate",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "校验失败");
  }
  return response.data;
}

export async function publishVisualizationProcess(
  request: PublishVisualizationRequest
): Promise<VisualizationPublishResult> {
  const response = await requestApi<ApiResponse<VisualizationPublishResult>>(
    "/visualization/processes/publish",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "发布失败");
  }
  return response.data;
}

export async function saveVisualizationProcess(
  request: SaveVisualizationProcessRequest
): Promise<SaveVisualizationProcessResult> {
  const isUpdate = Boolean(request.processId);
  const path = isUpdate
    ? `/visualization/processes/${request.processId}`
    : "/visualization/processes";
  const response = await requestApi<ApiResponse<SaveVisualizationProcessResult>>(path, {
    method: isUpdate ? "PUT" : "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "保存失败");
  }
  return response.data;
}

export async function getVisualizationProcessDetail(id: string): Promise<VisualizationProcessDetail> {
  const response = await requestApi<ApiResponse<VisualizationProcessDetail>>(`/visualization/processes/${id}`);
  if (!response.data) {
    throw new Error(response.message || "获取流程详情失败");
  }
  return response.data;
}

export async function getVisualizationInstanceDetail(
  id: string
): Promise<VisualizationInstanceDetail> {
  const response = await requestApi<ApiResponse<VisualizationInstanceDetail>>(
    `/visualization/instances/${id}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取实例详情失败");
  }
  return response.data;
}

export async function getVisualizationMetrics(params?: {
  department?: string;
  flowType?: string;
  from?: string;
  to?: string;
}): Promise<VisualizationMetricsResponse> {
  const query = params
    ? new URLSearchParams(
        Object.entries(params).reduce((acc, [k, v]) => {
          if (v) acc[k] = v;
          return acc;
        }, {} as Record<string, string>)
      ).toString()
    : "";
  const response = await requestApi<ApiResponse<VisualizationMetricsResponse>>(
    `/visualization/metrics${query ? `?${query}` : ""}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取指标失败");
  }
  return response.data;
}

export async function getVisualizationAudit(
  pagedRequest: PagedRequest
): Promise<PagedResult<AuditListItem>> {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<AuditListItem>>>(`/visualization/audit?${query}`);
  if (!response.data) {
    throw new Error(response.message || "获取审计记录失败");
  }
  return response.data;
}

async function requestApi<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers ?? {});
  const token = localStorage.getItem("access_token");
  const tenantId = localStorage.getItem("tenant_id");

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  if (tenantId && !headers.has("X-Tenant-Id")) {
    headers.set("X-Tenant-Id", tenantId);
  }

  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers
  });

  if (!response.ok) {
    const text = await response.text();
    message.error(text || "网络请求失败");
    throw new Error(text || "网络请求失败");
  }

  return (await response.json()) as T;
}
