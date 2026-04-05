<template>
  <a-layout class="console-layout">
    <a-layout-header class="console-header">
      <div class="left">
        <div class="brand" @click="go('/console')">{{ t("consoleLayout.brandTitle") }}</div>
        <a-menu
          mode="horizontal"
          theme="light"
          :selected-keys="selectedKeys"
          style="border-bottom: none; line-height: 54px; border: none;"
          @click="onPrimaryMenuClick"
        >
          <a-menu-item v-for="item in primaryMenuItems" :key="item.path">
            {{ item.label }}
          </a-menu-item>
        </a-menu>
      </div>
      <div class="right">
        <a-button class="route-nav-btn" @click="routeDrawerOpen = true">{{ t("consoleLayout.routeNavigator") }}</a-button>
        <UnifiedContextBar />
        <NotificationBell />
        <a-dropdown trigger="click">
          <a-button type="text" class="profile-btn">
            <a-space>
              <a-avatar size="small">{{ profileInitials }}</a-avatar>
              <span class="profile-name">{{ profileDisplayName }}</span>
            </a-space>
          </a-button>
          <template #overlay>
            <a-menu>
              <a-menu-item key="profile" @click="go('/profile')">
                {{ t("layout.profile") }}
              </a-menu-item>
              <a-menu-divider />
              <a-menu-item key="logout" @click="handleLogout">
                {{ t("layout.logout") }}
              </a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
      </div>
    </a-layout-header>

    <a-layout-content class="console-content">
      <router-view v-if="canAccessCurrentRoute" />
      <div v-else class="no-permission-state">
        <a-empty :description="t('consoleLayout.noAccess')" />
      </div>
    </a-layout-content>
  </a-layout>

  <a-drawer
    v-model:open="routeDrawerOpen"
    :title="t('consoleLayout.routeNavigator')"
    :width="480"
    :destroy-on-close="true"
  >
    <a-input-search
      v-model:value="routeSearch"
      :placeholder="t('consoleLayout.searchRoute')"
      allow-clear
      style="margin-bottom: 12px;"
    />
    <a-collapse :bordered="false">
      <a-collapse-panel v-for="group in groupedRoutes" :key="group.key" :header="group.label">
        <a-list size="small" :data-source="group.items">
          <template #renderItem="{ item }">
            <a-list-item>
              <a-space direction="vertical" style="width: 100%;">
                <a-space>
                  <a-typography-text>{{ item.label }}</a-typography-text>
                  <a-tag v-if="item.permissionCode" color="blue">{{ item.permissionCode }}</a-tag>
                  <a-tag v-if="item.hasDynamicParam" color="orange">{{ t("consoleLayout.routeNeedContext") }}</a-tag>
                  <a-tag v-if="!item.allowed" color="red">{{ t("consoleLayout.routeNoPermission") }}</a-tag>
                </a-space>
                <a-space>
                  <a-typography-text type="secondary">{{ item.path }}</a-typography-text>
                  <a-button
                    type="link"
                    size="small"
                    :disabled="!item.allowed || !item.navigablePath"
                    @click="navigateFromDrawer(item.navigablePath)"
                  >
                    {{ t("consoleLayout.routeOpen") }}
                  </a-button>
                </a-space>
              </a-space>
            </a-list-item>
          </template>
        </a-list>
      </a-collapse-panel>
    </a-collapse>
  </a-drawer>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter, type RouteRecordNormalized } from "vue-router";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { useTagsViewStore } from "@/stores/tagsView";
import NotificationBell from "@/components/layout/NotificationBell.vue";
import UnifiedContextBar from "@/components/context/UnifiedContextBar.vue";
import { resolveRequiredPermission } from "@/router/route-access";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const userStore = useUserStore();
const permissionStore = usePermissionStore();
const tagsViewStore = useTagsViewStore();

const routeDrawerOpen = ref(false);
const routeSearch = ref("");

interface MenuItem {
  path: string;
  label: string;
}

interface RouteNavigatorItem {
  path: string;
  label: string;
  permissionCode?: string;
  hasDynamicParam: boolean;
  allowed: boolean;
  navigablePath?: string;
}

function isPrivilegedUser() {
  if (userStore.profile?.isPlatformAdmin) return true;
  return userStore.permissions.includes("*:*:*")
    || userStore.roles.some((role) => ["admin", "superadmin"].includes(role.toLowerCase()));
}

function hasPermission(permissionCode?: string) {
  if (!permissionCode) return true;
  if (isPrivilegedUser()) return true;
  return userStore.permissions.includes(permissionCode);
}

function resolveRouteLabel(routeRecord: RouteRecordNormalized): string {
  const titleKey = typeof routeRecord.meta.titleKey === "string" ? routeRecord.meta.titleKey : "";
  if (titleKey) return t(titleKey);
  const title = typeof routeRecord.meta.title === "string" ? routeRecord.meta.title : "";
  if (title) return title;
  if (typeof routeRecord.name === "string" && routeRecord.name.length > 0) return routeRecord.name;
  return routeRecord.path;
}

function resolveNavigablePath(rawPath: string): string | undefined {
  if (!rawPath.includes(":")) return rawPath;
  const params = route.params as Record<string, unknown>;
  let unresolved = false;
  const patched = rawPath.replace(/:([A-Za-z0-9_]+)/g, (_, key: string) => {
    const value = params[key];
    if (typeof value === "string" && value.length > 0) return encodeURIComponent(value);
    unresolved = true;
    return `{${key}}`;
  });
  return unresolved ? undefined : patched;
}

function resolveRouteGroupLabel(path: string): string {
  if (path.startsWith("/console")) return t("consoleLayout.groupRuntime");
  if (path.startsWith("/settings") || path.startsWith("/system") || path.startsWith("/monitor") || path.startsWith("/profile")) {
    return t("consoleLayout.groupSystem");
  }
  if (path.startsWith("/ai")) return t("consoleLayout.groupAi");
  if (path.startsWith("/approval")) return t("consoleLayout.groupApproval");
  if (path.startsWith("/apps") || path.startsWith("/lowcode") || path.startsWith("/visualization")) {
    return t("consoleLayout.groupApplicationDesign");
  }
  return t("consoleLayout.groupOther");
}

function shouldShowInNavigator(routeRecord: RouteRecordNormalized): boolean {
  if (!routeRecord.path.startsWith("/")) return false;
  if (["/", "/login", "/register", "/:pathMatch(.*)*"].includes(routeRecord.path)) return false;
  if (routeRecord.redirect) return false;
  return true;
}

const allNavigableRoutes = computed<RouteNavigatorItem[]>(() => {
  const deduplicated = new Map<string, RouteNavigatorItem>();
  router.getRoutes()
    .filter(shouldShowInNavigator)
    .forEach((routeRecord) => {
      const permissionCode = typeof routeRecord.meta.requiresPermission === "string"
        ? routeRecord.meta.requiresPermission
        : resolveRequiredPermission(routeRecord.path);
      const item: RouteNavigatorItem = {
        path: routeRecord.path,
        label: resolveRouteLabel(routeRecord),
        permissionCode,
        hasDynamicParam: routeRecord.path.includes(":"),
        allowed: hasPermission(permissionCode),
        navigablePath: resolveNavigablePath(routeRecord.path)
      };
      if (!deduplicated.has(item.path)) {
        deduplicated.set(item.path, item);
      }
    });
  return [...deduplicated.values()].sort((a, b) => a.path.localeCompare(b.path));
});

const primaryMenuItems = computed<MenuItem[]>(() => {
  const preferredPaths = [
    "/console",
    "/console/catalog",
    "/console/tenant-applications",
    "/lowcode/apps",
    "/settings/org/users",
    "/settings/org/departments",
    "/console/runtime-contexts",
    "/ai/agents",
    "/approval/workspace"
  ];
  const items: MenuItem[] = [];
  preferredPaths.forEach((path) => {
    const matched = allNavigableRoutes.value.find((item) => item.path === path);
    if (!matched || !matched.allowed) return;
    items.push({ path: matched.path, label: matched.label });
  });
  return items;
});

const groupedRoutes = computed(() => {
  const keyword = routeSearch.value.trim().toLowerCase();
  const matched = allNavigableRoutes.value.filter((item) => {
    if (!keyword) return true;
    return item.path.toLowerCase().includes(keyword) || item.label.toLowerCase().includes(keyword);
  });

  const groupMap = new Map<string, { key: string; label: string; items: RouteNavigatorItem[] }>();
  matched.forEach((item) => {
    const label = resolveRouteGroupLabel(item.path);
    const key = label;
    const target = groupMap.get(key) ?? { key, label, items: [] };
    target.items.push(item);
    groupMap.set(key, target);
  });

  return [...groupMap.values()];
});

const selectedKeys = computed(() => {
  const matched = primaryMenuItems.value.find((item) => route.path === item.path || route.path.startsWith(`${item.path}/`));
  return matched ? [matched.path] : [];
});

const profileDisplayName = computed(
  () => userStore.profile?.displayName || userStore.profile?.username || t("layout.profile")
);
const profileInitials = computed(() => profileDisplayName.value.slice(0, 2));

const canAccessCurrentRoute = computed(() => {
  const requiredPermission = typeof route.meta.requiresPermission === "string"
    ? route.meta.requiresPermission
    : resolveRequiredPermission(route.path);
  return hasPermission(requiredPermission);
});

function onPrimaryMenuClick(info: { key: string }) {
  go(info.key);
}

function go(path: string) {
  router.push(path);
}

function navigateFromDrawer(path?: string) {
  if (!path) return;
  routeDrawerOpen.value = false;
  go(path);
}

async function handleLogout() {
  await userStore.logout();
  permissionStore.reset();
  tagsViewStore.delAllViews();
  router.push("/login");
}
</script>

<style scoped>
.console-layout { min-height: 100vh; }

.console-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 16px 0 24px;
  height: 56px;
  background: var(--color-bg-container);
  border-bottom: 1px solid var(--color-border);
}

.left { display: flex; align-items: center; flex: 1; min-width: 0; overflow: hidden; gap: 20px; }
.brand { color: var(--color-text-primary); font-weight: 600; font-size: 16px; cursor: pointer; white-space: nowrap; flex-shrink: 0; }

.right { display: flex; align-items: center; gap: 8px; flex-shrink: 0; margin-left: 16px; }
.profile-btn { color: var(--color-text-primary); }
.profile-name { color: var(--color-text-primary); }
.route-nav-btn { margin-right: 4px; }

.console-content { min-height: calc(100vh - 56px); background: var(--color-bg-layout); }
.no-permission-state { min-height: calc(100vh - 56px); display: flex; justify-content: center; align-items: center; }
</style>
