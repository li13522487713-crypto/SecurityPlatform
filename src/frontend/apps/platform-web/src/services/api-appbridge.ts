import type {
  AppBridgeCommandCreateRequest,
  AppCommandDetail,
  AppCommandListItem,
  AppExposurePolicy,
  ExposedDataQueryResult,
  OnlineAppProjectionDetail,
  OnlineAppProjectionItem
} from "@atlas/appbridge-console";
import type { PagedRequest, PagedResult } from "@atlas/shared-core";
import { createAppBridgeConsoleApi } from "@atlas/appbridge-console";
import { requestApi } from "@/services/api-core";

const appBridgeApi = createAppBridgeConsoleApi(requestApi);

export async function getOnlineApps(
  request: PagedRequest
): Promise<PagedResult<OnlineAppProjectionItem>> {
  return appBridgeApi.getOnlineApps(request);
}

export async function getOnlineAppDetail(appInstanceId: string): Promise<OnlineAppProjectionDetail> {
  return appBridgeApi.getOnlineAppDetail(appInstanceId);
}

export async function getExposurePolicy(appInstanceId: string): Promise<AppExposurePolicy> {
  return appBridgeApi.getExposurePolicy(appInstanceId);
}

export async function updateExposurePolicy(
  appInstanceId: string,
  payload: Pick<AppExposurePolicy, "exposedDataSets" | "allowedCommands" | "maskPolicies">
): Promise<AppExposurePolicy> {
  return appBridgeApi.updateExposurePolicy(appInstanceId, payload);
}

export async function createAppBridgeCommand(payload: AppBridgeCommandCreateRequest): Promise<string> {
  return appBridgeApi.createCommand(payload);
}

export async function getAppBridgeCommands(
  request: PagedRequest,
  appInstanceId?: string
): Promise<PagedResult<AppCommandListItem>> {
  return appBridgeApi.getCommands(request, appInstanceId);
}

export async function getAppBridgeCommandDetail(commandId: string): Promise<AppCommandDetail> {
  return appBridgeApi.getCommandDetail(commandId);
}

export async function queryExposedData(
  appInstanceId: string,
  dataSet: string,
  paged: PagedRequest
): Promise<ExposedDataQueryResult> {
  return appBridgeApi.queryExposedData(appInstanceId, dataSet, paged);
}
