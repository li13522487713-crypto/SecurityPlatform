import type { PagedRequest, PagedResult } from "@atlas/shared-core";
import type { JsonObject, ProtocolManifest } from "@atlas/schema-protocol";

export interface OnlineAppProjectionItem {
  appInstanceId: string;
  appKey: string;
  appName: string;
  bridgeMode: string;
  runtimeStatus: string;
  healthStatus: string;
  releaseVersion?: string;
  lastSeenAt: string;
}

export interface OnlineAppProjectionDetail extends OnlineAppProjectionItem {
  bridgeEndpoint?: string;
  supportedCommands: string[];
}

export interface AppExposurePolicy {
  appInstanceId: string;
  exposedDataSets: string[];
  allowedCommands: string[];
  maskPolicies: Record<string, string[]>;
  updatedAt: string;
}

export interface AppBridgeCommandCreateRequest {
  appInstanceId: string;
  commandType: string;
  payloadJson: string;
  dryRun: boolean;
  reason?: string;
  protocol?: ProtocolManifest;
}

export interface AppCommandListItem {
  commandId: string;
  appInstanceId: string;
  appKey?: string;
  commandType: string;
  dryRun: boolean;
  riskLevel: string;
  status: string;
  initiator: string;
  createdAt: string;
  updatedAt: string;
}

export interface AppCommandDetail extends AppCommandListItem {
  payloadJson: string;
  reason: string;
  approvalRequestId?: string;
  message: string;
  resultJson: string;
  startedAt?: string;
  completedAt?: string;
}

export interface ExposedDataQueryResult {
  dataSet: string;
  result: PagedResult<JsonObject>;
}

export type AppBridgePagedRequest = PagedRequest;
