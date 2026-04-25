import type { ApiResponse } from "@atlas/shared-react-core/types";
import { requestApi, toQuery } from "./api-core";

export type DatabaseObjectType = "table" | "view" | "procedure" | "trigger";
export type DatabaseEnvironment = "Draft" | "Online";

export interface DatabaseObjectDto {
  name: string;
  objectType: DatabaseObjectType;
  schema?: string;
  engine?: string;
  algorithm?: string;
  rowCount?: number | string;
  comment?: string;
  createdAt?: string;
  updatedAt?: string;
  status?: string;
  canPreview: boolean;
  canDrop: boolean;
}

export interface DatabaseColumnDto {
  name: string;
  dataType: string;
  rawDataType?: string;
  length?: number;
  precision?: number;
  scale?: number;
  nullable: boolean;
  primaryKey: boolean;
  autoIncrement: boolean;
  defaultValue?: string;
  comment?: string;
  ordinal: number;
}

export interface PreviewDataRequest {
  schema?: string;
  pageIndex: number;
  pageSize: number;
  orderBy?: string;
  environment?: DatabaseEnvironment;
}

export interface PreviewDataColumn {
  name: string;
  dataType: string;
}

export interface PreviewDataResponse {
  columns: PreviewDataColumn[];
  rows: Record<string, unknown>[];
  total: number;
  pageIndex: number;
  pageSize: number;
  truncated?: boolean;
  elapsedMs?: number;
}

export interface DdlResponse {
  ddl: string;
}

export interface TableColumnDesignDto {
  id: string;
  name: string;
  dataType: string;
  length?: number;
  precision?: number;
  scale?: number;
  nullable: boolean;
  primaryKey: boolean;
  autoIncrement: boolean;
  defaultValue?: string;
  comment?: string;
}

export interface TableOptionsDto {
  engine?: string;
  charset?: string;
  collation?: string;
  schema?: string;
  tablespace?: string;
  includeAuditFields?: boolean;
  extraOptions?: Record<string, string | null>;
}

export interface PreviewCreateTableDdlRequest {
  schema?: string;
  tableName: string;
  comment?: string;
  columns: TableColumnDesignDto[];
  options?: TableOptionsDto;
}

export interface CreateTableRequest extends PreviewCreateTableDdlRequest {
  mode?: "visual";
}

export interface CreateTableSqlRequest {
  sql: string;
}

export interface PreviewViewSqlRequest {
  sql: string;
  limit?: number;
}

export interface CreateViewRequest {
  schema?: string;
  viewName: string;
  comment?: string;
  sql: string;
  mode?: "SelectOnly" | "CreateViewSql";
}

export interface DropDatabaseObjectRequest {
  schema?: string;
  confirmName: string;
  confirmDanger: boolean;
}

function unwrap<T>(response: ApiResponse<T>): T {
  if (response.success === false || response.data == null) {
    throw new Error(response.message || "Database structure API request failed.");
  }
  return response.data;
}

function unwrapVoid(response: ApiResponse<unknown>): void {
  if (response.success === false) {
    throw new Error(response.message || "Database structure API request failed.");
  }
}

function base(databaseId: string): string {
  return `/database-resources/${encodeURIComponent(databaseId)}/structure`;
}

export async function listDatabaseObjects(databaseId: string, type: DatabaseObjectType): Promise<DatabaseObjectDto[]> {
  return unwrap(await requestApi<ApiResponse<DatabaseObjectDto[]>>(`${base(databaseId)}/objects?${toQuery({}, { type })}`));
}

export async function getTableColumns(databaseId: string, tableName: string, schema?: string): Promise<DatabaseColumnDto[]> {
  const query = schema ? `?${toQuery({}, { schema })}` : "";
  return unwrap(await requestApi<ApiResponse<DatabaseColumnDto[]>>(`${base(databaseId)}/tables/${encodeURIComponent(tableName)}/columns${query}`));
}

export async function getViewColumns(databaseId: string, viewName: string, schema?: string): Promise<DatabaseColumnDto[]> {
  const query = schema ? `?${toQuery({}, { schema })}` : "";
  return unwrap(await requestApi<ApiResponse<DatabaseColumnDto[]>>(`${base(databaseId)}/views/${encodeURIComponent(viewName)}/columns${query}`));
}

export async function getTableDdl(databaseId: string, tableName: string, schema?: string): Promise<DdlResponse> {
  const query = schema ? `?${toQuery({}, { schema })}` : "";
  return unwrap(await requestApi<ApiResponse<DdlResponse>>(`${base(databaseId)}/tables/${encodeURIComponent(tableName)}/ddl${query}`));
}

export async function getViewDdl(databaseId: string, viewName: string, schema?: string): Promise<DdlResponse> {
  const query = schema ? `?${toQuery({}, { schema })}` : "";
  return unwrap(await requestApi<ApiResponse<DdlResponse>>(`${base(databaseId)}/views/${encodeURIComponent(viewName)}/ddl${query}`));
}

export async function previewTableData(databaseId: string, tableName: string, request: PreviewDataRequest): Promise<PreviewDataResponse> {
  return unwrap(await requestApi<ApiResponse<PreviewDataResponse>>(`${base(databaseId)}/tables/${encodeURIComponent(tableName)}/preview`, {
    method: "POST",
    body: JSON.stringify(request)
  }));
}

export async function previewViewData(databaseId: string, viewName: string, request: PreviewDataRequest): Promise<PreviewDataResponse> {
  return unwrap(await requestApi<ApiResponse<PreviewDataResponse>>(`${base(databaseId)}/views/${encodeURIComponent(viewName)}/preview`, {
    method: "POST",
    body: JSON.stringify(request)
  }));
}

export async function previewCreateTableDdl(databaseId: string, request: PreviewCreateTableDdlRequest): Promise<DdlResponse> {
  return unwrap(await requestApi<ApiResponse<DdlResponse>>(`${base(databaseId)}/tables/preview-ddl`, {
    method: "POST",
    body: JSON.stringify(request)
  }));
}

export async function createTableVisual(databaseId: string, request: CreateTableRequest): Promise<void> {
  unwrapVoid(await requestApi<ApiResponse<unknown>>(`${base(databaseId)}/tables`, {
    method: "POST",
    body: JSON.stringify({ ...request, mode: "visual" })
  }));
}

export async function createTableSql(databaseId: string, request: CreateTableSqlRequest): Promise<void> {
  unwrapVoid(await requestApi<ApiResponse<unknown>>(`${base(databaseId)}/tables/sql`, {
    method: "POST",
    body: JSON.stringify(request)
  }));
}

export async function previewViewSql(databaseId: string, request: PreviewViewSqlRequest): Promise<PreviewDataResponse> {
  return unwrap(await requestApi<ApiResponse<PreviewDataResponse>>(`${base(databaseId)}/views/preview`, {
    method: "POST",
    body: JSON.stringify(request)
  }));
}

export async function createView(databaseId: string, request: CreateViewRequest): Promise<void> {
  unwrapVoid(await requestApi<ApiResponse<unknown>>(`${base(databaseId)}/views`, {
    method: "POST",
    body: JSON.stringify(request)
  }));
}

export async function dropTable(databaseId: string, tableName: string, request: DropDatabaseObjectRequest): Promise<void> {
  unwrapVoid(await requestApi<ApiResponse<unknown>>(`${base(databaseId)}/tables/${encodeURIComponent(tableName)}`, {
    method: "DELETE",
    body: JSON.stringify(request)
  }));
}

export async function dropView(databaseId: string, viewName: string, request: DropDatabaseObjectRequest): Promise<void> {
  unwrapVoid(await requestApi<ApiResponse<unknown>>(`${base(databaseId)}/views/${encodeURIComponent(viewName)}`, {
    method: "DELETE",
    body: JSON.stringify(request)
  }));
}
