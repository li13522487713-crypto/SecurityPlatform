import type { ApiResponse, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi } from "./api-core";

export interface WorkflowCreateRequest {
  name: string;
  description?: string;
  mode: 0 | 1;
}

export interface WorkflowSaveRequest {
  canvasJson: string;
  commitId?: string;
}

export interface WorkflowListItem {
  id: string;
  name: string;
  description?: string;
  updatedAt?: string;
  createdAt?: string;
  publishedAt?: string;
  mode?: 0 | 1;
  status?: 0 | 1 | 2;
  latestVersionNumber?: number;
}

interface CozeResponse<T> {
  code?: number;
  msg?: string;
  data?: T;
}

function toApiResponse<T>(result: CozeResponse<T>, fallbackData?: T): ApiResponse<T> {
  const success = (result.code ?? -1) === 0;
  return {
    success,
    code: success ? "SUCCESS" : "COZE_API_ERROR",
    message: result.msg ?? (success ? "success" : "request failed"),
    traceId: "",
    data: result.data ?? fallbackData
  };
}

function normalizeMode(mode: 0 | 1): number {
  return mode === 1 ? 3 : 0;
}

export async function createWorkflow(request: WorkflowCreateRequest): Promise<ApiResponse<string>> {
  const result = await requestApi<CozeResponse<{ workflow_id?: string }>>("/api/workflow_api/create", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      name: request.name,
      desc: request.description ?? "",
      icon_uri: "",
      space_id: "",
      flow_mode: normalizeMode(request.mode)
    })
  });

  return toApiResponse<string>(
    {
      code: result.code,
      msg: result.msg,
      data: result.data?.workflow_id ?? ""
    },
    ""
  );
}

export async function saveWorkflowDraft(
  workflowId: string,
  request: WorkflowSaveRequest
): Promise<ApiResponse<boolean>> {
  const result = await requestApi<CozeResponse<object>>("/api/workflow_api/save", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      workflow_id: workflowId,
      schema: request.canvasJson,
      submit_commit_id: request.commitId ?? "atlas-auto"
    })
  });

  return toApiResponse<boolean>(
    {
      code: result.code,
      msg: result.msg,
      data: (result.code ?? -1) === 0
    },
    false
  );
}

export async function listWorkflows(
  pageIndex = 1,
  pageSize = 20,
  keyword?: string
): Promise<ApiResponse<PagedResult<WorkflowListItem>>> {
  const result = await requestApi<CozeResponse<{ workflow_list?: Array<Record<string, unknown>>; total?: number }>>(
    "/api/workflow_api/workflow_list",
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        page: pageIndex,
        size: pageSize,
        name: keyword ?? "",
        space_id: ""
      })
    }
  );

  const items = (result.data?.workflow_list ?? []).map((item) => {
    const workflowId = String(item.workflow_id ?? "");
    const updatedAtRaw = item.update_time;
    const createdAtRaw = item.create_time;
    const publishedAtRaw = item.publish_time;
    const updatedAt = typeof updatedAtRaw === "number" ? new Date(updatedAtRaw).toISOString() : undefined;
    const createdAt = typeof createdAtRaw === "number" ? new Date(createdAtRaw).toISOString() : undefined;
    const publishedAt = typeof publishedAtRaw === "number" ? new Date(publishedAtRaw).toISOString() : undefined;
    const modeValue = Number(item.flow_mode);

    return {
      id: workflowId,
      name: String(item.name ?? workflowId),
      description: typeof item.desc === "string" ? item.desc : undefined,
      updatedAt,
      createdAt,
      publishedAt,
      mode: modeValue === 3 ? 1 : 0,
      status: Number(item.status ?? 0) === 3 ? 1 : 0,
      latestVersionNumber: typeof item.workflow_version === "number" ? item.workflow_version : undefined
    } satisfies WorkflowListItem;
  });

  return toApiResponse<PagedResult<WorkflowListItem>>(
    {
      code: result.code,
      msg: result.msg,
      data: {
        items,
        total: result.data?.total ?? items.length,
        pageIndex,
        pageSize
      }
    },
    {
      items: [],
      total: 0,
      pageIndex,
      pageSize
    }
  );
}
