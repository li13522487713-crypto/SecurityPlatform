import { mockResolve } from "./mock-utils";

/**
 * Mock：个人设置（PRD 03-项目开发底部头像入口）。
 *
 * 路由：
 *   GET    /api/v1/me/settings/general
 *   PATCH  /api/v1/me/settings/general
 *   GET    /api/v1/me/settings/publish-channels
 *   GET    /api/v1/me/settings/datasources
 *   DELETE /api/v1/me/account
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

let GENERAL_SETTINGS: MeGeneralSettings = {
  locale: "zh-CN",
  theme: "light"
};

export async function getMeGeneralSettings(): Promise<MeGeneralSettings> {
  return mockResolve({ ...GENERAL_SETTINGS });
}

export async function updateMeGeneralSettings(patch: Partial<MeGeneralSettings>): Promise<MeGeneralSettings> {
  GENERAL_SETTINGS = {
    ...GENERAL_SETTINGS,
    ...patch
  };
  return mockResolve({ ...GENERAL_SETTINGS });
}

export async function listMePublishChannels(): Promise<MePublishChannelItem[]> {
  return mockResolve<MePublishChannelItem[]>([
    { id: "ch-wechat-personal", name: "微信个人", type: "wechat-personal", bound: false },
    { id: "ch-feishu-personal", name: "飞书个人", type: "feishu-personal", bound: false }
  ]);
}

export async function listMeDataSources(): Promise<MeDataSourceItem[]> {
  return mockResolve<MeDataSourceItem[]>([
    { id: "ds-qdrant", name: "默认 Qdrant", type: "qdrant", bound: true },
    { id: "ds-minio", name: "默认 MinIO", type: "minio", bound: true }
  ]);
}

export async function deleteMeAccount(): Promise<void> {
  return mockResolve(undefined);
}
