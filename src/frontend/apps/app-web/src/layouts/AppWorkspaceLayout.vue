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

        <a-menu-item key="org">
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

        <a-menu-item key="settings">
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
          <a-dropdown>
            <a-button type="text" size="small">
              <UserOutlined />
              <span class="user-label">{{ t("workspace.user") }}</span>
            </a-button>
            <template #overlay>
              <a-menu @click="handleUserMenuClick">
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
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
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
  LogoutOutlined
} from "@ant-design/icons-vue";
import { useAppUserStore } from "@/stores/user";
import { getRuntimeMenu } from "@/services/api-runtime";
import type { RuntimeMenuItem } from "@/types/api";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const userStore = useAppUserStore();

const collapsed = ref(false);
const runtimeMenuItems = ref<RuntimeMenuItem[]>([]);
const appKey = computed(() => String(route.params.appKey ?? ""));

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

function handleUserMenuClick(info: { key: string }) {
  if (info.key === "logout") {
    void handleLogout();
  }
}

async function handleLogout() {
  await userStore.logout();
  void router.push({ name: "app-login", params: { appKey: appKey.value } });
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

onMounted(() => {
  void loadRuntimeMenu();
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
</style>
