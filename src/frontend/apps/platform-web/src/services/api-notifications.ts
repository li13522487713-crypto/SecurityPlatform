import type { ApiResponse, PagedResult } from "@atlas/shared-core";
import { requestApi } from "./api-core";

export interface UserNotificationDto {
  userNotificationId: string;
  notificationId: string;
  title: string;
  content: string;
  noticeType: string;
  priority: number;
  publishedAt: string;
  isRead: boolean;
  readAt?: string;
}

export async function getMyNotifications(
  pageIndex = 1,
  pageSize = 20,
  isRead?: boolean
): Promise<PagedResult<UserNotificationDto>> {
  const query = new URLSearchParams({ PageIndex: String(pageIndex), PageSize: String(pageSize) });
  if (typeof isRead === "boolean") query.set("isRead", String(isRead));
  const response = await requestApi<ApiResponse<PagedResult<UserNotificationDto>>>(`/notifications/inbox?${query}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getUnreadCount(): Promise<number> {
  const response = await requestApi<ApiResponse<{ count: number }>>("/notifications/unread-count");
  return response.data?.count ?? 0;
}

export async function markRead(notificationId: string): Promise<void> {
  await requestApi<ApiResponse<object>>(`/notifications/${notificationId}/read`, { method: "PUT" });
}

export async function markAllRead(): Promise<void> {
  await requestApi<ApiResponse<object>>("/notifications/read-all", { method: "PUT" });
}
