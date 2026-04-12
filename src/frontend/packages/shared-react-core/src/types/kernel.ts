export type JsonPrimitive = string | number | boolean | null;
export type JsonValue = JsonPrimitive | JsonObject | JsonValue[];

export interface JsonObject {
  [key: string]: JsonValue;
}

export interface ApiResponse<T> {
  success: boolean;
  code: string;
  message: string;
  traceId: string;
  data?: T;
}

export interface PagedRequest {
  pageIndex: number;
  pageSize: number;
  keyword?: string;
  sortBy?: string;
  sortDesc?: boolean;
  departmentId?: string | number;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  pageIndex: number;
  pageSize: number;
}

export enum QueryOperator {
  Equal = "eq",
  NotEqual = "ne",
  GreaterThan = "gt",
  GreaterThanOrEqual = "gte",
  LessThan = "lt",
  LessThanOrEqual = "lte",
  Like = "like",
  In = "in",
  Between = "between"
}

export interface QueryRule {
  id: string;
  field: string;
  operator: QueryOperator | string;
  value: unknown;
}

export interface QueryGroup {
  id: string;
  conjunction: "and" | "or";
  rules?: QueryRule[];
  groups?: QueryGroup[];
}

export interface AdvancedQueryConfig {
  rootGroup: QueryGroup;
}

export type ClientType = "WebH5" | "Mobile" | "Backend";
export type ClientPlatform = "Web" | "Android" | "iOS";
export type ClientChannel = "Browser" | "App";
export type ClientAgent = "Chrome" | "Edge" | "Safari" | "Firefox" | "Other";

export interface ClientContext {
  clientType: ClientType;
  clientPlatform: ClientPlatform;
  clientChannel: ClientChannel;
  clientAgent: ClientAgent;
}

export interface TenantContext {
  tenantId: string;
}

export interface AppContext {
  appId?: string;
  appKey?: string;
  appInstanceId?: string;
}

export interface UserContext {
  userId: string;
  username: string;
  displayName: string;
}

export type HostMode = "platform" | "app";

export interface CapabilityHostContext {
  hostMode: HostMode;
  tenantId?: string;
  appId?: string;
  appKey?: string;
  appInstanceId?: string;
  permissionSet?: readonly string[];
}

export interface MenuMeta {
  icon?: string;
  hidden?: boolean;
  order?: number;
}

export interface MenuNode {
  key: string;
  title: string;
  path: string;
  permissionCode?: string;
  children?: MenuNode[];
  meta?: MenuMeta;
}

export interface NavigationProjection {
  scope: "platform" | "app" | "runtime";
  items: MenuNode[];
  generatedAt: string;
}

export type PermissionScope = "platform" | "app" | "tenant";

export interface PermissionCode {
  code: string;
  scope: PermissionScope;
  description?: string;
}

export interface PermissionSet {
  roles: string[];
  permissions: string[];
  isPlatformAdmin?: boolean;
}

export type DraftStatus = "draft" | "published" | "archived";

export interface VersionInfo {
  versionId: string;
  versionNo: string;
  createdAt: string;
  createdBy?: string;
}

export interface ReleaseState {
  status: DraftStatus;
  currentVersion?: VersionInfo;
  latestPublishedVersion?: VersionInfo;
}

export type PublishMode = "full" | "incremental" | "dry-run";

export interface ConnectorSchema {
  key: string;
  name: string;
  version?: string;
  authType?: "none" | "token" | "oauth2" | "mtls";
}

export interface AppExposurePolicy {
  exposedPermissions: string[];
  exposedDataSets: string[];
  allowedCommands: string[];
  redactFields?: string[];
}

export interface AppCommand {
  commandId: string;
  commandType: string;
  idempotencyKey: string;
  payload: JsonValue;
  dryRun?: boolean;
  reason?: string;
}

export interface AppCommandResult {
  commandId: string;
  status: "pending" | "acknowledged" | "succeeded" | "failed";
  message?: string;
}

export interface ConnectorHeartbeat {
  appInstanceId: string;
  status: "online" | "offline" | "degraded";
  lastSeenAt: string;
}
