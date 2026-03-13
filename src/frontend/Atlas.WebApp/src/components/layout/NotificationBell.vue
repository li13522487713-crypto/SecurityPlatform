<template>
  <a-dropdown trigger="click" placement="bottomRight" :arrow="false" overlay-class-name="notification-dropdown">
    <a-badge :count="unreadCount > 99 ? '99+' : unreadCount" :offset="[-4, 4]">
      <a-button type="text" class="bell-btn" @click="refresh">
        <BellOutlined :style="{ fontSize: '18px' }" />
      </a-button>
    </a-badge>

    <template #overlay>
      <div class="notif-panel">
        <div class="notif-header">
          <span class="notif-title">通知</span>
          <a-button v-if="unreadCount > 0" type="link" size="small" @click="markAll">全部已读</a-button>
        </div>

        <a-spin :spinning="loading">
          <template v-if="items.length > 0">
            <div
              v-for="item in items"
              :key="item.notificationId"
              class="notif-item"
              :class="{ unread: !item.isRead }"
              @click="handleItemClick(item)"
            >
              <a-badge :dot="!item.isRead" :offset="[-2, 4]">
                <span class="notif-type-tag" :class="typeClass(item.noticeType)">
                  {{ typeLabel(item.noticeType) }}
                </span>
              </a-badge>
              <div class="notif-content">
                <div class="notif-item-title">{{ item.title }}</div>
                <div class="notif-time">{{ formatTime(item.publishedAt) }}</div>
              </div>
            </div>
          </template>
          <a-empty v-else description="暂无通知" :image="Empty.PRESENTED_IMAGE_SIMPLE" />
        </a-spin>

        <div class="notif-footer">
          <router-link to="/system/notifications">查看全部通知</router-link>
        </div>
      </div>
    </template>
  </a-dropdown>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from "vue";
import { useRouter } from "vue-router";
import { BellOutlined } from "@ant-design/icons-vue";
import { Empty } from "ant-design-vue";
import { getUnreadCount, getMyNotifications, markRead, markAllRead } from "@/services/notification";
import type { UserNotificationDto } from "@/services/notification";

const router = useRouter();
const unreadCount = ref(0);
const items = ref<UserNotificationDto[]>([]);
const loading = ref(false);

let timer: number | undefined;

const refresh = async () => {
  loading.value = true;
  try {
    const [countResult, listResult] = await Promise.all([
      getUnreadCount(),
      getMyNotifications(1, 5)
    ]);
    unreadCount.value = countResult;
    items.value = listResult.items;
  } catch {
    // ignore silently
  } finally {
    loading.value = false;
  }
};

const handleItemClick = async (item: UserNotificationDto) => {
  if (!item.isRead) {
    try {
      await markRead(item.notificationId);
      item.isRead = true;
      unreadCount.value = Math.max(0, unreadCount.value - 1);
    } catch {
      // ignore
    }
  }
  router.push("/system/notifications");
};

const markAll = async () => {
  try {
    await markAllRead();
    items.value.forEach(i => (i.isRead = true));
    unreadCount.value = 0;
  } catch {
    // ignore
  }
};

const typeLabel = (type: string) => {
  const map: Record<string, string> = {
    Announcement: "公告",
    announcement: "公告",
    System: "系统",
    system: "系统",
    Reminder: "提醒"
  };
  return map[type] ?? type;
};

const typeClass = (type: string) => {
  const map: Record<string, string> = {
    Announcement: "type-announcement",
    announcement: "type-announcement",
    System: "type-system",
    system: "type-system",
    Reminder: "type-reminder"
  };
  return map[type] ?? "";
};

const formatTime = (iso: string) => {
  try {
    const d = new Date(iso);
    return d.toLocaleString("zh-CN", { month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit" });
  } catch {
    return iso;
  }
};

const startPolling = () => {
  timer = window.setInterval(async () => {
    try {
      unreadCount.value = await getUnreadCount();
    } catch {
      // ignore
    }
  }, 60_000); // 每分钟轮询一次
};

onMounted(() => {
  refresh();
  startPolling();
});

onUnmounted(() => {
  window.clearInterval(timer);
});
</script>

<style scoped>
.bell-btn {
  padding: 0 8px;
  color: var(--color-text-secondary);
}

.bell-btn:hover {
  color: var(--color-primary);
}

.notif-panel {
  width: 320px;
  background: var(--color-bg-elevated);
  border-radius: var(--border-radius-md);
  box-shadow: var(--shadow-md);
  overflow: hidden;
}

.notif-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  border-bottom: 1px solid var(--color-border);
}

.notif-title {
  font-weight: 600;
  font-size: 14px;
}

.notif-item {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 10px 16px;
  cursor: pointer;
  border-bottom: 1px solid var(--color-bg-subtle);
  transition: background 0.15s;
}

.notif-item:hover {
  background: var(--color-bg-hover);
}

.notif-item.unread {
  background: var(--color-primary-bg);
}

.notif-item.unread:hover {
  background: #dbeafe;
}

.notif-type-tag {
  display: inline-block;
  padding: 1px 6px;
  border-radius: 3px;
  font-size: 11px;
  flex-shrink: 0;
  margin-top: 2px;
}

.type-announcement {
  background: var(--color-primary-bg);
  color: var(--color-primary);
}

.type-system {
  background: #f9f0ff;
  color: #722ed1;
}

.type-reminder {
  background: var(--color-warning-bg);
  color: #d46b08;
}

.notif-content {
  flex: 1;
  min-width: 0;
}

.notif-item-title {
  font-size: 13px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.notif-time {
  font-size: 11px;
  color: var(--color-text-tertiary);
  margin-top: 2px;
}

.notif-footer {
  text-align: center;
  padding: 10px;
  border-top: 1px solid var(--color-border);
  font-size: 13px;
}
</style>
