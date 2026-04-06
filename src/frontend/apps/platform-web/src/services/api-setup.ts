import type { ApiResponse } from "@atlas/shared-core";

const BASE = "/api/v1/setup";

export interface SetupStateResponse {
  status: string;
  platformSetupCompleted: boolean;
  completedAt: string | null;
  failureMessage: string | null;
}

export interface DriverFieldDefinition {
  code: string;
  label: string;
  inputType: string;
  required: boolean;
  secret: boolean;
  multiline: boolean;
  placeholder: string | null;
  defaultValue: string | null;
}

export interface DriverDefinition {
  code: string;
  displayName: string;
  supportsVisual: boolean;
  connectionStringExample: string;
  fields: DriverFieldDefinition[];
}

export interface TestConnectionRequest {
  driverCode: string;
  mode: string;
  connectionString?: string;
  visualConfig?: Record<string, string>;
}

export interface TestConnectionResponse {
  connected: boolean;
  message: string;
}

export interface InitializeRequest {
  database: {
    driverCode: string;
    mode: string;
    connectionString?: string;
    visualConfig?: Record<string, string>;
  };
  admin: {
    tenantId?: string;
    username: string;
    password: string;
  };
  roles?: {
    selectedRoleCodes?: string[];
  };
  organization?: {
    departments?: SetupDepartmentConfig[];
    positions?: SetupPositionConfig[];
  };
}

export interface SetupDepartmentConfig {
  name: string;
  code?: string;
  parentCode?: string;
  sortOrder: number;
}

export interface SetupPositionConfig {
  name: string;
  code: string;
  description?: string;
  sortOrder: number;
}

async function fetchJson<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(options?.headers ?? {})
    }
  });
  return response.json() as Promise<T>;
}

export async function getSetupState(): Promise<ApiResponse<SetupStateResponse>> {
  return fetchJson<ApiResponse<SetupStateResponse>>(BASE + "/state");
}

export async function getDrivers(): Promise<ApiResponse<DriverDefinition[]>> {
  return fetchJson<ApiResponse<DriverDefinition[]>>(BASE + "/drivers");
}

export async function testConnection(
  request: TestConnectionRequest
): Promise<ApiResponse<TestConnectionResponse>> {
  return fetchJson<ApiResponse<TestConnectionResponse>>(BASE + "/test-connection", {
    method: "POST",
    body: JSON.stringify(request)
  });
}

export interface InitializeResponse {
  status: string;
  platformSetupCompleted: boolean;
  schemaInitialized: boolean;
  tablesCreated: number;
  migrationsApplied: boolean;
  migrationCount: number;
  seedCompleted: boolean;
  seedSummary: string;
  rolesCreated: number;
  departmentsCreated: number;
  positionsCreated: number;
  adminCreated: boolean;
  adminUsername: string | null;
  effectiveAdminRoles: string[];
  adminPermissionCheckPassed: boolean;
  adminPermissionCheckMessage: string;
  errors: string[];
}

export async function initializePlatform(
  request: InitializeRequest
): Promise<ApiResponse<InitializeResponse>> {
  return fetchJson<ApiResponse<InitializeResponse>>(BASE + "/initialize", {
    method: "POST",
    body: JSON.stringify(request)
  });
}

