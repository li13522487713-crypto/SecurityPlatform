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

export interface AuthProfile {
  id: string;
  username: string;
  displayName: string;
  tenantId: string;
  roles: string[];
  permissions: string[];
  isPlatformAdmin: boolean;
  clientContext?: ClientContext;
}

export interface AuthTokenResult {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  refreshExpiresAt: string;
  sessionId: number;
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
