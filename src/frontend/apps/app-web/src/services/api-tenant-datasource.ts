import type { ApiResponse } from "@atlas/shared-react-core/types";
import { extractResourceId, requestApi } from "./api-core";

export interface TenantDataSourceDto {
  id: string;
  tenantIdValue: string;
  name: string;
  dbType: string;
  driverCode: string;
  host: string | null;
  port: number | null;
  databaseName: string | null;
  maskedConnectionSummary: string | null;
  ownershipScope: string;
  ownerAppInstanceId: string | null;
  appId: string | null;
  maxPoolSize: number;
  connectionTimeoutSeconds: number;
  lastTestSuccess: boolean | null;
  lastTestedAt: string | null;
  lastTestMessage: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface TenantDataSourceMutationRequest {
  tenantIdValue?: string;
  name: string;
  connectionString?: string;
  dbType: string;
  ownershipScope?: string;
  ownerAppInstanceId?: string | null;
  appId?: string | null;
  mode?: "raw" | "visual";
  visualConfig?: Record<string, string> | null;
  maxPoolSize?: number;
  connectionTimeoutSeconds?: number;
}

export interface TenantDataSourceCreateRequest extends TenantDataSourceMutationRequest {
  connectionString: string;
}

export interface TestConnectionRequest {
  connectionString?: string;
  dbType: string;
  mode?: "raw" | "visual";
  visualConfig?: Record<string, string> | null;
}

export interface TestConnectionResult {
  success: boolean;
  errorMessage?: string | null;
  latencyMs?: number | null;
}

export interface DataSourceDriverFieldDefinition {
  key: string;
  label: string;
  inputType: string;
  required: boolean;
  secret: boolean;
  multiline: boolean;
  placeholder?: string | null;
  defaultValue?: string | null;
}

export interface DataSourceDriverDefinition {
  code: string;
  displayName: string;
  supportsVisual: boolean;
  connectionStringExample: string;
  fields: DataSourceDriverFieldDefinition[];
}

function buildApiError(message?: string | null, traceId?: string | null): Error {
  const normalized = String(message ?? "").trim() || "请求失败";
  return new Error(traceId ? `${normalized} (traceId: ${traceId})` : normalized);
}

function assertApiSuccess<T>(response: ApiResponse<T>, fallbackMessage: string): T {
  if (!response.success || response.data === undefined || response.data === null) {
    throw buildApiError(response.message || fallbackMessage, response.traceId);
  }

  return response.data;
}

export function formatTenantDataSourceSummary(dataSource: Pick<TenantDataSourceDto, "dbType" | "host" | "port" | "databaseName" | "maskedConnectionSummary">): string {
  if (dataSource.maskedConnectionSummary) {
    return dataSource.maskedConnectionSummary;
  }

  const segments = [dataSource.dbType];
  if (dataSource.host) {
    segments.push(dataSource.port ? `${dataSource.host}:${dataSource.port}` : dataSource.host);
  }
  if (dataSource.databaseName) {
    segments.push(dataSource.databaseName);
  }
  return segments.join(" / ");
}

export async function listTenantDataSources(): Promise<TenantDataSourceDto[]> {
  const response = await requestApi<ApiResponse<TenantDataSourceDto[]>>("/tenant-datasources");
  return assertApiSuccess(response, "获取数据源列表失败");
}

export async function getTenantDataSource(id: string | number): Promise<TenantDataSourceDto> {
  const all = await listTenantDataSources();
  const match = all.find(item => item.id === String(id));
  if (!match) {
    throw new Error("数据源不存在");
  }
  return match;
}

export async function createTenantDataSource(request: TenantDataSourceCreateRequest): Promise<string> {
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string }>>("/tenant-datasources", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  const id = extractResourceId(response.data);
  if (!response.success || !id) {
    throw buildApiError(response.message || "创建数据源失败", response.traceId);
  }

  return id;
}

export async function updateTenantDataSource(id: string | number, request: TenantDataSourceMutationRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/tenant-datasources/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw buildApiError(response.message || "更新数据源失败", response.traceId);
  }
}

export async function deleteTenantDataSource(id: string | number): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/tenant-datasources/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw buildApiError(response.message || "删除数据源失败", response.traceId);
  }
}

export async function testTenantDataSourceConnection(request: TestConnectionRequest): Promise<TestConnectionResult> {
  const response = await requestApi<ApiResponse<TestConnectionResult>>("/tenant-datasources/test", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  return assertApiSuccess(response, "测试数据源连接失败");
}

export async function testTenantDataSourceById(id: string | number): Promise<TestConnectionResult> {
  const response = await requestApi<ApiResponse<TestConnectionResult>>(`/tenant-datasources/${id}/test`, {
    method: "POST"
  });
  return assertApiSuccess(response, "测试数据源连接失败");
}

export async function getTenantDataSourceDrivers(): Promise<DataSourceDriverDefinition[]> {
  const response = await requestApi<ApiResponse<DataSourceDriverDefinition[]>>("/tenant-datasources/drivers");
  return assertApiSuccess(response, "获取驱动列表失败");
}
