// WorkflowCore 引擎模块 API
import type {
  ApiResponse,
  PagedRequest,
  PagedResult,
  StepTypeMetadata,
  RegisterWorkflowDefinitionRequest,
  ExecutionPointerResponse,
  WorkflowInstanceResponse,
  WorkflowInstanceListItem
} from "@/types/api";
import { requestApi, toQuery } from "@/services/api-core";

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

export async function startWorkflow(data: { workflowId: string; version?: number; data?: unknown; reference?: string }) {
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
