import { requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";

export type TeamStatus = "Draft" | "Ready" | "Published" | "Disabled" | "Archived";
export type TeamPublishStatus = "Unpublished" | "PendingApproval" | "Published";
export type TeamRiskLevel = "Low" | "Medium" | "High";
export type NodeRunStatus =
  | "Idle"
  | "Ready"
  | "WaitingDependency"
  | "Assigned"
  | "Running"
  | "WaitingInput"
  | "WaitingTool"
  | "WaitingApproval"
  | "Retrying"
  | "Succeeded"
  | "Failed"
  | "Skipped"
  | "Cancelled";

export interface AgentTeamListItem {
  id: number;
  teamName: string;
  description?: string;
  owner: string;
  status: TeamStatus;
  publishStatus: TeamPublishStatus;
  publishedVersionId?: number;
  riskLevel: TeamRiskLevel;
  version: number;
  updatedAt: string;
}

export interface AgentTeamDetail {
  id: number;
  teamName: string;
  description?: string;
  owner: string;
  collaborators: string[];
  status: TeamStatus;
  publishStatus: TeamPublishStatus;
  publishedVersionId?: number;
  riskLevel: TeamRiskLevel;
  version: number;
  tags: string[];
  defaultModelPolicyJson: string;
  budgetPolicyJson: string;
  permissionScopeJson: string;
  createdAt: string;
  updatedAt: string;
}

export interface SubAgentItem {
  id: number;
  teamId: number;
  agentName: string;
  role: string;
  goal: string;
  status: string;
  updatedAt: string;
}

export interface OrchestrationNodeItem {
  id: number;
  teamId: number;
  nodeName: string;
  nodeType: string;
  bindAgentId?: number;
  dependencies: number[];
  executionMode: string;
  humanApprovalRequired: boolean;
  isCritical: boolean;
  skipAllowed: boolean;
  updatedAt: string;
}

export interface TeamVersionItem {
  id: number;
  teamId: number;
  versionNo: string;
  publishStatus: TeamPublishStatus;
  publishedBy?: string;
  publishedAt?: string;
  rollbackFromVersionId?: number;
}

export interface AgentTeamRunDetail {
  id: number;
  teamId: number;
  teamVersionId: number;
  currentState: string;
  inputPayloadJson: string;
  outputResultJson: string;
  outputSummary?: string;
  errorRecordsJson: string;
  startedAt: string;
  endedAt?: string;
}

export interface NodeRunItem {
  id: number;
  runId: number;
  nodeId: number;
  agentId?: number;
  state: NodeRunStatus;
  retryCount: number;
  inputSnapshotJson: string;
  outputSnapshotJson: string;
  errorCode?: string;
  errorMessage?: string;
  startedAt: string;
  endedAt?: string;
  humanInterventionAllowed: boolean;
}

export interface AgentTeamDebugResult {
  success: boolean;
  message: string;
  outputJson: string;
  nodeRuns: NodeRunItem[];
}

export async function getAgentTeamPaged(request: PagedRequest & {
  status?: TeamStatus;
  publishStatus?: TeamPublishStatus;
  riskLevel?: TeamRiskLevel;
}) {
  const response = await requestApi<ApiResponse<PagedResult<AgentTeamListItem>>>(`/agent-teams?${toQuery(request)}`);
  if (!response.data) {
    throw new Error(response.message || "查询 Agent 团队失败");
  }

  return response.data;
}

export async function getAgentTeamDetail(id: number) {
  const response = await requestApi<ApiResponse<AgentTeamDetail>>(`/agent-teams/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询 Agent 团队详情失败");
  }

  return response.data;
}

export async function createAgentTeam(payload: Record<string, unknown>) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/agent-teams", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.success) {
    throw new Error(response.message || "创建 Agent 团队失败");
  }

  return Number(response.data?.id || 0);
}

export async function updateAgentTeam(id: number, payload: Record<string, unknown>) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agent-teams/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.success) {
    throw new Error(response.message || "更新 Agent 团队失败");
  }
}

export async function deleteAgentTeam(id: number) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agent-teams/${id}`, { method: "DELETE" });
  if (!response.success) {
    throw new Error(response.message || "删除 Agent 团队失败");
  }
}

export async function duplicateAgentTeam(id: number) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agent-teams/${id}/duplicate`, { method: "POST" });
  if (!response.success) {
    throw new Error(response.message || "复制 Agent 团队失败");
  }

  return Number(response.data?.id || 0);
}

export async function publishAgentTeam(id: number, payload: { releaseNote?: string; requiresApproval?: boolean; approvalRecordId?: string } = {}) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agent-teams/${id}/publish`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.success) {
    throw new Error(response.message || "发布 Agent 团队失败");
  }
}

export async function getSubAgents(teamId: number) {
  const response = await requestApi<ApiResponse<SubAgentItem[]>>(`/agent-teams/${teamId}/sub-agents`);
  if (!response.data) {
    throw new Error(response.message || "查询子代理失败");
  }

  return response.data;
}

export async function createSubAgent(teamId: number, payload: Record<string, unknown>) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agent-teams/${teamId}/sub-agents`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.success) {
    throw new Error(response.message || "创建子代理失败");
  }

  return Number(response.data?.id || 0);
}

export async function updateSubAgent(teamId: number, subAgentId: number, payload: Record<string, unknown>) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agent-teams/${teamId}/sub-agents/${subAgentId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.success) {
    throw new Error(response.message || "更新子代理失败");
  }
}

export async function getOrchestrationNodes(teamId: number) {
  const response = await requestApi<ApiResponse<OrchestrationNodeItem[]>>(`/agent-teams/${teamId}/nodes`);
  if (!response.data) {
    throw new Error(response.message || "查询编排节点失败");
  }

  return response.data;
}

export async function validateOrchestration(teamId: number) {
  const response = await requestApi<ApiResponse<{ valid: boolean }>>(`/agent-teams/${teamId}/nodes/validate`, { method: "POST" });
  if (!response.success) {
    throw new Error(response.message || "编排校验失败");
  }
}

export async function getTeamVersions(teamId: number) {
  const response = await requestApi<ApiResponse<TeamVersionItem[]>>(`/agent-teams/${teamId}/versions`);
  if (!response.data) {
    throw new Error(response.message || "查询版本失败");
  }

  return response.data;
}

export async function rollbackTeamVersion(teamId: number, versionId: number) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agent-teams/${teamId}/rollback/${versionId}`, { method: "POST" });
  if (!response.success) {
    throw new Error(response.message || "回滚失败");
  }
}

export async function createAgentTeamRun(payload: {
  teamId: number;
  teamVersionId: number;
  triggerType: "Manual" | "Api" | "Schedule" | "Event";
  inputPayloadJson: string;
}) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/agent-team-runs", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.success) {
    throw new Error(response.message || "创建执行失败");
  }

  return Number(response.data?.id || 0);
}

export async function getAgentTeamRun(runId: number) {
  const response = await requestApi<ApiResponse<AgentTeamRunDetail>>(`/agent-team-runs/${runId}`);
  if (!response.data) {
    throw new Error(response.message || "查询执行详情失败");
  }

  return response.data;
}

export async function getAgentTeamRunNodes(runId: number) {
  const response = await requestApi<ApiResponse<NodeRunItem[]>>(`/agent-team-runs/${runId}/nodes`);
  if (!response.data) {
    throw new Error(response.message || "查询节点执行失败");
  }

  return response.data;
}

export async function getRunInterventions(runId: number) {
  const response = await requestApi<ApiResponse<NodeRunItem[]>>(`/agent-team-runs/${runId}/interventions`);
  if (!response.data) {
    throw new Error(response.message || "查询介入节点失败");
  }

  return response.data;
}

export async function interveneRunNode(runId: number, nodeRunId: number, payload: { action: "confirm" | "skip" | "retry" | "override"; payloadJson?: string }) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/agent-team-runs/${runId}/nodes/${nodeRunId}/intervene`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.success) {
    throw new Error(response.message || "介入失败");
  }
}

export async function debugAgentTeam(teamId: number, payload: {
  inputPayloadJson: string;
  fullChain: boolean;
  nodeId?: number;
  subAgentId?: number;
}) {
  const response = await requestApi<ApiResponse<AgentTeamDebugResult>>(`/agent-teams/${teamId}/debug`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.data) {
    throw new Error(response.message || "调试失败");
  }

  return response.data;
}
