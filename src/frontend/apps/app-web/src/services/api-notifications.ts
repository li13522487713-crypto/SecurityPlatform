import type { ApiResponse, PagedResult } from "@atlas/shared-react-core";
import { requestApi } from "./api-core";

export interface UserNotificationItem {
  id: string;
  title: string;
  content: string;
  type: string;
  isRead: boolean;
  createdAt: string;
}

export async function getUnreadCount(): Promise<number> {
  try {
    const response = await requestApi<ApiResponse<{ count: number }>>(
      "/notifications/unread-count"
    );
    return response.data?.count ?? 0;
  } catch {
    return 0;
  }
}

export async function getNotifications(
  pageIndex = 1,
  pageSize = 20,
  isRead?: boolean
): Promise<PagedResult<UserNotificationItem>> {
  const params = new URLSearchParams({
    PageIndex: pageIndex.toString(),
    PageSize: pageSize.toString()
  });
  if (isRead !== undefined) params.set("isRead", String(isRead));
  const response = await requestApi<ApiResponse<PagedResult<UserNotificationItem>>>(
    `/notifications/inbox?${params.toString()}`
  );
  if (!response.data) throw new Error(response.message || "Failed to fetch notifications");
  return response.data;
}

export async function markAsRead(notificationId: string): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `/notifications/${encodeURIComponent(notificationId)}/read`,
    { method: "PUT" }
  );
}

export async function markAllAsRead(): Promise<void> {
  await requestApi<ApiResponse<unknown>>("/notifications/read-all", {
    method: "PUT"
  });
}
