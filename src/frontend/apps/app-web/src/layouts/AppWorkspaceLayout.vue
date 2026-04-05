<template>
  <a-layout class="app-workspace-layout">
    <a-layout-sider
      :width="240"
      theme="light"
      collapsible
      v-model:collapsed="collapsed"
      class="workspace-sider"
    >
      <div class="sider-logo">
        <h3 v-if="!collapsed" class="app-title">{{ appKey }}</h3>
        <span v-else class="app-title-collapsed">{{ appKey.charAt(0).toUpperCase() }}</span>
      </div>
      <a-menu
        mode="inline"
        :selectedKeys="selectedKeys"
        :openKeys="openKeys"
        @click="handleMenuClick"
        @openChange="handleOpenChange"
      >
        <a-menu-item key="dashboard">
          <template #icon><DashboardOutlined /></template>
          {{ t("workspace.menuDashboard") }}
        </a-menu-item>

        <a-sub-menu v-if="runtimeMenuItems.length > 0" key="runtime">
          <template #icon><AppstoreOutlined /></template>
          <template #title>{{ t("workspace.menuRuntime") }}</template>
          <a-menu-item
            v-for="item in runtimeMenuItems"
            :key="`runtime:${item.pageKey}`"
          >
            {{ item.title }}
          </a-menu-item>
        </a-sub-menu>

        <a-menu-item v-if="hasPermission(APP_PERMISSIONS.APP_MEMBERS_VIEW)" key="org">
          <template #icon><TeamOutlined /></template>
          {{ t("workspace.menuOrg") }}
        </a-menu-item>

        <a-menu-item key="approval">
          <template #icon><AuditOutlined /></template>
          {{ t("workspace.menuApproval") }}
        </a-menu-item>

        <a-sub-menu key="ai-group">
          <template #icon><RobotOutlined /></template>
          <template #title>{{ t("workspace.menuAI") }}</template>
          <a-menu-item key="ai-chat">
            {{ t("workspace.menuAIChat") }}
          </a-menu-item>
          <a-menu-item key="ai-assistant">
            {{ t("workspace.menuAIAssistant") }}
          </a-menu-item>
        </a-sub-menu>

        <a-menu-item key="reports">
          <template #icon><BarChartOutlined /></template>
          {{ t("workspace.menuReports") }}
        </a-menu-item>

        <a-menu-item key="dashboards-mgmt">
          <template #icon><FundOutlined /></template>
          {{ t("workspace.menuDashboards") }}
        </a-menu-item>

        <a-menu-item key="visualization">
          <template #icon><MonitorOutlined /></template>
          {{ t("workspace.menuVisualization") }}
        </a-menu-item>

        <a-menu-item v-if="hasPermission(APP_PERMISSIONS.APPS_UPDATE)" key="settings">
          <template #icon><SettingOutlined /></template>
          {{ t("workspace.menuSettings") }}
        </a-menu-item>
      </a-menu>
    </a-layout-sider>

    <a-layout>
      <a-layout-header class="workspace-header">
        <div class="header-left">
          <span class="header-title">{{ t("workspace.title") }}</span>
        </div>
        <div class="header-right">
          <a-button type="link" size="small" @click="openPlatform">
            {{ t("layout.backToPlatform") }}
          </a-button>
          <a-badge :count="unreadCount" :offset="[-4, 4]" size="small">
            <a-button type="text" size="small" @click="notificationVisible = true">
              <BellOutlined />
            </a-button>
          </a-badge>
          <a-dropdown>
            <a-button type="text" size="small">
              <UserOutlined />
              <span class="user-label">{{ displayName }}</span>
            </a-button>
            <template #overlay>
              <a-menu @click="handleUserMenuClick">
                <a-menu-item key="profile">
                  <UserOutlined />
                  {{ t("profile.title") }}
                </a-menu-item>
                <a-menu-item key="changePassword">
                  <LockOutlined />
                  {{ t("profile.changePassword") }}
                </a-menu-item>
                <a-menu-divider />
                <a-menu-item key="logout">
                  <LogoutOutlined />
                  {{ t("auth.logout") }}
                </a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
        </div>
      </a-layout-header>
      <a-layout-content class="workspace-content">
        <router-view />
      </a-layout-content>
    </a-layout>
  </a-layout>

  <!-- Profile Drawer -->
  <a-drawer
    v-model:open="profileVisible"
    :title="t('profile.title')"
    :width="400"
    destroy-on-close
  >
    <a-descriptions :column="1" bordered size="small">
      <a-descriptions-item :label="t('profile.username')">
        {{ userStore.profile?.username ?? '—' }}
      </a-descriptions-item>
      <a-descriptions-item :label="t('profile.displayName')">
        {{ userStore.profile?.displayName ?? '—' }}
      </a-descriptions-item>
      <a-descriptions-item :label="t('profile.roles')">
        <a-tag v-for="role in userStore.roles" :key="role" color="blue">{{ role }}</a-tag>
        <span v-if="userStore.roles.length === 0">—</span>
      </a-descriptions-item>
      <a-descriptions-item :label="t('profile.tenant')">
        {{ userStore.profile?.tenantId ?? '—' }}
      </a-descriptions-item>
    </a-descriptions>
  </a-drawer>

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
                  <BellOutlined style="font-size: 18px; color: #1677ff" />
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
import {
  DashboardOutlined,
  AppstoreOutlined,
  TeamOutlined,
  AuditOutlined,
  RobotOutlined,
  BarChartOutlined,
  FundOutlined,
  MonitorOutlined,
  SettingOutlined,
  UserOutlined,
  LogoutOutlined,
  LockOutlined,
  BellOutlined
} from "@ant-design/icons-vue";
import { changePassword } from "@/services/api-profile";
import {
  getUnreadCount,
  getNotifications,
  markAllAsRead
} from "@/services/api-notifications";
import type { UserNotificationItem } from "@/services/api-notifications";
import { useAppUserStore } from "@/stores/user";
import { usePermission } from "@/composables/usePermission";
import { APP_PERMISSIONS } from "@/constants/permissions";
import { getRuntimeMenu } from "@/services/api-runtime";
import type { RuntimeMenuItem } from "@/types/api";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const userStore = useAppUserStore();

const { hasPermission } = usePermission();

const collapsed = ref(false);
const runtimeMenuItems = ref<RuntimeMenuItem[]>([]);
const appKey = computed(() => String(route.params.appKey ?? ""));
const displayName = computed(() => userStore.name || t("workspace.user"));

const menuKeyToRoute: Record<string, string> = {
  dashboard: "dashboard",
  org: "org",
  approval: "approval",
  "ai-chat": "ai/chat",
  "ai-assistant": "ai/assistant",
  reports: "reports",
  "dashboards-mgmt": "dashboards",
  visualization: "visualization",
  settings: "settings"
};

const selectedKeys = computed(() => {
  const path = route.path;
  const base = `/apps/${appKey.value}`;

  for (const item of runtimeMenuItems.value) {
    if (path === `${base}/r/${item.pageKey}`) {
      return [`runtime:${item.pageKey}`];
    }
  }

  if (path.startsWith(`${base}/ai/assistant`)) return ["ai-assistant"];
  if (path.startsWith(`${base}/ai/chat`)) return ["ai-chat"];
  if (path.startsWith(`${base}/approval`)) return ["approval"];
  if (path.startsWith(`${base}/reports`)) return ["reports"];
  if (path.startsWith(`${base}/dashboards`)) return ["dashboards-mgmt"];
  if (path.startsWith(`${base}/visualization`)) return ["visualization"];
  if (path.startsWith(`${base}/settings`)) return ["settings"];
  if (path.startsWith(`${base}/org`)) return ["org"];
  if (path.startsWith(`${base}/r/`)) return [];
  if (path === base || path === `${base}/` || path.startsWith(`${base}/dashboard`)) return ["dashboard"];
  return [];
});

const openKeys = ref<string[]>([]);

function handleOpenChange(keys: string[]) {
  openKeys.value = keys;
}

function handleMenuClick(info: { key: string }) {
  const key = String(info.key);
  const encodedAppKey = encodeURIComponent(appKey.value);

  if (key.startsWith("runtime:")) {
    const pageKey = key.slice("runtime:".length);
    void router.push(`/apps/${encodedAppKey}/r/${encodeURIComponent(pageKey)}`);
    return;
  }

  const routeSuffix = menuKeyToRoute[key];
  if (routeSuffix) {
    void router.push(`/apps/${encodedAppKey}/${routeSuffix}`);
  }
}

function resolvePlatformWebOrigin(): string {
  const configured = String(import.meta.env.VITE_PLATFORM_WEB_ORIGIN ?? "").trim();
  if (configured) return configured;
  if (typeof window === "undefined") return "http://localhost:5180";
  const current = new URL(window.location.origin);
  if (current.port === "5181") {
    current.port = "5180";
  }
  return current.origin;
}

function openPlatform() {
  const url = `${resolvePlatformWebOrigin()}/console/tenant-applications?appKey=${encodeURIComponent(appKey.value)}`;
  window.open(url, "_blank", "noopener,noreferrer");
}

const profileVisible = ref(false);
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
    profileVisible.value = true;
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
    await changePassword(changePwdForm.currentPassword, changePwdForm.newPassword);
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
    if (menu.items.length > 0) {
      openKeys.value = ["runtime"];
    }
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
  min-height: 100vh;
}

.workspace-sider {
  border-right: 1px solid #f0f0f0;
}

.sider-logo {
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-bottom: 1px solid #f0f0f0;
  padding: 0 16px;
  overflow: hidden;
}

.app-title {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  color: #1677ff;
}

.app-title-collapsed {
  font-size: 18px;
  font-weight: 700;
  color: #1677ff;
}

.workspace-header {
  background: #fff;
  padding: 0 20px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid #f0f0f0;
  height: 48px;
  line-height: 48px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.header-title {
  font-size: 14px;
  font-weight: 500;
  color: rgba(0, 0, 0, 0.85);
}

.header-right {
  display: flex;
  align-items: center;
  gap: 4px;
}

.user-label {
  margin-left: 4px;
}

.workspace-content {
  padding: 16px;
  min-height: 280px;
  background: #f5f5f5;
}

.notification-time {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
  white-space: nowrap;
}
</style>
