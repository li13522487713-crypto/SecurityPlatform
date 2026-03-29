<template>
  <div data-testid="e2e-shell-main">
    <a-layout class="console-layout">
      <div data-testid="e2e-header">
        <a-layout-header class="console-header">
          <div class="left" data-testid="e2e-sidebar">
            <div class="brand" @click="go('/console')">{{ t("consoleLayout.brandTitle") }}</div>
            <div data-testid="e2e-sidebar-menu" class="menu-wrapper">
              <a-menu
                mode="horizontal"
                theme="light"
                :selected-keys="selectedKeys"
                style="border-bottom: none; line-height: 54px; width: 100%; border: none;"
                @click="onMenuClick"
              >
                <template v-for="item in visibleMenuItems" :key="item.key">
                  <a-sub-menu v-if="item.children" :key="item.key" :title="item.label">
                    <a-menu-item v-for="child in item.children" :key="child.key">
                      {{ child.label }}
                    </a-menu-item>
                  </a-sub-menu>
                  <a-menu-item v-else :key="item.key">
                    {{ item.label }}
                  </a-menu-item>
                </template>
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
                      <span data-testid="e2e-user-menu-profile">{{ t("layout.profile") }}</span>
                    </a-menu-item>
                    <a-menu-divider />
                    <a-menu-item key="logout" @click="logout">
                      <span data-testid="e2e-user-menu-logout">{{ t("layout.logout") }}</span>
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
          <a-empty :description="t('consoleLayout.noAccess')" />
        </div>
      </a-layout-content>
    </a-layout>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { useTagsViewStore } from "@/stores/tagsView";
import NotificationBell from "@/components/layout/NotificationBell.vue";
import UnifiedContextBar from "@/components/context/UnifiedContextBar.vue";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const userStore = useUserStore();
const permissionStore = usePermissionStore();
const tagsViewStore = useTagsViewStore();

interface RawConsoleMenuItem {
  key: string;
  labelKey: string;
  path?: string;
  permission?: string;
  children?: RawConsoleMenuItem[];
}

interface ConsoleMenuItem {
  key: string;
  label: string;
  path?: string;
  permission?: string;
  children?: ConsoleMenuItem[];
}

const menuItemsRaw: RawConsoleMenuItem[] = [
  { key: "/console", labelKey: "consoleLayout.menuConsoleHome", path: "/console", permission: "apps:view" },
  { key: "/console/apps", labelKey: "consoleLayout.menuAppManagement", path: "/console/apps", permission: "apps:view" },
  {
    key: "group-runtime",
    labelKey: "consoleLayout.groupRuntime",
    children: [
      { key: "/console/runtime-contexts", labelKey: "route.consoleRuntimeContexts", path: "/console/runtime-contexts", permission: "apps:view" },
      { key: "/console/runtime-executions", labelKey: "route.consoleRuntimeExecutions", path: "/console/runtime-executions", permission: "apps:view" },
      { key: "/console/releases", labelKey: "route.consoleReleases", path: "/console/releases", permission: "apps:view" },
      { key: "/console/debug", labelKey: "route.consoleDebugLayer", path: "/console/debug", permission: "apps:view" }
    ]
  },
  {
    key: "group-tenant",
    labelKey: "consoleLayout.groupTenant",
    children: [
      { key: "/console/tenant-applications", labelKey: "route.consoleTenantApplications", path: "/console/tenant-applications", permission: "apps:view" },
      { key: "/console/catalog", labelKey: "route.consoleCatalog", path: "/console/catalog", permission: "apps:view" },
      { key: "/console/resources", labelKey: "route.consoleResources", path: "/console/resources", permission: "apps:view" },
      { key: "/console/migration-governance", labelKey: "route.consoleMigrationGovernance", path: "/console/migration-governance", permission: "apps:view" },
      { key: "/console/app-db-migrations", labelKey: "route.consoleAppDbMigrations", path: "/console/app-db-migrations", permission: "apps:view" }
    ]
  },
  {
    key: "group-system",
    labelKey: "consoleLayout.groupSystem",
    children: [
      { key: "/settings/system/datasources", labelKey: "route.consoleDatasources", path: "/settings/system/datasources", permission: "system:admin" },
      { key: "/settings/system/configs", labelKey: "route.systemConfigs", path: "/settings/system/configs", permission: "config:view" },
      { key: "/admin/ai-config", labelKey: "route.aiAdminConfig", path: "/admin/ai-config", permission: "ai-admin-config:view" }
    ]
  }
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

function mapRawToVisible(raw: RawConsoleMenuItem): ConsoleMenuItem | null {
  if (raw.children) {
    const filtered = raw.children
      .filter((child) => hasPermission(child.permission))
      .map((child) => ({
        key: child.key,
        label: t(child.labelKey),
        path: child.path,
        permission: child.permission
      }));
    if (filtered.length === 0) {
      return null;
    }
    return {
      key: raw.key,
      label: t(raw.labelKey),
      children: filtered
    };
  }
  return hasPermission(raw.permission)
    ? { key: raw.key, label: t(raw.labelKey), path: raw.path, permission: raw.permission }
    : null;
}

const visibleMenuItems = computed(() => {
  return menuItemsRaw.map(mapRawToVisible).filter(Boolean) as ConsoleMenuItem[];
});

const flatMenuItems = computed(() => {
  const result: ConsoleMenuItem[] = [];
  menuItemsRaw.forEach((item) => {
    if (item.path) {
      result.push({
        key: item.key,
        label: t(item.labelKey),
        path: item.path,
        permission: item.permission
      });
    }
    if (item.children) {
      item.children.forEach((child) => {
        if (child.path) {
          result.push({
            key: child.key,
            label: t(child.labelKey),
            path: child.path,
            permission: child.permission
          });
        }
      });
    }
  });
  return result;
});

const selectedKeys = computed(() => {
  const sortedItems = [...flatMenuItems.value].sort((a, b) => (b.path?.length || 0) - (a.path?.length || 0));
  const matched = sortedItems.find((item) => item.path && (route.path === item.path || route.path.startsWith(`${item.path}/`)));
  if (matched && hasPermission(matched.permission)) {
    return [matched.key];
  }
  return [];
});

const profileDisplayName = computed(
  () => userStore.profile?.displayName || userStore.profile?.username || t("layout.profile")
);
const profileInitials = computed(() => profileDisplayName.value.slice(0, 2));

const canAccessCurrentRoute = computed(() => {
  const requiredPermission = typeof route.meta.requiresPermission === "string"
    ? route.meta.requiresPermission
    : undefined;
  return hasPermission(requiredPermission);
});

function onMenuClick(info: { key: string }) {
  if (!info.key.startsWith("group-")) {
    go(info.key);
  }
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
  padding: 0 16px 0 24px;
  height: 56px;
  background: var(--color-bg-container);
  border-bottom: 1px solid var(--color-border);
}

.left {
  display: flex;
  align-items: center;
  flex: 1;
  min-width: 0;
  overflow: hidden;
}

.brand {
  color: var(--color-text-primary);
  font-weight: 600;
  font-size: 16px;
  margin-right: 32px;
  cursor: pointer;
  white-space: nowrap;
  flex-shrink: 0;
}

.menu-wrapper {
  flex: 1;
  min-width: 0;
}

.right {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
  margin-left: 16px;
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
