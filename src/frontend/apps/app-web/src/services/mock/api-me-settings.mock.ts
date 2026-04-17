import type { ApiResponse } from "@atlas/shared-react-core/types";
import { requestApi } from "../api-core";

/**
 * 个人设置（PRD 03 头像入口）。已切换为真实 REST：
 *   Atlas.PlatformHost/Controllers/MeSettingsController.cs
 *   Atlas.Infrastructure/Services/Coze/InMemoryMeSettingsService.cs
 *
 * - General settings 用 ConcurrentDictionary 暂存（进程内）。
 * - Publish channels / Data sources 当前返回内置常量。
 * - DELETE /me/account 仅清空当前用户偏好，不真正删除账号。
 */

export interface MeGeneralSettings {
  locale: "zh-CN" | "en-US";
  theme: "light" | "dark" | "system";
  defaultWorkspaceId?: string;
}

export interface MePublishChannelItem {
  id: string;
  name: string;
  type: "wechat-personal" | "feishu-personal" | "custom";
  bound: boolean;
}

export interface MeDataSourceItem {
  id: string;
  name: string;
  type: "qdrant" | "minio" | "obs" | "rdbms";
  bound: boolean;
}

export async function getMeGeneralSettings(): Promise<MeGeneralSettings> {
  const response = await requestApi<ApiResponse<MeGeneralSettings>>("/me/settings/general");
  if (!response.data) {
    throw new Error(response.message || "Failed to load general settings");
  }
  return response.data;
}

export async function updateMeGeneralSettings(patch: Partial<MeGeneralSettings>): Promise<MeGeneralSettings> {
  const response = await requestApi<ApiResponse<MeGeneralSettings>>("/me/settings/general", {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(patch)
  });
  if (!response.data) {
    throw new Error(response.message || "Failed to update general settings");
  }
  return response.data;
}

export async function listMePublishChannels(): Promise<MePublishChannelItem[]> {
  const response = await requestApi<ApiResponse<MePublishChannelItem[]>>("/me/settings/publish-channels");
  return response.data ?? [];
}

export async function listMeDataSources(): Promise<MeDataSourceItem[]> {
  const response = await requestApi<ApiResponse<MeDataSourceItem[]>>("/me/settings/datasources");
  return response.data ?? [];
}

export async function deleteMeAccount(): Promise<void> {
  await requestApi<ApiResponse<{ success: boolean }>>("/me/account", {
    method: "DELETE"
  });
}
