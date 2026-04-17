import type { ApiResponse } from "@atlas/shared-react-core/types";
import type {
  WorkspaceInitRequest,
  WorkspaceSeedBundleRequest,
  WorkspaceSetupStateDto
} from "../api-setup-console";
import { mockApiResponse, mockReject, MOCK_DELAY_MS } from "./mock-utils";
import {
  getWorkspace,
  snapshotWorkspaces,
  upsertWorkspace
} from "./setup-console-store";

/**
 * 工作空间级初始化 mock。
 *
 * 防重复触发：
 * - 已 `workspace_init_completed` 的空间再次调用 `init` 会返回 200 但不重复创建资源；
 *   除非显式 `seedBundle` 升级到新 `bundleVersion`。
 */

export async function listSetupConsoleWorkspaces(): Promise<ApiResponse<WorkspaceSetupStateDto[]>> {
  return mockApiResponse<WorkspaceSetupStateDto[]>(snapshotWorkspaces());
}

export async function initializeWorkspace(
  workspaceId: string,
  request: WorkspaceInitRequest
): Promise<ApiResponse<WorkspaceSetupStateDto>> {
  if (!workspaceId) {
    return mockReject("VALIDATION_ERROR", "workspaceId is required", MOCK_DELAY_MS);
  }
  if (!request.workspaceName) {
    return mockReject("VALIDATION_ERROR", "workspaceName is required", MOCK_DELAY_MS);
  }

  const existing = getWorkspace(workspaceId);
  if (existing && existing.state === "workspace_init_completed") {
    // 幂等：已完成不重新初始化，直接返回当前状态。
    return mockApiResponse<WorkspaceSetupStateDto>(existing);
  }

  // 标记为 running -> completed（mock 同步完成，真接口异步推进）。
  upsertWorkspace({
    workspaceId,
    workspaceName: request.workspaceName,
    state: "workspace_init_running",
    seedBundleVersion: request.seedBundleVersion,
    lastUpdatedAt: new Date().toISOString()
  });

  return mockApiResponse<WorkspaceSetupStateDto>(
    upsertWorkspace({
      workspaceId,
      workspaceName: request.workspaceName,
      state: "workspace_init_completed",
      seedBundleVersion: request.seedBundleVersion,
      lastUpdatedAt: new Date().toISOString()
    })
  );
}

export async function applyWorkspaceSeedBundle(
  workspaceId: string,
  request: WorkspaceSeedBundleRequest
): Promise<ApiResponse<WorkspaceSetupStateDto>> {
  const existing = getWorkspace(workspaceId);
  if (!existing) {
    return mockReject("NOT_FOUND", "workspace not found", MOCK_DELAY_MS);
  }
  if (!request.bundleVersion) {
    return mockReject("VALIDATION_ERROR", "bundleVersion is required", MOCK_DELAY_MS);
  }

  if (existing.seedBundleVersion === request.bundleVersion && !request.forceReapply) {
    // 同 bundle 不重复执行（防重复初始化）。
    return mockApiResponse<WorkspaceSetupStateDto>(existing);
  }

  return mockApiResponse<WorkspaceSetupStateDto>(
    upsertWorkspace({
      ...existing,
      seedBundleVersion: request.bundleVersion,
      state: "workspace_init_completed",
      lastUpdatedAt: new Date().toISOString()
    })
  );
}

export async function completeWorkspaceInit(workspaceId: string): Promise<ApiResponse<WorkspaceSetupStateDto>> {
  const existing = getWorkspace(workspaceId);
  if (!existing) {
    return mockReject("NOT_FOUND", "workspace not found", MOCK_DELAY_MS);
  }
  return mockApiResponse<WorkspaceSetupStateDto>(
    upsertWorkspace({
      ...existing,
      state: "workspace_init_completed",
      lastUpdatedAt: new Date().toISOString()
    })
  );
}
