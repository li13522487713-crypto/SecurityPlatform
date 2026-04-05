<template>
  <div style="padding: 24px;">
    <a-card>
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
        <h3 style="margin: 0;">{{ t("notificationInbox.pageTitle") }}</h3>
        <a-button v-if="unreadCount > 0" @click="handleMarkAll">{{ t("notificationInbox.markAllRead") }}</a-button>
      </div>

      <a-tabs v-model:active-key="activeTab" @change="handleTabChange">
        <a-tab-pane key="all" :tab="t('notificationInbox.tabAll')" />
        <a-tab-pane key="unread" :tab="t('notificationInbox.tabUnread', { count: unreadCount })" />
        <a-tab-pane key="read" :tab="t('notificationInbox.tabRead')" />
      </a-tabs>

      <a-spin :spinning="loading">
        <div v-if="items.length > 0" style="display: flex; flex-direction: column; gap: 8px;">
          <a-card
            v-for="item in items"
            :key="item.userNotificationId"
            size="small"
            :style="{ background: item.isRead ? undefined : '#f0f7ff', borderColor: item.isRead ? undefined : '#91caff' }"
          >
            <div style="display: flex; align-items: flex-start; gap: 12px;">
              <div style="flex-shrink: 0; display: flex; flex-direction: column; gap: 4px;">
                <a-tag :color="tagColor(item.noticeType)">{{ typeLabel(item.noticeType) }}</a-tag>
                <a-tag v-if="item.priority === 2" color="red">{{ t("notificationInbox.urgent") }}</a-tag>
                <a-tag v-else-if="item.priority === 1" color="orange">{{ t("notificationInbox.important") }}</a-tag>
              </div>
              <div style="flex: 1; min-width: 0;">
                <div style="font-weight: 500; display: flex; align-items: center; gap: 6px;">
                  <span v-if="!item.isRead" style="width: 6px; height: 6px; border-radius: 50%; background: #1677ff; flex-shrink: 0;" />
                  {{ item.title }}
                </div>
                <div style="font-size: 13px; color: #666; margin-top: 4px;">{{ item.content }}</div>
                <div style="font-size: 12px; color: #999; margin-top: 6px;">{{ formatTime(item.publishedAt) }}</div>
              </div>
              <div style="flex-shrink: 0;">
                <a-button v-if="!item.isRead" size="small" type="link" @click="handleMarkRead(item)">
                  {{ t("notificationInbox.markRead") }}
                </a-button>
              </div>
            </div>
          </a-card>
        </div>
        <a-empty v-else :description="t('notificationInbox.empty')" />
      </a-spin>

      <div v-if="total > pageSize" style="margin-top: 20px; display: flex; justify-content: flex-end;">
        <a-pagination v-model:current="pageIndex" :page-size="pageSize" :total="total" show-quick-jumper @change="loadData" />
      </div>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { getMyNotifications, getUnreadCount, markRead, markAllRead } from "@/services/api-notifications";
import type { UserNotificationDto } from "@/services/api-notifications";

const { t, locale } = useI18n();

const loading = ref(false);
const items = ref<UserNotificationDto[]>([]);
const total = ref(0);
const pageIndex = ref(1);
const pageSize = ref(20);
const activeTab = ref<"all" | "unread" | "read">("all");
const unreadCount = ref(0);

const isReadFilter = computed<boolean | undefined>(() => {
  if (activeTab.value === "unread") return false;
  if (activeTab.value === "read") return true;
  return undefined;
});

async function loadData() {
  loading.value = true;
  try {
    const [result, count] = await Promise.all([
      getMyNotifications(pageIndex.value, pageSize.value, isReadFilter.value),
      getUnreadCount()
    ]);
    items.value = result.items;
    total.value = result.total;
    unreadCount.value = count;
  } catch {
    message.error(t("notificationInbox.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleTabChange() {
  pageIndex.value = 1;
  void loadData();
}

async function handleMarkRead(item: UserNotificationDto) {
  try {
    await markRead(item.notificationId);
    await loadData();
  } catch {
    message.error(t("notificationInbox.opFailed"));
  }
}

async function handleMarkAll() {
  try {
    await markAllRead();
    await loadData();
    message.success(t("notificationInbox.allMarked"));
  } catch {
    message.error(t("notificationInbox.opFailed"));
  }
}

function typeLabel(type: string) {
  const map: Record<string, string> = {
    Announcement: t("notificationInbox.typeAnnouncement"),
    System: t("notificationInbox.typeSystem"),
    Reminder: t("notificationInbox.typeReminder")
  };
  return map[type] ?? type;
}

function tagColor(type: string) {
  const map: Record<string, string> = { Announcement: "blue", System: "purple", Reminder: "orange" };
  return map[type] ?? "default";
}

function formatTime(iso: string) {
  try {
    const loc = locale.value === "en-US" ? "en-US" : "zh-CN";
    return new Date(iso).toLocaleString(loc);
  } catch {
    return iso;
  }
}

onMounted(loadData);
</script>
