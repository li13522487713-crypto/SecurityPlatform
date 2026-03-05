// 审批流/实例/任务/代理人/部门负责人模块 API
import type {
  ApiResponse,
  PagedRequest,
  PagedResult,
  ApprovalFlowDefinitionListItem,
  ApprovalFlowDefinitionResponse,
  ApprovalFlowDefinitionCreateRequest,
  ApprovalFlowDefinitionUpdateRequest,
  ApprovalFlowPublishRequest,
  ApprovalFlowValidationResult,
  ApprovalFlowCopyRequest,
  ApprovalFlowImportRequest,
  ApprovalFlowExportResponse,
  ApprovalFlowCompareResponse,
  ApprovalStartRequest,
  ApprovalTaskResponse,
  ApprovalTaskDecideRequest,
  ApprovalInstanceListItem,
  ApprovalInstanceResponse,
  ApprovalCopyRecordResponse
} from "@/types/api";
import type {
  ApprovalHistoryEventDto,
  ApprovalInstanceDetailDto
} from "@/types/approval-instance-detail";
import type {
  FlowDefinition,
  FlowSaveRequest,
  FlowSaveResponse,
  FlowPublishResponse,
  FlowValidationResult
} from "@/types/workflow";
import { requestApi, toQuery } from "@/services/api-core";

function toApprovalFlowRequest(req: FlowSaveRequest): ApprovalFlowDefinitionCreateRequest {
  return {
    name: req.definition.name || req.definition.code || "未命名流程",
    definitionJson: JSON.stringify(req.definition),
    description: req.definition.remark || undefined
  };
}

function parseFlowDefinition(definitionJson: string): FlowDefinition {
  return JSON.parse(definitionJson) as FlowDefinition;
}

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

export async function copyApprovalFlow(id: string, request?: ApprovalFlowCopyRequest) {
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>(`/approval/flows/${id}/copy`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request || {})
  });
  if (!response.data) {
    throw new Error(response.message || "复制失败");
  }
  return response.data;
}

export async function importApprovalFlow(request: ApprovalFlowImportRequest) {
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>("/approval/flows/import", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "导入失败");
  }
  return response.data;
}

export async function exportApprovalFlow(id: string) {
  const response = await requestApi<ApiResponse<ApprovalFlowExportResponse>>(`/approval/flows/${id}/export`);
  if (!response.data) {
    throw new Error(response.message || "导出失败");
  }
  return response.data;
}

export async function compareApprovalFlowVersion(id: string, targetVersion: number) {
  const response = await requestApi<ApiResponse<ApprovalFlowCompareResponse>>(`/approval/flows/${id}/versions/${targetVersion}/compare`);
  if (!response.data) {
    throw new Error(response.message || "版本对比失败");
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
  const response = await requestApi<ApiResponse<void>>(`/approval/flows/${id}/publication`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request || {})
  });
  if (!response.success) {
    throw new Error(response.message || "发布失败");
  }
}

export async function validateApprovalFlow(request: ApprovalFlowDefinitionCreateRequest): Promise<ApprovalFlowValidationResult> {
  const response = await requestApi<ApiResponse<ApprovalFlowValidationResult>>("/approval/flows/validation", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "校验失败");
  }
  return response.data;
}

export async function disableApprovalFlow(id: string) {
  const response = await requestApi<ApiResponse<void>>(`/approval/flows/${id}/deactivation`, {
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

export async function getAdminInstancesPaged(
  pagedRequest: PagedRequest,
  filters?: {
    status?: number;
    definitionId?: string;
    initiatorUserId?: string;
    businessKey?: string;
    startedFrom?: string;
    startedTo?: string;
  }
) {
  const params = new URLSearchParams(toQuery(pagedRequest));
  if (filters?.status !== undefined) params.append("status", String(filters.status));
  if (filters?.definitionId) params.append("definitionId", filters.definitionId);
  if (filters?.initiatorUserId) params.append("initiatorUserId", filters.initiatorUserId);
  if (filters?.businessKey) params.append("businessKey", filters.businessKey);
  if (filters?.startedFrom) params.append("startedFrom", filters.startedFrom);
  if (filters?.startedTo) params.append("startedTo", filters.startedTo);

  const response = await requestApi<ApiResponse<PagedResult<ApprovalInstanceListItem>>>(
    `/approval/instances/admin?${params.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getApprovalInstanceById(id: string): Promise<ApprovalInstanceDetailDto> {
  const response = await requestApi<ApiResponse<ApprovalInstanceDetailDto>>(`/approval/instances/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getApprovalInstanceHistory(
  id: string,
  pagedRequest: PagedRequest
): Promise<PagedResult<ApprovalHistoryEventDto>> {
  const params = new URLSearchParams(toQuery(pagedRequest));
  const response = await requestApi<ApiResponse<PagedResult<ApprovalHistoryEventDto>>>(
    `/approval/instances/${id}/history?${params.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function cancelApprovalInstance(id: string) {
  const response = await requestApi<ApiResponse<void>>(`/approval/instances/${id}/cancellation`, {
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

export async function getApprovalTaskById(id: string) {
  const response = await requestApi<ApiResponse<ApprovalTaskResponse>>(`/approval/tasks/${id}`);
  if (!response.data) {
    throw new Error(response.message || "任务不存在");
  }
  return response.data;
}

export async function getApprovalTasksByInstance(instanceId: string, pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<ApprovalTaskResponse>>>(
    `/approval/instances/${instanceId}/tasks?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function decideApprovalTask(request: ApprovalTaskDecideRequest) {
  const response = await requestApi<ApiResponse<void>>(`/approval/tasks/${request.taskId}/decision`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "操作失败");
  }
}

export async function delegateTask(taskId: string, delegateeUserId: string, comment?: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/tasks/${taskId}/delegation?delegateeUserId=${delegateeUserId}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(comment || "")
  });
  if (!response.success) {
    throw new Error(response.message || "委派失败");
  }
}

export async function transferTask(instanceId: string, taskId: string, targetAssigneeValue: string, comment?: string) {
  const request = {
    operationType: 21, // Transfer
    targetAssigneeValue,
    comment
  };
  const response = await requestApi<ApiResponse<string>>(`/approval/instances/${instanceId}/operations?taskId=${taskId}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "转办失败");
  }
}

export async function resolveTask(taskId: string, comment?: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/tasks/${taskId}/resolution`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(comment || "")
  });
  if (!response.success) {
    throw new Error(response.message || "归还失败");
  }
}

export async function claimTask(taskId: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/tasks/${taskId}/claim`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "认领失败");
  }
}

export async function urgeTask(taskId: string, message?: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/tasks/${taskId}/urge`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(message || "")
  });
  if (!response.success) {
    throw new Error(response.message || "催办失败");
  }
}

export async function communicateTask(taskId: string, recipientUserId: string, content: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/tasks/${taskId}/communication?recipientUserId=${recipientUserId}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(content)
  });
  if (!response.success) {
    throw new Error(response.message || "沟通失败");
  }
}

export async function getCommunications(taskId: string) {
  const response = await requestApi<ApiResponse<ApprovalCommunicationMessage[]>>(`/approval/tasks/${taskId}/communications`);
  if (!response.data) {
    throw new Error(response.message || "获取沟通记录失败");
  }
  return response.data;
}

export interface ApprovalCommunicationMessage {
  id: string;
  senderUserId: string;
  senderName?: string;
  content: string;
  createdAt: string;
}

export async function jumpTask(instanceId: string, targetNodeId: string, taskId?: string) {
  const request = {
    operationType: 36, // Jump
    targetNodeId
  };
  const query = taskId ? `?taskId=${encodeURIComponent(taskId)}` : "";
  const response = await requestApi<ApiResponse<string>>(`/approval/instances/${instanceId}/operations${query}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "跳转失败");
  }
}

export async function reclaimTask(instanceId: string, taskId: string) {
  const request = {
    operationType: 37 // Reclaim
  };
  const response = await requestApi<ApiResponse<string>>(`/approval/instances/${instanceId}/operations?taskId=${taskId}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "拿回失败");
  }
}

export async function getTaskPool(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<ApprovalTaskResponse>>>(`/approval/tasks/pool?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function batchTransferTasks(fromUserId: string, toUserId: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/tasks/batch-transfer?fromUserId=${fromUserId}&toUserId=${toUserId}`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "转办失败");
  }
}

export async function suspendInstance(instanceId: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/instances/${instanceId}/suspension`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "挂起失败");
  }
}

export async function activateInstance(instanceId: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/instances/${instanceId}/activation`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "激活失败");
  }
}

export async function terminateInstance(instanceId: string, comment?: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/instances/${instanceId}/termination`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(comment || "")
  });
  if (!response.success) {
    throw new Error(response.message || "终止失败");
  }
}

export async function saveDraft(request: ApprovalStartRequest) {
  const response = await requestApi<ApiResponse<ApprovalInstanceResponse>>("/approval/instances/draft", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "保存草稿失败");
  }
  return response.data;
}

export async function submitDraft(instanceId: string) {
  const response = await requestApi<ApiResponse<ApprovalInstanceResponse>>(`/approval/instances/${instanceId}/submission`, {
    method: "POST"
  });
  if (!response.data) {
    throw new Error(response.message || "提交草稿失败");
  }
  return response.data;
}

export async function markTaskViewed(taskId: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/tasks/${taskId}/viewed`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "操作失败");
  }
}

export async function getMyCopyRecordsPaged(pagedRequest: PagedRequest, isRead?: boolean) {
  const params = new URLSearchParams(toQuery(pagedRequest));
  if (isRead !== undefined) {
    params.append("isRead", String(isRead));
  }
  const response = await requestApi<ApiResponse<PagedResult<ApprovalCopyRecordResponse>>>(
    `/approval/copy-records/my-copies?${params.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询抄送记录失败");
  }
  return response.data;
}

export async function markCopyRecordAsRead(copyRecordId: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/copy-records/${copyRecordId}/mark-read`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "标记已读失败");
  }
}

// ---------- Workflow Designer (FlowDefinition) ----------
export async function loadFlowDefinition(id: string): Promise<FlowDefinition> {
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>(`/approval/flows/${id}`);
  if (!response.data?.definitionJson) {
    throw new Error(response.message || "加载流程失败");
  }
  return parseFlowDefinition(response.data.definitionJson);
}

export async function saveFlowDefinition(req: FlowSaveRequest): Promise<FlowSaveResponse> {
  const payload = toApprovalFlowRequest(req);
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>("/approval/flows", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.data) {
    throw new Error(response.message || "保存流程失败");
  }
  return { id: response.data.id, version: response.data.version };
}

export async function updateFlowDefinition(id: string, req: FlowSaveRequest): Promise<void> {
  const payload = toApprovalFlowRequest(req);
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>(`/approval/flows/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.data) {
    throw new Error(response.message || "更新流程失败");
  }
}

export async function publishFlowDefinition(id: string): Promise<FlowPublishResponse> {
  const response = await requestApi<ApiResponse<FlowPublishResponse>>(`/approval/flows/${id}/publication`, {
    method: "POST"
  });
  if (!response.data) {
    throw new Error(response.message || "发布流程失败");
  }
  return response.data;
}

export async function validateFlowDefinition(req: FlowSaveRequest): Promise<FlowValidationResult> {
  const payload = toApprovalFlowRequest(req);
  const response = await requestApi<ApiResponse<FlowValidationResult>>("/approval/flows/validation", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.data) {
    throw new Error(response.message || "校验失败");
  }
  return response.data;
}

export async function previewFlowDefinition(id: string): Promise<FlowDefinition> {
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>(`/approval/flows/${id}`);
  if (!response.data?.definitionJson) {
    throw new Error(response.message || "预览失败");
  }
  return parseFlowDefinition(response.data.definitionJson);
}

// ─── 审批代理人 ───────────────────────────────────────────────────────────────

export interface ApprovalAgentConfigResponse {
  id: string;
  agentUserId: string;
  principalUserId: string;
  startTime: string;
  endTime: string;
  isEnabled: boolean;
  createdAt: string;
}

export interface CreateApprovalAgentRequest {
  agentUserId: string;
  startTime: string;
  endTime: string;
}

export async function getMyAgentConfigs(): Promise<ApprovalAgentConfigResponse[]> {
  const response = await requestApi<ApiResponse<ApprovalAgentConfigResponse[]>>('/approval/agents');
  if (!response.data) {
    throw new Error(response.message || '获取代理设置失败');
  }
  return response.data;
}

export async function createAgentConfig(request: CreateApprovalAgentRequest): Promise<void> {
  const response = await requestApi<ApiResponse<string>>('/approval/agents', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  if (!response.success) {
    throw new Error(response.message || '创建代理设置失败');
  }
}

export async function deleteAgentConfig(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<string>>(`/approval/agents/${id}`, {
    method: 'DELETE',
  });
  if (!response.success) {
    throw new Error(response.message || '删除代理设置失败');
  }
}

// ─── 部门负责人 ───────────────────────────────────────────────────────────────

export interface SetDepartmentLeaderRequest {
  departmentId: string;
  leaderUserId: string;
}

export async function getDepartmentLeader(departmentId: string): Promise<string | null> {
  const response = await requestApi<ApiResponse<string | null>>(`/approval/department-leaders/${departmentId}`);
  return response.data ?? null;
}

export async function setDepartmentLeader(request: SetDepartmentLeaderRequest): Promise<void> {
  const response = await requestApi<ApiResponse<string>>('/approval/department-leaders', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  if (!response.success) {
    throw new Error(response.message || '设置部门负责人失败');
  }
}

export async function removeDepartmentLeader(departmentId: string): Promise<void> {
  const response = await requestApi<ApiResponse<string>>(`/approval/department-leaders/${departmentId}`, {
    method: 'DELETE',
  });
  if (!response.success) {
    throw new Error(response.message || '移除部门负责人失败');
  }
}
