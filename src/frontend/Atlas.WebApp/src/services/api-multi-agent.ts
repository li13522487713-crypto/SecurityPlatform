import { API_BASE, requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import { getAccessToken, getAntiforgeryToken, getTenantId } from "@/utils/auth";

export type MultiAgentOrchestrationMode = 0 | 1;
export type MultiAgentOrchestrationStatus = 0 | 1 | 2;
export type MultiAgentExecutionStatus = 0 | 1 | 2 | 3 | 4 | 5;

export interface MultiAgentMemberInput {
  agentId: number;
  alias?: string;
  sortOrder: number;
  isEnabled: boolean;
  promptPrefix?: string;
}

export interface MultiAgentOrchestrationListItem {
  id: number;
  name: string;
  description?: string;
  mode: MultiAgentOrchestrationMode;
  status: MultiAgentOrchestrationStatus;
  memberCount: number;
  creatorUserId: number;
  createdAt: string;
  updatedAt: string;
}

export interface MultiAgentOrchestrationDetail extends MultiAgentOrchestrationListItem {
  members: MultiAgentMemberInput[];
}

export interface MultiAgentExecutionStep {
  agentId: number;
  agentName: string;
  alias?: string;
  inputMessage: string;
  outputMessage?: string;
  status: MultiAgentExecutionStatus;
  errorMessage?: string;
  startedAt: string;
  completedAt?: string;
}

export interface MultiAgentExecutionResult {
  executionId: number;
  orchestrationId: number;
  status: MultiAgentExecutionStatus;
  outputMessage?: string;
  errorMessage?: string;
  steps: MultiAgentExecutionStep[];
  startedAt: string;
  completedAt?: string;
}

export interface MultiAgentRunRequest {
  message: string;
  enableRag?: boolean;
}

export interface MultiAgentStreamEvent {
  eventType: string;
  data: string;
  parsed?: unknown;
}

export async function getMultiAgentOrchestrationsPaged(
  request: PagedRequest
) {
  const query = toQuery(request);
  const response = await requestApi<ApiResponse<PagedResult<MultiAgentOrchestrationListItem>>>(
    `/multi-agent-orchestrations?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询多 Agent 编排失败");
  }

  return response.data;
}

export async function getMultiAgentOrchestrationById(id: number) {
  const response = await requestApi<ApiResponse<MultiAgentOrchestrationDetail>>(
    `/multi-agent-orchestrations/${id}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询多 Agent 编排详情失败");
  }

  return response.data;
}

export async function createMultiAgentOrchestration(request: {
  name: string;
  description?: string;
  mode: MultiAgentOrchestrationMode;
  members: MultiAgentMemberInput[];
}) {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    "/multi-agent-orchestrations",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "创建多 Agent 编排失败");
  }

  return response.data?.id;
}

export async function updateMultiAgentOrchestration(
  id: number,
  request: {
    name: string;
    description?: string;
    mode: MultiAgentOrchestrationMode;
    members: MultiAgentMemberInput[];
    status?: MultiAgentOrchestrationStatus;
  }
) {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/multi-agent-orchestrations/${id}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新多 Agent 编排失败");
  }
}

export async function deleteMultiAgentOrchestration(id: number) {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/multi-agent-orchestrations/${id}`,
    { method: "DELETE" }
  );
  if (!response.success) {
    throw new Error(response.message || "删除多 Agent 编排失败");
  }
}

export async function runMultiAgentOrchestration(id: number, request: MultiAgentRunRequest) {
  const response = await requestApi<ApiResponse<MultiAgentExecutionResult>>(
    `/multi-agent-orchestrations/${id}/run`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "执行多 Agent 编排失败");
  }

  return response.data;
}

export async function getMultiAgentExecutionById(executionId: number) {
  const response = await requestApi<ApiResponse<MultiAgentExecutionResult>>(
    `/multi-agent-orchestrations/executions/${executionId}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询多 Agent 执行结果失败");
  }

  return response.data;
}

export async function streamMultiAgentOrchestration(
  id: number,
  request: MultiAgentRunRequest,
  onEvent: (event: MultiAgentStreamEvent) => void,
  signal?: AbortSignal
) {
  const headers = new Headers({
    "Content-Type": "application/json",
    Accept: "text/event-stream",
    "Idempotency-Key": crypto.randomUUID()
  });

  const token = getAccessToken();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const tenantId = getTenantId();
  if (tenantId) {
    headers.set("X-Tenant-Id", tenantId);
  }

  const csrfToken = getAntiforgeryToken();
  if (csrfToken) {
    headers.set("X-CSRF-TOKEN", csrfToken);
  }

  const response = await fetch(`${API_BASE}/multi-agent-orchestrations/${id}/stream`, {
    method: "POST",
    credentials: "include",
    headers,
    body: JSON.stringify(request),
    signal
  });

  if (!response.ok) {
    let message = "流式执行多 Agent 编排失败";
    try {
      const payload = (await response.json()) as ApiResponse<unknown>;
      if (payload?.message) {
        message = payload.message;
      }
    } catch {
      // ignored
    }
    throw new Error(message);
  }

  if (!response.body) {
    throw new Error("流式响应为空");
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  while (true) {
    const { value, done } = await reader.read();
    if (done) {
      break;
    }

    buffer += decoder.decode(value, { stream: true });
    const chunks = buffer.split("\n\n");
    buffer = chunks.pop() ?? "";
    chunks.forEach((chunk) => {
      const lines = chunk.split("\n");
      let eventType = "message";
      const dataLines: string[] = [];
      lines.forEach((line) => {
        if (line.startsWith("event:")) {
          eventType = line.slice("event:".length).trim();
        } else if (line.startsWith("data:")) {
          dataLines.push(line.slice("data:".length).trim());
        }
      });

      const data = dataLines.join("\n");
      if (!data) {
        return;
      }

      let parsed: unknown;
      try {
        parsed = JSON.parse(data);
      } catch {
        parsed = undefined;
      }
      onEvent({ eventType, data, parsed });
    });
  }
}
