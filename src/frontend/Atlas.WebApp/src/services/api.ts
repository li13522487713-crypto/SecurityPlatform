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
  ApprovalHistoryEventResponse
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

export interface AuditListItem {
  id: string;
  actor: string;
  action: string;
  result: string;
  target: string;
  ipAddress?: string;
  userAgent?: string;
  occurredAt: string;
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
