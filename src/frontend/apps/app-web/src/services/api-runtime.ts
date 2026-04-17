import type { ApiResponse } from "@atlas/shared-react-core";
import type { RuntimeMenuResponse } from "../types/api";
import type { RuntimePageSchema } from "../types/runtime-page-schema";
import type { RuntimeManifest, RuntimeExecution, RuntimeAuditEvent } from "../runtime/release/runtime-release-types";
import { isDirectRuntimeMode, requestApi, resolveAppHostPrefix } from "./api-core";
export { requestApi } from "./api-core";

function resolveAppKeyFromPath(): string | null {
  if (typeof window === "undefined") {
    return null;
  }

  const appRouteMatch = window.location.pathname.match(/^\/apps\/([^/]+)/);
  return appRouteMatch?.[1] ? decodeURIComponent(appRouteMatch[1]) : null;
}

function resolveRuntimeAppKey(inputAppKey?: string): string | null {
  const normalized = inputAppKey?.trim();
  return normalized && normalized.length > 0 ? normalized : resolveAppKeyFromPath();
}

export async function getRuntimeMenu(appKey: string): Promise<RuntimeMenuResponse> {
  const requestPath = `${resolveAppHostPrefix(appKey)}/api/v1/runtime/apps/${encodeURIComponent(appKey)}/menu`;
  const response = await requestApi<ApiResponse<RuntimeMenuResponse>>(
    requestPath
  );
  return response.data ?? { appKey, items: [] };
}

export function buildRuntimeRecordsUrl(pageKey: string, appKey?: string): string {
  const encodedPageKey = encodeURIComponent(pageKey);
  const runtimeAppKey = resolveRuntimeAppKey(appKey);
  if (isDirectRuntimeMode() || !runtimeAppKey) {
    return `/api/app/runtime/pages/${encodedPageKey}/records`;
  }

  return `${resolveAppHostPrefix(runtimeAppKey)}/api/app/runtime/pages/${encodedPageKey}/records`;
}

export async function getRuntimePageSchema(pageKey: string, appKey?: string): Promise<RuntimePageSchema> {
  const encodedPageKey = encodeURIComponent(pageKey);
  const runtimeAppKey = resolveRuntimeAppKey(appKey);
  const requestPath = isDirectRuntimeMode() || !runtimeAppKey
    ? `/api/app/runtime/pages/${encodedPageKey}/schema`
    : `${resolveAppHostPrefix(runtimeAppKey)}/api/app/runtime/pages/${encodedPageKey}/schema`;

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
  const prefix = isDirectRuntimeMode() ? "" : resolveAppHostPrefix(appKey);
  try {
    const resp = await requestApi<ApiResponse<RuntimeExecution>>(
      `${prefix}/api/app/runtime/executions`,
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

  const prefix = isDirectRuntimeMode() ? "" : resolveAppHostPrefix(appKey);
  try {
    await requestApi<ApiResponse<unknown>>(
      `${prefix}/api/app/runtime/audit/events`,
      {
        method: "POST",
        body: JSON.stringify({ events }),
      },
    );
  } catch {
    // 审计上报失败不阻断业务
  }
}
