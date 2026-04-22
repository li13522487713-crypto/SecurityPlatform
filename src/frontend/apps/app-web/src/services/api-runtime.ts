import type { ApiResponse } from "@atlas/shared-react-core";
import type { RuntimeMenuResponse } from "../types/api";
import type { RuntimePageSchema } from "../types/runtime-page-schema";
import type { RuntimeManifest, RuntimeExecution, RuntimeAuditEvent } from "../runtime/release/runtime-release-types";
import { requestApi } from "./api-core";
export { requestApi } from "./api-core";

export async function getRuntimeMenu(appKey: string): Promise<RuntimeMenuResponse> {
  const requestPath = `/api/v1/runtime/apps/${encodeURIComponent(appKey)}/menu`;
  const response = await requestApi<ApiResponse<RuntimeMenuResponse>>(
    requestPath
  );
  return response.data ?? { appKey, items: [] };
}

export function buildRuntimeRecordsUrl(pageKey: string): string {
  const encodedPageKey = encodeURIComponent(pageKey);
  return `/api/app/runtime/pages/${encodedPageKey}/records`;
}

export async function getRuntimePageSchema(pageKey: string): Promise<RuntimePageSchema> {
  const encodedPageKey = encodeURIComponent(pageKey);
  const requestPath = `/api/app/runtime/pages/${encodedPageKey}/schema`;

  const response = await requestApi<ApiResponse<RuntimePageSchema>>(
    requestPath
  );
  if (!response.data) {
    throw new Error(response.message || "Failed to load runtime page");
  }
  return response.data;
}

/**
 * 统一运行时 API 网关接口。
 *
 * 将 manifest 拉取、execution 管理、审计上报集中于此，
 * 供 bootstrap / host / audit-reporter 调用。
 */
export interface RuntimeGateway {
  getRuntimeManifest(appKey: string, pageKey: string): Promise<RuntimeManifest>;
  createRuntimeExecution(input: {
    appKey: string;
    pageKey: string;
    releaseId?: string;
  }): Promise<RuntimeExecution>;
  reportRuntimeEvent(event: RuntimeAuditEvent): Promise<void>;
}

export async function createRuntimeExecution(
  appKey: string,
  input: {
    pageKey: string;
    releaseId?: string;
    releaseVersion?: number;
  },
): Promise<RuntimeExecution | null> {
  try {
    const resp = await requestApi<ApiResponse<RuntimeExecution>>(
      `/api/app/runtime/executions`,
      {
        method: "POST",
        body: JSON.stringify({
          appKey,
          pageKey: input.pageKey,
          releaseId: input.releaseId,
          releaseVersion: input.releaseVersion,
        }),
      },
    );
    return resp.data ?? null;
  } catch {
    return null;
  }
}

export async function reportRuntimeEvents(
  appKey: string,
  events: RuntimeAuditEvent[],
): Promise<void> {
  if (events.length === 0) return;

  try {
    await requestApi<ApiResponse<unknown>>(
      `/api/app/runtime/audit/events`,
      {
        method: "POST",
        body: JSON.stringify({ events }),
      },
    );
  } catch {
    // 审计上报失败不阻断业务
  }
}
