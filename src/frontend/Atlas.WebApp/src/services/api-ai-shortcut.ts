import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@/types/api";

export interface AiShortcutCommandItem {
  id: number;
  commandKey: string;
  displayName: string;
  targetPath: string;
  description?: string;
  sortOrder: number;
  isEnabled: boolean;
}

export interface AiShortcutCommandCreateRequest {
  commandKey: string;
  displayName: string;
  targetPath: string;
  description?: string;
  sortOrder: number;
}

export interface AiShortcutCommandUpdateRequest {
  displayName: string;
  targetPath: string;
  description?: string;
  sortOrder: number;
  isEnabled: boolean;
}

export interface AiBotPopupInfoDto {
  id: number;
  popupCode: string;
  title: string;
  content: string;
  dismissed: boolean;
  createdAt: string;
  updatedAt?: string;
}

export async function getAiShortcutCommands() {
  const response = await requestApi<ApiResponse<AiShortcutCommandItem[]>>("/ai-shortcuts");
  if (!response.data) {
    throw new Error(response.message || "加载快捷命令失败");
  }

  return response.data;
}

export async function createAiShortcutCommand(request: AiShortcutCommandCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/ai-shortcuts", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "创建快捷命令失败");
  }

  return Number(response.data.id);
}

export async function updateAiShortcutCommand(id: number, request: AiShortcutCommandUpdateRequest) {
  const response = await requestApi<ApiResponse<object>>(`/ai-shortcuts/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新快捷命令失败");
  }
}

export async function deleteAiShortcutCommand(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-shortcuts/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除快捷命令失败");
  }
}

export async function getAiOnboardingPopup() {
  const response = await requestApi<ApiResponse<AiBotPopupInfoDto>>("/ai-shortcuts/popup");
  if (!response.data) {
    throw new Error(response.message || "加载引导弹窗失败");
  }

  return response.data;
}

export async function dismissAiOnboardingPopup(popupCode: string, dismissed: boolean) {
  const response = await requestApi<ApiResponse<AiBotPopupInfoDto>>("/ai-shortcuts/popup/dismiss", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ popupCode, dismissed })
  });
  if (!response.data) {
    throw new Error(response.message || "更新引导弹窗状态失败");
  }

  return response.data;
}
