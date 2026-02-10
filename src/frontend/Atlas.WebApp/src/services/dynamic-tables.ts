import type { ApiResponse, PagedRequest, PagedResult, JsonValue } from "@/types/api";
import type {
  DynamicTableListItem,
  DynamicTableDetail,
  DynamicTableCreateRequest,
  DynamicTableUpdateRequest,
  DynamicRecordUpsertRequest,
  DynamicRecordQueryRequest,
  DynamicRecordListResult
} from "@/types/dynamic-tables";
import { requestApi } from "@/services/api";

export async function getDynamicTablesPaged(pagedRequest: PagedRequest) {
  const query = new URLSearchParams({
    pageIndex: pagedRequest.pageIndex.toString(),
    pageSize: pagedRequest.pageSize.toString(),
    keyword: pagedRequest.keyword ?? ""
  }).toString();
  const response = await requestApi<ApiResponse<PagedResult<DynamicTableListItem>>>(`/dynamic-tables?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getDynamicTableDetail(tableKey: string) {
  const response = await requestApi<ApiResponse<DynamicTableDetail | null>>(`/dynamic-tables/${encodeURIComponent(tableKey)}`);
  return response.data ?? null;
}

export async function createDynamicTable(request: DynamicTableCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/dynamic-tables", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建失败");
  }
  return response.data;
}

export async function updateDynamicTable(tableKey: string, request: DynamicTableUpdateRequest) {
  const response = await requestApi<ApiResponse<{ tableKey: string }>>(`/dynamic-tables/${encodeURIComponent(tableKey)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function deleteDynamicTable(tableKey: string) {
  const response = await requestApi<ApiResponse<{ tableKey: string }>>(`/dynamic-tables/${encodeURIComponent(tableKey)}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function createDynamicRecord(tableKey: string, request: DynamicRecordUpsertRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dynamic-tables/${encodeURIComponent(tableKey)}/records`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建失败");
  }
  return response.data;
}

export async function updateDynamicRecord(
  tableKey: string,
  id: string,
  request: DynamicRecordUpsertRequest
) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dynamic-tables/${encodeURIComponent(tableKey)}/records/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function deleteDynamicRecord(tableKey: string, id: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/dynamic-tables/${encodeURIComponent(tableKey)}/records/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function queryDynamicRecords(
  tableKey: string,
  request: DynamicRecordQueryRequest
): Promise<DynamicRecordListResult> {
  const response = await requestApi<ApiResponse<DynamicRecordListResult>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/records/query`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getDynamicAmisSchema(path: string): Promise<JsonValue> {
  const response = await requestApi<ApiResponse<JsonValue>>(`/amis/dynamic-tables/${path}`);
  if (!response.data) {
    throw new Error(response.message || "加载 AMIS Schema 失败");
  }
  return response.data;
}

// ---- 审批流绑定 ----

export interface ApprovalBindingRequest {
  approvalFlowDefinitionId: number | null;
  approvalStatusField: string | null;
}

export interface ApprovalSubmitResponse {
  instanceId: string;
  recordId: string;
  status: string;
}

export async function bindApprovalFlow(tableKey: string, request: ApprovalBindingRequest) {
  const response = await requestApi<ApiResponse<{ tableKey: string }>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/approval-binding`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "绑定失败");
  }
}

export async function submitRecordApproval(
  tableKey: string,
  recordId: string
): Promise<ApprovalSubmitResponse> {
  const response = await requestApi<ApiResponse<ApprovalSubmitResponse>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/records/${recordId}/approval`,
    {
      method: "POST"
    }
  );
  if (!response.data) {
    throw new Error(response.message || "提交审批失败");
  }
  return response.data;
}
