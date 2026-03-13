<template>
  <a-layout class="workspace-layout">
    <a-layout-sider theme="light" :width="220" class="workspace-sider">
      <div class="sider-title">
        <div class="app-name" :title="appName">{{ appName }}</div>
        <a-button type="link" size="small" @click="goConsole">返回控制台</a-button>
      </div>
      <a-menu mode="inline" :selected-keys="selectedKeys" @click="onMenuClick">
        <a-menu-item :key="dashboardPath">应用仪表盘</a-menu-item>
        <a-menu-item :key="builderPath">页面设计器</a-menu-item>
        <a-menu-item :key="runtimeHomePath">运行态入口</a-menu-item>
        <a-menu-item :key="settingsPath">应用设置</a-menu-item>
      </a-menu>
    </a-layout-sider>

    <a-layout>
      <a-layout-header class="workspace-header">
        <div class="header-left">
          <span>应用工作台</span>
          <a-tag color="blue">AppId: {{ appId }}</a-tag>
        </div>
        <div class="header-right">
          <NotificationBell />
          <a-dropdown trigger="click">
            <a-button type="text">
              <a-space>
                <a-avatar size="small">{{ profileInitials }}</a-avatar>
                <span>{{ profileDisplayName }}</span>
              </a-space>
            </a-button>
            <template #overlay>
              <a-menu>
                <a-menu-item key="profile" @click="go('/profile')">个人中心</a-menu-item>
                <a-menu-divider />
                <a-menu-item key="logout" @click="logout">退出登录</a-menu-item>
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
import { computed, onMounted, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { useTagsViewStore } from "@/stores/tagsView";
import NotificationBell from "@/components/layout/NotificationBell.vue";
import { getLowCodeAppDetail } from "@/services/lowcode";

const route = useRoute();
const router = useRouter();
const userStore = useUserStore();
const permissionStore = usePermissionStore();
const tagsViewStore = useTagsViewStore();

const appName = computed(() => {
  const metaName = route.meta?.title;
  if (typeof metaName === "string" && metaName.trim()) {
    return metaName;
  }
  return "应用";
});
const appId = computed(() => String(route.params.appId ?? ""));

const dashboardPath = computed(() => `/apps/${appId.value}/dashboard`);
const builderPath = computed(() => `/apps/${appId.value}/builder`);
const runtimeHomePath = computed(() => `/apps/${appId.value}/run/home`);
const settingsPath = computed(() => `/apps/${appId.value}/settings`);

const selectedKeys = computed(() => {
  if (route.path.startsWith(builderPath.value)) {
    return [builderPath.value];
  }
  if (route.path.startsWith(`/apps/${appId.value}/run/`)) {
    return [runtimeHomePath.value];
  }
  if (route.path.startsWith(settingsPath.value)) {
    return [settingsPath.value];
  }
  return [dashboardPath.value];
});

const profileDisplayName = computed(
  () => userStore.profile?.displayName || userStore.profile?.username || "个人中心"
);
const profileInitials = computed(() => profileDisplayName.value.slice(0, 2));

function onMenuClick(info: { key: string }) {
  go(info.key);
}

function go(path: string) {
  router.push(path);
}

function goConsole() {
  router.push("/console");
}

async function logout() {
  await userStore.logout();
  permissionStore.reset();
  tagsViewStore.delAllViews();
  router.push("/login");
}

async function syncTitle() {
  if (!appId.value) {
    return;
  }

  try {
    const detail = await getLowCodeAppDetail(appId.value);
    if (detail?.name) {
      document.title = `${detail.name} - 应用工作台 - Atlas Security Platform`;
    }
  } catch {
    // ignore
  }
}

onMounted(syncTitle);
watch(appId, () => {
  syncTitle();
});
</script>

<style scoped>
.workspace-layout {
  min-height: 100vh;
}

.workspace-sider {
  border-right: 1px solid var(--color-border);
}

.sider-title {
  padding: 12px;
  border-bottom: 1px solid var(--color-border);
}

.app-name {
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 6px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.workspace-header {
  height: var(--header-height);
  line-height: var(--header-height);
  padding: 0 var(--spacing-md);
  background: var(--color-bg-container);
  border-bottom: 1px solid var(--color-border);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.workspace-content {
  margin: var(--spacing-md);
}
</style>
