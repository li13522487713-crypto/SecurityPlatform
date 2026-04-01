import type { ApiResponse } from "@/types/api";
import { requestApi } from "@/services/api";

export type SchemaDraftObjectType = "Table" | "Field" | "Index" | "Relation";
export type SchemaDraftChangeType = "Create" | "Update" | "Delete";
export type SchemaDraftRiskLevel = "Low" | "Medium" | "High";
export type SchemaDraftStatus = "Pending" | "Validated" | "Published" | "Abandoned";

export interface SchemaDraftListItem {
  id: string;
  objectType: SchemaDraftObjectType;
  objectId: string;
  objectKey: string;
  changeType: SchemaDraftChangeType;
  riskLevel: SchemaDraftRiskLevel;
  status: SchemaDraftStatus;
  validationMessage?: string | null;
  createdAt: string;
  createdBy: number;
}

export interface SchemaDraftCreateRequest {
  tableKey: string;
  objectType: SchemaDraftObjectType;
  objectId?: string | null;
  objectKey: string;
  changeType: SchemaDraftChangeType;
  afterSnapshot?: unknown;
}

export interface SchemaDraftPublishResult {
  publishedCount: number;
  failedCount: number;
  errors: string[];
}

export interface SchemaDraftValidateResult {
  isValid: boolean;
  messages: string[];
}

export async function listSchemaDrafts(tableKey: string): Promise<SchemaDraftListItem[]> {
  const response = await requestApi<ApiResponse<SchemaDraftListItem[]>>(
    `/api/v1/schema-drafts?tableKey=${encodeURIComponent(tableKey)}`
  );
  return response.data ?? [];
}

export async function createSchemaDraft(request: SchemaDraftCreateRequest): Promise<SchemaDraftListItem> {
  const response = await requestApi<ApiResponse<SchemaDraftListItem>>(
    "/api/v1/schema-drafts",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) throw new Error(response.message || "创建草稿失败");
  return response.data;
}

export async function validateSchemaDraft(draftId: string): Promise<SchemaDraftValidateResult> {
  const response = await requestApi<ApiResponse<SchemaDraftValidateResult>>(
    `/api/v1/schema-drafts/${encodeURIComponent(draftId)}/validate`,
    { method: "POST" }
  );
  return response.data ?? { isValid: false, messages: [] };
}

export async function publishSchemaDrafts(tableKey: string): Promise<SchemaDraftPublishResult> {
  const response = await requestApi<ApiResponse<SchemaDraftPublishResult>>(
    "/api/v1/schema-drafts/publish",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ tableKey })
    }
  );
  return response.data ?? { publishedCount: 0, failedCount: 0, errors: [] };
}

export async function abandonSchemaDraft(draftId: string): Promise<void> {
  await requestApi<ApiResponse<null>>(
    `/api/v1/schema-drafts/${encodeURIComponent(draftId)}/abandon`,
    { method: "POST" }
  );
}
