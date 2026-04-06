import type { ApiResponse } from "@atlas/shared-core";

export interface SetupStateResponse {
  platformStatus: string;
  platformSetupCompleted: boolean;
  appStatus: string;
  appSetupCompleted: boolean;
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

export interface AppSetupDatabaseConfig {
  driverCode: string;
  mode: string;
  connectionString?: string;
  visualConfig?: Record<string, string>;
}

export interface TestConnectionResponse {
  connected: boolean;
  message: string;
}

export interface AppSetupInitializeResponse {
  platformStatus: string;
  platformSetupCompleted: boolean;
  appStatus: string;
  appSetupCompleted: boolean;
  databaseConnected: boolean;
  coreTablesVerified: boolean;
  errors: string[];
}

export interface AppSetupInitializeRequest {
  database: AppSetupDatabaseConfig;
  appName: string;
  adminUsername: string;
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
  return fetchJson<ApiResponse<SetupStateResponse>>("/api/v1/setup/state");
}

export async function getDrivers(): Promise<ApiResponse<DriverDefinition[]>> {
  return fetchJson<ApiResponse<DriverDefinition[]>>("/api/v1/setup/drivers");
}

export async function testConnection(
  database: AppSetupDatabaseConfig
): Promise<ApiResponse<TestConnectionResponse>> {
  return fetchJson<ApiResponse<TestConnectionResponse>>("/api/v1/setup/test-connection", {
    method: "POST",
    body: JSON.stringify({ database })
  });
}

export async function initializeApp(
  request: AppSetupInitializeRequest
): Promise<ApiResponse<AppSetupInitializeResponse>> {
  return fetchJson<ApiResponse<AppSetupInitializeResponse>>("/api/v1/setup/initialize", {
    method: "POST",
    body: JSON.stringify(request)
  });
}
