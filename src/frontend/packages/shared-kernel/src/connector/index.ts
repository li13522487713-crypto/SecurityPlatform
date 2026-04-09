import type { JsonValue } from "../contracts/index";

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
