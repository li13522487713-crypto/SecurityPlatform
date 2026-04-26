import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";
import { extractResourceId, requestApi, toQuery } from "./api-core";

export type DatabaseCenterSourceKind = "AiDatabase" | "TenantDataSource" | "External" | string;
export type DatabaseCenterEnvironment = "Draft" | "Online";
export type DatabaseCenterObjectType = "table" | "view" | "procedure" | "function" | "trigger" | "event";
export type DatabaseCenterProvisionMode = "SQLiteFile" | "MySqlDatabase" | "PostgreSqlSchema" | "PostgreSqlDatabase" | "ExistingDatabase" | string;

export interface DatabaseCenterSourceSummary {
  id: string;
  aiDatabaseId?: string | null;
  name: string;
  sourceKind: DatabaseCenterSourceKind;
  driverCode: string;
  environment?: DatabaseCenterEnvironment | null;
  status?: string | null;
  readOnly?: boolean | null;
  address?: string | null;
  workspaceId?: string | null;
  description?: string | null;
  hostProfileId?: string | null;
  hostProfileName?: string | null;
  physicalDatabaseName?: string | null;
  defaultSchemaName?: string | null;
  maskedConnectionSummary?: string | null;
  provisionState?: string | null;
  draftObjectCount?: number | null;
  onlineObjectCount?: number | null;
  recordCount?: number | null;
  createdAt?: string | null;
  updatedAt?: string | null;
}

export interface DatabaseCenterSourceDetail extends DatabaseCenterSourceSummary {
  lastProvisionMessage?: string | null;
  lastProvisionedAt?: string | null;
  ownerUserId?: string | null;
  tags?: string[];
  capabilities?: {
    canQuery?: boolean;
    canWriteSchema?: boolean;
    canManageHostProfile?: boolean;
    canPreviewData?: boolean;
  };
}

export interface DatabaseCenterSourceQuery extends Partial<PagedRequest> {
  keyword?: string;
  workspaceId?: string;
  driverCode?: string;
  sourceKind?: DatabaseCenterSourceKind;
}

export interface DatabaseCenterSchemaSummary {
  name: string;
  environment: DatabaseCenterEnvironment;
  tableCount: number;
  viewCount: number;
  procedureCount?: number;
  triggerCount?: number;
  defaultSchema?: boolean;
}

export interface DatabaseCenterObjectSummary {
  id: string;
  name: string;
  objectType: DatabaseCenterObjectType;
  schema?: string | null;
  rowCount?: number | string | null;
  comment?: string | null;
  engine?: string | null;
  createdAt?: string | null;
  updatedAt?: string | null;
  canPreview?: boolean;
  canDrop?: boolean;
}

export interface DatabaseCenterColumnSummary {
  name: string;
  dataType: string;
  nullable: boolean;
  primaryKey: boolean;
  foreignKey?: boolean;
  referencesTable?: string | null;
  referencesColumn?: string | null;
  ordinal: number;
  comment?: string | null;
}

export interface DatabaseCenterRelationSummary {
  id: string;
  fromTable: string;
  fromColumn: string;
  toTable: string;
  toColumn: string;
  constraintName?: string | null;
}

export interface DatabaseCenterSchemaStructure {
  sourceId: string;
  schemaName: string;
  environment: DatabaseCenterEnvironment;
  objects: DatabaseCenterObjectSummary[];
  columnsByObject: Record<string, DatabaseCenterColumnSummary[]>;
  relations: DatabaseCenterRelationSummary[];
}

export interface DatabaseCenterSqlRequest {
  sourceId: string;
  sql: string;
  schema?: string;
  environment?: DatabaseCenterEnvironment;
  limit?: number;
}

export interface DatabaseCenterSqlResult {
  columns: Array<{ name: string; dataType?: string | null }>;
  rows: Record<string, unknown>[];
  affectedRows?: number | null;
  elapsedMs?: number | null;
  truncated?: boolean;
}

export interface DatabaseCenterConnectionTestResult {
  success: boolean;
  message?: string | null;
  testedAt?: string | null;
  traceId?: string | null;
}

export interface DatabaseCenterConnectionLog {
  id: string;
  sourceId: string;
  success: boolean;
  message?: string | null;
  createdAt?: string | null;
}

export interface DatabaseCenterCreateDatabaseRequest {
  name: string;
  description?: string | null;
  workspaceId?: string;
  driverCode: string;
  hostProfileId?: string | null;
  provisionMode: DatabaseCenterProvisionMode;
  physicalDatabaseName?: string | null;
  defaultSchemaName?: string | null;
  environmentMode?: "DraftOnly" | "DraftAndOnline" | string;
}

function buildError(message?: string | null, traceId?: string | null): Error {
  const normalized = String(message ?? "").trim() || "Database center API request failed.";
  return new Error(traceId ? `${normalized} (traceId: ${traceId})` : normalized);
}

function unwrap<T>(response: ApiResponse<T>, fallbackMessage: string): T {
  if (!response.success || response.data == null) {
    throw buildError(response.message || fallbackMessage, response.traceId);
  }

  return response.data;
}

export async function listDatabaseCenterSources(query: DatabaseCenterSourceQuery = {}): Promise<PagedResult<DatabaseCenterSourceSummary>> {
  const response = await requestApi<ApiResponse<PagedResult<DatabaseCenterSourceSummary>>>(`/database-center/sources?${toQuery({
    pageIndex: query.pageIndex ?? 1,
    pageSize: query.pageSize ?? 20,
    keyword: query.keyword,
    sortBy: query.sortBy,
    sortDesc: query.sortDesc
  }, {
    workspaceId: query.workspaceId,
    driverCode: query.driverCode,
    sourceKind: query.sourceKind
  })}`);
  const result = unwrap(response, "获取数据库资源失败");
  return { ...result, items: result.items.map(normalizeSource) };
}

export async function getDatabaseCenterSource(id: string): Promise<DatabaseCenterSourceDetail> {
  const response = await requestApi<ApiResponse<Record<string, unknown>>>(`/database-center/sources/${encodeURIComponent(id)}/instance-summary`);
  return normalizeSource(unwrap(response, "获取数据库资源详情失败")) as DatabaseCenterSourceDetail;
}

export async function testDatabaseCenterSource(id: string): Promise<DatabaseCenterConnectionTestResult> {
  const response = await requestApi<ApiResponse<DatabaseCenterConnectionTestResult>>(
    `/database-center/sources/${encodeURIComponent(id)}/test`,
    { method: "POST" }
  );
  return unwrap(response, "测试数据源连接失败");
}

export async function listDatabaseCenterConnectionLogs(id: string): Promise<DatabaseCenterConnectionLog[]> {
  const response = await requestApi<ApiResponse<DatabaseCenterConnectionLog[]>>(
    `/database-center/sources/${encodeURIComponent(id)}/connection-logs`
  );
  return unwrap(response, "获取连接日志失败");
}

export async function listDatabaseCenterSchemas(sourceId: string, environment: DatabaseCenterEnvironment = "Draft"): Promise<DatabaseCenterSchemaSummary[]> {
  const query = new URLSearchParams({ environment }).toString();
  const response = await requestApi<ApiResponse<Array<Record<string, unknown>>>>(
    `/database-center/sources/${encodeURIComponent(sourceId)}/schemas?${query}`
  );
  return unwrap(response, "获取 Schema 列表失败").map(item => {
    const groups = Array.isArray(item.groups) ? item.groups as Array<Record<string, unknown>> : [];
    const countOf = (type: string) => Number(groups.find(group => group.type === type)?.count ?? 0);
    return {
      name: String(item.name ?? ""),
      environment,
      tableCount: countOf("table"),
      viewCount: countOf("view"),
      procedureCount: countOf("procedure"),
      triggerCount: countOf("trigger"),
      defaultSchema: !item.isSystem
    };
  });
}

export async function getDatabaseCenterSchemaStructure(
  sourceId: string,
  schemaName: string,
  environment: DatabaseCenterEnvironment = "Draft"
): Promise<DatabaseCenterSchemaStructure> {
  const query = new URLSearchParams({ environment }).toString();
  const response = await requestApi<ApiResponse<DatabaseCenterSchemaStructure>>(
    `/database-center/sources/${encodeURIComponent(sourceId)}/schemas/${encodeURIComponent(schemaName)}/structure?${query}`
  );
  const raw = unwrap(response, "获取 Schema 结构失败") as DatabaseCenterSchemaStructure;
  return {
    ...raw,
    objects: (raw.objects ?? []).map(item => ({
      ...item,
      id: item.id ?? `${item.schema ?? schemaName}:${item.objectType}:${item.name}`
    }))
  };
}

export async function executeDatabaseCenterSql(request: DatabaseCenterSqlRequest): Promise<DatabaseCenterSqlResult> {
  const response = await requestApi<ApiResponse<DatabaseCenterSqlResult>>("/database-center/sql/execute", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  return unwrap(response, "执行 SQL 失败");
}

export async function previewDatabaseCenterSql(request: DatabaseCenterSqlRequest): Promise<DatabaseCenterSqlResult> {
  const response = await requestApi<ApiResponse<DatabaseCenterSqlResult>>("/database-center/sql/preview", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  return unwrap(response, "预览 SQL 失败");
}

export async function createDatabaseCenterDatabase(request: DatabaseCenterCreateDatabaseRequest): Promise<string> {
  const provisionMode = toProvisionMode(request.driverCode, request.provisionMode);
  const environmentMode = request.environmentMode === "DraftOnly" ? 1 : 0;
  const response = await requestApi<ApiResponse<{ id?: string; Id?: string; draftSourceId?: string; DraftSourceId?: string }>>("/ai-databases", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      name: request.name,
      description: request.description,
      workspaceId: request.workspaceId,
      driverCode: request.driverCode,
      hostProfileId: request.hostProfileId,
      provisionMode,
      environmentMode,
      schemaName: request.defaultSchemaName,
      physicalDatabaseName: request.physicalDatabaseName,
      fields: [
        {
          name: "name",
          description: "Database center bootstrap field",
          type: "string",
          required: false,
          indexed: false,
          isSystemField: false,
          sortOrder: 0
        }
      ]
    })
  });
  const id = String(response.data?.draftSourceId ?? response.data?.DraftSourceId ?? extractResourceId(response.data) ?? "");
  if (!response.success || !id) {
    throw buildError(response.message || "创建数据库失败", response.traceId);
  }

  return id;
}

function toProvisionMode(driverCode: string, provisionMode?: string | null): number {
  const explicit = String(provisionMode ?? "").toLowerCase();
  if (explicit === "existingdatabase" || explicit === "existing") return 4;

  const normalized = driverCode.toLowerCase();
  if (normalized === "sqlite") return 0;
  if (normalized === "mysql") return 1;
  if (normalized === "postgresql" || normalized === "postgres") return 2;
  return 4;
}

function normalizeSource(source: Record<string, unknown>): DatabaseCenterSourceSummary {
  const id = String(source.id ?? source.sourceId ?? "");
    return {
      id,
      aiDatabaseId: (source.aiDatabaseId ?? source.databaseId) as string | null | undefined,
      name: String(source.name ?? ""),
    sourceKind: String(source.sourceKind ?? "AiDatabase"),
    driverCode: String(source.driverCode ?? ""),
    environment: normalizeEnvironment(source.environment),
    status: (source.status ?? source.provisionState) as string | null | undefined,
    readOnly: source.readOnly as boolean | null | undefined,
    address: source.address as string | null | undefined,
    workspaceId: source.workspaceId as string | null | undefined,
    description: source.description as string | null | undefined,
    hostProfileId: source.hostProfileId as string | null | undefined,
    hostProfileName: source.hostProfileName as string | null | undefined,
    physicalDatabaseName: (source.physicalDatabaseName ?? source.databaseName) as string | null | undefined,
    defaultSchemaName: (source.defaultSchemaName ?? source.schemaName) as string | null | undefined,
    maskedConnectionSummary: source.maskedConnectionSummary as string | null | undefined,
    provisionState: (source.provisionState ?? source.status) as string | null | undefined,
    updatedAt: source.updatedAt as string | null | undefined,
    createdAt: source.createdAt as string | null | undefined
  };
}

function normalizeEnvironment(value: unknown): DatabaseCenterEnvironment {
  if (value === 2 || value === "2" || String(value).toLowerCase() === "online") {
    return "Online";
  }

  return "Draft";
}
