import type { ApiResponse } from "@atlas/shared-core/types";
import type { WorkflowApiFactoryOptions } from "./workflow-api-factory";
import { createWorkflowV2Api } from "./workflow-api-factory";

export * from "./workflow-api-factory";

export type WorkflowRequestFn = <T>(path: string, init?: RequestInit) => Promise<T>;

export function createWorkflowApiFromRequest(
  requestFn: WorkflowRequestFn,
  options?: Omit<WorkflowApiFactoryOptions, "requestFn">
) {
  return createWorkflowV2Api({
    requestFn,
    resolveAbsoluteUrl: options?.resolveAbsoluteUrl,
    resolveAppId: options?.resolveAppId
  });
}

export async function unwrapApiData<T>(promise: Promise<ApiResponse<T>>, fallback: T): Promise<T> {
  const response = await promise;
  return response.data ?? fallback;
}
