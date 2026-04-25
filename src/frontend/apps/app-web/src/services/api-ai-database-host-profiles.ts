import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { extractResourceId, requestApi, toQuery } from "./api-core";

export type AiDatabaseHostProfileDriverCode = "SQLite" | "MySql" | "PostgreSQL" | "SqlServer" | "Oracle" | "Dm" | "Kdbndp" | string;
export type AiDatabaseHostProfileStatus = "Ready" | "Pending" | "Disabled" | "Failed" | string;

export interface AiDatabaseHostProfile {
  id: string;
  name: string;
  driverCode: AiDatabaseHostProfileDriverCode;
  description?: string | null;
  maskedConnectionSummary?: string | null;
  defaultDatabaseName?: string | null;
  defaultSchemaName?: string | null;
  maxPoolSize?: number | null;
  connectionTimeoutSeconds?: number | null;
  isDefault: boolean;
  isActive: boolean;
  status?: AiDatabaseHostProfileStatus | null;
  lastTestSuccess?: boolean | null;
  lastTestedAt?: string | null;
  lastTestMessage?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface AiDatabaseHostProfileMutationRequest {
  name: string;
  driverCode: AiDatabaseHostProfileDriverCode;
  description?: string | null;
  connectionString?: string | null;
  defaultDatabaseName?: string | null;
  defaultSchemaName?: string | null;
  maxPoolSize?: number | null;
  connectionTimeoutSeconds?: number | null;
  isDefault?: boolean;
  isActive?: boolean;
}

export interface AiDatabaseHostProfileQuery extends Partial<PagedRequest> {
  keyword?: string;
  driverCode?: string;
  activeOnly?: boolean;
}

export interface AiDatabaseHostProfileTestRequest {
  driverCode: AiDatabaseHostProfileDriverCode;
  connectionString?: string | null;
  profileId?: string;
}

export interface AiDatabaseHostProfileTestResult {
  success: boolean;
  message?: string | null;
  latencyMs?: number | null;
  serverVersion?: string | null;
}

function buildError(message?: string | null, traceId?: string | null): Error {
  const normalized = String(message ?? "").trim() || "Host profile API request failed.";
  return new Error(traceId ? `${normalized} (traceId: ${traceId})` : normalized);
}

function unwrap<T>(response: ApiResponse<T>, fallbackMessage: string): T {
  if (!response.success || response.data == null) {
    throw buildError(response.message || fallbackMessage, response.traceId);
  }

  return response.data;
}

function unwrapVoid(response: ApiResponse<unknown>, fallbackMessage: string): void {
  if (!response.success) {
    throw buildError(response.message || fallbackMessage, response.traceId);
  }
}

export async function listAiDatabaseHostProfiles(query: AiDatabaseHostProfileQuery = {}): Promise<PagedResult<AiDatabaseHostProfile>> {
  const response = await requestApi<ApiResponse<PagedResult<AiDatabaseHostProfile>>>(`/ai-database-host-profiles?${toQuery({
    pageIndex: query.pageIndex ?? 1,
    pageSize: query.pageSize ?? 20,
    keyword: query.keyword,
    sortBy: query.sortBy,
    sortDesc: query.sortDesc
  }, {
    driverCode: query.driverCode,
    activeOnly: query.activeOnly == null ? undefined : String(query.activeOnly)
  })}`);
  const result = unwrap(response, "获取托管配置失败");
  return { ...result, items: result.items.map(normalizeProfile) };
}

export async function getAiDatabaseHostProfile(id: string): Promise<AiDatabaseHostProfile> {
  const response = await requestApi<ApiResponse<AiDatabaseHostProfile>>(`/ai-database-host-profiles/${encodeURIComponent(id)}`);
  return normalizeProfile(unwrap(response, "获取托管配置详情失败"));
}

export async function createAiDatabaseHostProfile(request: AiDatabaseHostProfileMutationRequest): Promise<string> {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>("/ai-database-host-profiles", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(toBackendRequest(request))
  });
  const id = extractResourceId(response.data);
  if (!response.success || !id) {
    throw buildError(response.message || "创建托管配置失败", response.traceId);
  }

  return id;
}

export async function updateAiDatabaseHostProfile(id: string, request: AiDatabaseHostProfileMutationRequest): Promise<void> {
  const response = await requestApi<ApiResponse<unknown>>(`/ai-database-host-profiles/${encodeURIComponent(id)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(toBackendRequest(request))
  });
  unwrapVoid(response, "更新托管配置失败");
}

function normalizeProfile(profile: AiDatabaseHostProfile & Record<string, unknown>): AiDatabaseHostProfile {
  const testStatus = String(profile.status ?? profile.testStatus ?? "");
  return {
    ...profile,
    isActive: Boolean(profile.isActive ?? profile.isEnabled),
    status: profile.status ?? testStatus,
    lastTestSuccess: profile.lastTestSuccess ?? (testStatus ? testStatus === "Success" : null),
    lastTestedAt: profile.lastTestedAt ?? (profile.lastTestAt as string | null | undefined),
    defaultDatabaseName: profile.defaultDatabaseName ?? (profile.adminDatabase as string | null | undefined),
    defaultSchemaName: profile.defaultSchemaName ?? (profile.defaultSchema as string | null | undefined),
    updatedAt: profile.updatedAt ?? null
  };
}

function toBackendRequest(request: AiDatabaseHostProfileMutationRequest) {
  return {
    name: request.name,
    driverCode: request.driverCode,
    provisionMode: request.driverCode === "SQLite" ? "SQLiteFile" : request.driverCode === "MySql" ? "MySqlDatabase" : request.driverCode === "PostgreSQL" ? "PostgreSqlSchema" : "ExistingDatabase",
    adminConnection: request.connectionString,
    defaultSchema: request.defaultSchemaName,
    adminDatabase: request.defaultDatabaseName,
    isDefault: request.isDefault ?? false,
    isEnabled: request.isActive ?? true
  };
}

export async function deleteAiDatabaseHostProfile(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<unknown>>(`/ai-database-host-profiles/${encodeURIComponent(id)}`, {
    method: "DELETE"
  });
  unwrapVoid(response, "删除托管配置失败");
}

export async function testAiDatabaseHostProfile(request: AiDatabaseHostProfileTestRequest): Promise<AiDatabaseHostProfileTestResult> {
  const response = await requestApi<ApiResponse<AiDatabaseHostProfileTestResult>>("/ai-database-host-profiles/test", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  return unwrap(response, "测试托管配置连接失败");
}
