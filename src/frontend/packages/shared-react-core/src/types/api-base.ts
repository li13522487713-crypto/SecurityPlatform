import type { ClientContext } from "./kernel";

export type {
  JsonPrimitive,
  JsonValue,
  JsonObject,
  ApiResponse,
  ClientType,
  ClientPlatform,
  ClientChannel,
  ClientAgent,
  ClientContext,
  PagedRequest,
  PagedResult
} from "./kernel";

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
