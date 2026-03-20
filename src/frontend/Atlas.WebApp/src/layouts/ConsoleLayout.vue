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
                <a-menu-item
                  v-for="item in visibleMenuItems"
                  :key="item.path"
                >
                  {{ item.label }}
                </a-menu-item>
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
        <router-view v-if="canAccessCurrentRoute" />
        <div v-else class="no-permission-state">
          <a-empty description="暂无访问权限，请联系管理员" />
        </div>
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

interface ConsoleMenuItem {
  key: string;
  label: string;
  path: string;
  permission?: string;
}

const menuItems: ConsoleMenuItem[] = [
  { key: "console-home", label: "平台首页", path: "/console", permission: "apps:view" },
  { key: "console-apps", label: "应用管理", path: "/console/apps", permission: "apps:view" },
  { key: "console-catalog", label: "应用目录", path: "/console/catalog", permission: "apps:view" },
  { key: "console-tenant-applications", label: "租户开通", path: "/console/tenant-applications", permission: "apps:view" },
  { key: "console-runtime-contexts", label: "运行上下文", path: "/console/runtime-contexts", permission: "apps:view" },
  { key: "console-runtime-executions", label: "执行记录", path: "/console/runtime-executions", permission: "apps:view" },
  { key: "console-resources", label: "资源中心", path: "/console/resources", permission: "apps:view" },
  { key: "console-releases", label: "发布中心", path: "/console/releases", permission: "apps:view" },
  { key: "console-debug", label: "调试层", path: "/console/debug", permission: "apps:view" },
  { key: "console-datasources", label: "数据源管理", path: "/console/datasources", permission: "system:admin" },
  { key: "console-system-configs", label: "系统设置", path: "/console/settings/system/configs", permission: "config:view" }
];

function isPrivilegedUser() {
  if (userStore.profile?.isPlatformAdmin) {
    return true;
  }

  return userStore.permissions.includes("*:*:*")
    || userStore.roles.some((role) => ["admin", "superadmin"].includes(role.toLowerCase()));
}

function hasPermission(permissionCode?: string) {
  if (!permissionCode) {
    return true;
  }
  if (isPrivilegedUser()) {
    return true;
  }
  return userStore.permissions.includes(permissionCode);
}

const visibleMenuItems = computed(() => menuItems.filter((item) => hasPermission(item.permission)));

const selectedKeys = computed(() => {
  const sortedItems = [...menuItems].sort((a, b) => b.path.length - a.path.length);
  const matched = sortedItems.find((item) => route.path === item.path || route.path.startsWith(`${item.path}/`));
  if (matched && hasPermission(matched.permission)) {
    return [matched.path];
  }
  return [];
});

const profileDisplayName = computed(
  () => userStore.profile?.displayName || userStore.profile?.username || "个人中心"
);
const profileInitials = computed(() => profileDisplayName.value.slice(0, 2));
const canAccessCurrentRoute = computed(() => {
  const requiredPermission = typeof route.meta.requiresPermission === "string"
    ? route.meta.requiresPermission
    : undefined;
  return hasPermission(requiredPermission);
});

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

.no-permission-state {
  min-height: calc(100vh - 56px);
  display: flex;
  justify-content: center;
  align-items: center;
}
</style>
