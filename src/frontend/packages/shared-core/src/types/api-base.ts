import type { ClientContext } from "@atlas/shared-kernel";

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
} from "@atlas/shared-kernel";

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
