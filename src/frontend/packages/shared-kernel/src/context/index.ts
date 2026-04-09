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
  permissionSet?: string[];
}
