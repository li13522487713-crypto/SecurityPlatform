import { requestApi } from "@/services/api-core";
import type { ApiResponse, PagedResult } from "@/types/api";

export interface NotificationDto {
  id: string;
  title: string;
  content: string;
  noticeType: string;
  priority: number;
  publisherId: string;
  publisherName: string;
  publishedAt: string;
  expiresAt?: string;
  isActive: boolean;
  createdAt: string;
}

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

export interface CreateNotificationRequest {
  title: string;
  content: string;
  noticeType: string;
  priority: number;
  expiresAt?: string;
}

export interface UpdateNotificationRequest {
  title: string;
  content: string;
  noticeType: string;
  priority: number;
  expiresAt?: string;
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

// 管理员接口
export async function getNotificationsManage(params: {
  pageIndex?: number;
  pageSize?: number;
  title?: string;
  noticeType?: string;
  isActive?: boolean;
}): Promise<PagedResult<NotificationDto>> {
  const query = new URLSearchParams({
    PageIndex: String(params.pageIndex ?? 1),
    PageSize: String(params.pageSize ?? 20)
  });
  if (params.title) query.set("title", params.title);
  if (params.noticeType) query.set("noticeType", params.noticeType);
  if (typeof params.isActive === "boolean") query.set("isActive", String(params.isActive));

  const response = await requestApi<ApiResponse<PagedResult<NotificationDto>>>(`/notifications/manage?${query}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function createNotification(request: CreateNotificationRequest): Promise<string> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/notifications/manage", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  return response.data?.id ?? "";
}

export async function updateNotification(id: string, request: UpdateNotificationRequest): Promise<void> {
  await requestApi<ApiResponse<object>>(`/notifications/manage/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
}

export async function deleteNotification(id: string): Promise<void> {
  await requestApi<ApiResponse<object>>(`/notifications/manage/${id}`, { method: "DELETE" });
}

export async function revokeNotification(id: string): Promise<void> {
  await requestApi<ApiResponse<object>>(`/notifications/manage/${id}/revoke`, { method: "PUT" });
}
