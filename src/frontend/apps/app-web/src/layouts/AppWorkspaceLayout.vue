<template>
  <div class="app-workspace-layout">
    <AppSidebar :app-key="appKey" />
    <div class="workspace-main">
      <AppHeader
        :display-name="displayName"
        :unread-count="unreadCount"
        :tenant-display="tenantDisplay"
        @notification-click="notificationVisible = true"
        @user-menu-click="handleUserMenuClick"
      />
      <main class="workspace-content">
        <router-view />
      </main>
    </div>
  </div>

  <!-- Notifications Drawer -->
  <a-drawer
    v-model:open="notificationVisible"
    :title="t('notification.title')"
    :width="420"
    destroy-on-close
  >
    <template #extra>
      <a-button type="link" size="small" @click="handleMarkAllRead">
        {{ t("notification.markAllRead") }}
      </a-button>
    </template>
    <a-spin :spinning="notificationsLoading">
      <a-list
        :data-source="notifications"
        :locale="{ emptyText: t('notification.empty') }"
      >
        <template #renderItem="{ item }">
          <a-list-item>
            <a-list-item-meta
              :title="item.title"
              :description="item.content"
            >
              <template #avatar>
                <a-badge :dot="!item.isRead" :offset="[0, 0]">
                  <BellOutlined style="font-size: 18px; color: #4f46e5" />
                </a-badge>
              </template>
            </a-list-item-meta>
            <template #extra>
              <span class="notification-time">{{ item.createdAt }}</span>
            </template>
          </a-list-item>
        </template>
      </a-list>
    </a-spin>
  </a-drawer>

  <!-- Change Password Modal -->
  <a-modal
    v-model:open="changePwdVisible"
    :title="t('profile.changePassword')"
    :confirm-loading="changePwdSubmitting"
    @ok="handleChangePassword"
  >
    <a-form layout="vertical" :model="changePwdForm">
      <a-form-item :label="t('profile.currentPassword')" required>
        <a-input-password v-model:value="changePwdForm.currentPassword" />
      </a-form-item>
      <a-form-item :label="t('profile.newPassword')" required>
        <a-input-password v-model:value="changePwdForm.newPassword" />
      </a-form-item>
      <a-form-item :label="t('profile.confirmPassword')" required>
        <a-input-password v-model:value="changePwdForm.confirmPassword" />
      </a-form-item>
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { reactive } from "vue";
import { message } from "ant-design-vue";
import { BellOutlined } from "@ant-design/icons-vue";
import AppSidebar from "@/components/layout/AppSidebar.vue";
import AppHeader from "@/components/layout/AppHeader.vue";
import { changePassword } from "@/services/api-profile";
import {
  getUnreadCount,
  getNotifications,
  markAllAsRead
} from "@/services/api-notifications";
import type { UserNotificationItem } from "@/services/api-notifications";
import { useAppUserStore } from "@/stores/user";
import { getRuntimeMenu } from "@/services/api-runtime";
import type { RuntimeMenuItem } from "@/types/api";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const userStore = useAppUserStore();

const appKey = computed(() => String(route.params.appKey ?? ""));
const displayName = computed(() => userStore.name || t("workspace.user"));
const tenantDisplay = computed(() => {
  const tid = userStore.profile?.tenantId ?? "";
  if (tid.length > 8) return tid.slice(0, 4);
  return tid || "0001";
});

const runtimeMenuItems = ref<RuntimeMenuItem[]>([]);

const changePwdVisible = ref(false);
const changePwdSubmitting = ref(false);
const changePwdForm = reactive({
  currentPassword: "",
  newPassword: "",
  confirmPassword: ""
});

function handleUserMenuClick(info: { key: string }) {
  if (info.key === "logout") {
    void handleLogout();
  } else if (info.key === "profile") {
    void router.push({ name: "app-profile", params: { appKey: appKey.value } });
  } else if (info.key === "changePassword") {
    changePwdForm.currentPassword = "";
    changePwdForm.newPassword = "";
    changePwdForm.confirmPassword = "";
    changePwdVisible.value = true;
  }
}

async function handleChangePassword() {
  if (!changePwdForm.currentPassword.trim()) {
    message.warning(t("profile.currentPasswordRequired"));
    return;
  }
  if (!changePwdForm.newPassword.trim()) {
    message.warning(t("profile.newPasswordRequired"));
    return;
  }
  if (changePwdForm.newPassword !== changePwdForm.confirmPassword) {
    message.warning(t("profile.passwordMismatch"));
    return;
  }

  changePwdSubmitting.value = true;
  try {
    await changePassword({
      currentPassword: changePwdForm.currentPassword,
      newPassword: changePwdForm.newPassword,
      confirmPassword: changePwdForm.confirmPassword
    });
    message.success(t("profile.changePasswordSuccess"));
    changePwdVisible.value = false;
    await userStore.logout();
    void router.push({ name: "app-login", params: { appKey: appKey.value } });
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("profile.changePasswordFailed"));
  } finally {
    changePwdSubmitting.value = false;
  }
}

async function handleLogout() {
  await userStore.logout();
  void router.push({ name: "app-login", params: { appKey: appKey.value } });
}

const unreadCount = ref(0);
const notificationVisible = ref(false);
const notificationsLoading = ref(false);
const notifications = ref<UserNotificationItem[]>([]);

async function loadUnreadCount() {
  unreadCount.value = await getUnreadCount();
}

async function loadNotifications() {
  notificationsLoading.value = true;
  try {
    const result = await getNotifications(1, 50);
    notifications.value = result.items ?? [];
  } catch {
    notifications.value = [];
  } finally {
    notificationsLoading.value = false;
  }
}

async function handleMarkAllRead() {
  try {
    await markAllAsRead();
    notifications.value = notifications.value.map((n) => ({ ...n, isRead: true }));
    unreadCount.value = 0;
  } catch {
    message.error(t("common.error"));
  }
}

async function loadRuntimeMenu() {
  if (!appKey.value) return;
  try {
    const menu = await getRuntimeMenu(appKey.value);
    runtimeMenuItems.value = menu.items;
  } catch {
    runtimeMenuItems.value = [];
  }
}

watch(notificationVisible, (open) => {
  if (open) void loadNotifications();
});

onMounted(() => {
  void loadRuntimeMenu();
  void loadUnreadCount();
});
</script>

<style scoped>
.app-workspace-layout {
  display: flex;
  height: 100vh;
  overflow: hidden;
  background: #f3f4f6;
}

.workspace-main {
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow: hidden;
}

.workspace-content {
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;
  padding: 16px 24px 32px;
}

.notification-time {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
  white-space: nowrap;
}
</style>
