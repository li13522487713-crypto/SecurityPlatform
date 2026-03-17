<template>
  <div data-testid="e2e-shell-main">
    <a-layout class="console-layout">
      <div data-testid="e2e-header">
        <a-layout-header class="console-header">
          <div class="left" data-testid="e2e-sidebar">
            <div class="brand" @click="go('/console')">Atlas Console</div>
            <div data-testid="e2e-sidebar-menu">
              <a-menu
                mode="horizontal"
                theme="light"
                :selected-keys="selectedKeys"
                @click="onMenuClick"
                style="border-bottom: none; line-height: 54px;"
              >
                <a-menu-item key="/console">平台首页</a-menu-item>
                <a-menu-item key="/console/apps">应用管理</a-menu-item>
                <a-menu-item key="/console/datasources">数据源管理</a-menu-item>
                <a-menu-item key="/console/settings/system/configs">系统设置</a-menu-item>
              </a-menu>
            </div>
          </div>
          <div class="right">
            <UnifiedContextBar :show-app="false" />
            <NotificationBell />
            <a-dropdown trigger="click">
              <span data-testid="e2e-user-menu-trigger">
                <a-button type="text" class="profile-btn">
                  <a-space>
                    <a-avatar size="small">{{ profileInitials }}</a-avatar>
                    <span class="profile-name">{{ profileDisplayName }}</span>
                  </a-space>
                </a-button>
              </span>
              <template #overlay>
                <div data-testid="e2e-user-menu">
                  <a-menu>
                    <a-menu-item key="profile" @click="go('/profile')">
                      <span data-testid="e2e-user-menu-profile">个人中心</span>
                    </a-menu-item>
                    <a-menu-divider />
                    <a-menu-item key="logout" @click="logout">
                      <span data-testid="e2e-user-menu-logout">退出登录</span>
                    </a-menu-item>
                  </a-menu>
                </div>
              </template>
            </a-dropdown>
          </div>
        </a-layout-header>
      </div>

      <a-layout-content class="console-content">
        <router-view />
      </a-layout-content>
    </a-layout>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { useTagsViewStore } from "@/stores/tagsView";
import NotificationBell from "@/components/layout/NotificationBell.vue";
import UnifiedContextBar from "@/components/context/UnifiedContextBar.vue";

const route = useRoute();
const router = useRouter();
const userStore = useUserStore();
const permissionStore = usePermissionStore();
const tagsViewStore = useTagsViewStore();

const selectedKeys = computed(() => {
  const path = route.path;
  if (path.startsWith("/console/apps")) {
    return ["/console/apps"];
  }
  if (path.startsWith("/console/datasources")) {
    return ["/console/datasources"];
  }
  if (path.startsWith("/console/settings")) {
    return ["/console/settings/system/configs"];
  }
  return ["/console"];
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

async function logout() {
  await userStore.logout();
  permissionStore.reset();
  tagsViewStore.delAllViews();
  router.push("/login");
}
</script>

<style scoped>
.console-layout {
  min-height: 100vh;
}

.console-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 12px 0 20px;
  height: 56px;
  line-height: 56px;
  background: var(--color-bg-container);
  border-bottom: 1px solid var(--color-border);
}

.left {
  display: flex;
  align-items: center;
  min-width: 0;
}

.brand {
  color: var(--color-text-primary);
  font-weight: 600;
  font-size: 16px;
  margin-right: 32px;
  cursor: pointer;
  white-space: nowrap;
}

.right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.profile-btn {
  color: var(--color-text-primary);
}

.profile-name {
  color: var(--color-text-primary);
}

.console-content {
  min-height: calc(100vh - 56px);
  background: var(--color-bg-layout);
}
</style>
