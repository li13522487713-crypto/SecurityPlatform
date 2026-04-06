<template>
  <div class="shell">
    <!-- Mobile Overlay -->
    <div
      v-if="isMobileOpen"
      class="sidebar-overlay"
      @click="isMobileOpen = false"
    />

    <!-- Sidebar -->
    <aside class="sidebar" :class="{ 'sidebar--open': isMobileOpen }">
      <div class="sidebar__brand" @click="go('/console')">
        <div class="sidebar__logo">
          <AppstoreOutlined class="sidebar__logo-icon" />
        </div>
        <span class="sidebar__brand-text">{{ t("consoleLayout.brandTitle") }}</span>
      </div>

      <nav class="sidebar__nav">
        <div class="sidebar__group">
          <button
            v-for="item in mainNavItems"
            :key="item.path"
            type="button"
            class="sidebar__item"
            :class="{ 'sidebar__item--active': isActive(item.path) }"
            @click="handleSidebarNav(item.path)"
          >
            <div class="sidebar__item-left">
              <component :is="item.icon" class="sidebar__item-icon" />
              <span class="sidebar__item-label">{{ item.label }}</span>
            </div>
            <span v-if="item.badge" class="sidebar__badge">{{ item.badge }}</span>
          </button>
        </div>

        <div class="sidebar__group">
          <div class="sidebar__group-title">{{ t("consoleLayout.sidebarGroupMonitor") }}</div>
          <button
            v-for="item in monitorNavItems"
            :key="item.path"
            type="button"
            class="sidebar__item"
            :class="{ 'sidebar__item--active': isActive(item.path) }"
            @click="handleSidebarNav(item.path)"
          >
            <div class="sidebar__item-left">
              <component :is="item.icon" class="sidebar__item-icon" />
              <span class="sidebar__item-label">{{ item.label }}</span>
            </div>
            <span v-if="item.badge" class="sidebar__badge">{{ item.badge }}</span>
          </button>
        </div>
      </nav>

      <div class="sidebar__footer">
        <button
          type="button"
          class="sidebar__item"
          :class="{ 'sidebar__item--active': isActive('/settings/system/configs') }"
          @click="handleSidebarNav('/settings/system/configs')"
        >
          <div class="sidebar__item-left">
            <SettingOutlined class="sidebar__item-icon" />
            <span class="sidebar__item-label">{{ t("consoleLayout.sidebarSettings") }}</span>
          </div>
        </button>
      </div>
    </aside>

    <!-- Main Area -->
    <div class="main-area">
      <!-- Top Bar -->
      <header class="topbar">
        <div class="topbar__left">
          <button type="button" class="topbar__menu-btn" @click="isMobileOpen = true">
            <MenuOutlined />
          </button>

          <div class="topbar__search-wrapper">
            <SearchOutlined class="topbar__search-icon" />
            <input
              type="text"
              class="topbar__search-input"
              :placeholder="t('consoleLayout.searchPlaceholder')"
              @click="routeDrawerOpen = true"
              readonly
            />
            <kbd class="topbar__kbd">⌘K</kbd>
          </div>
        </div>

        <div class="topbar__right">
          <div class="topbar__tenant">
            <span class="topbar__tenant-dot" />
            {{ t("consoleLayout.tenantLabel") }}: {{ tenantShortId }}
          </div>

          <LocaleSwitch />
          <UnifiedContextBar />
          <NotificationBell />

          <div class="topbar__divider" />

          <a-dropdown trigger="click">
            <button type="button" class="topbar__profile">
              <a-avatar :size="32" class="topbar__avatar">{{ profileInitials }}</a-avatar>
              <span class="topbar__profile-name">{{ profileDisplayName }}</span>
            </button>
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
      </header>

      <!-- Content -->
      <main class="main-content">
        <router-view v-if="canAccessCurrentRoute" />
        <div v-else class="no-permission-state">
          <a-empty :description="t('consoleLayout.noAccess')" />
        </div>
      </main>
    </div>

    <!-- Route Navigator Drawer -->
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
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import type { Component } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter, type RouteRecordNormalized } from "vue-router";
import {
  ApiOutlined,
  AppstoreOutlined,
  HomeOutlined,
  InboxOutlined,
  TeamOutlined,
  SafetyCertificateOutlined,
  RobotOutlined,
  DashboardOutlined,
  DatabaseOutlined,
  SettingOutlined,
  MenuOutlined,
  SearchOutlined,
} from "@ant-design/icons-vue";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { useTagsViewStore } from "@/stores/tagsView";
import NotificationBell from "@/components/layout/NotificationBell.vue";
import LocaleSwitch from "@/components/layout/LocaleSwitch.vue";
import UnifiedContextBar from "@/components/context/UnifiedContextBar.vue";
import { resolveRequiredPermission } from "@/router/route-access";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const userStore = useUserStore();
const permissionStore = usePermissionStore();
const tagsViewStore = useTagsViewStore();

const isMobileOpen = ref(false);
const routeDrawerOpen = ref(false);
const routeSearch = ref("");

interface SidebarNavItem {
  path: string;
  label: string;
  icon: Component;
  badge?: string;
  permissionCode?: string;
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

function toSidebarLocaleKey(path: string) {
  return `consoleLayout.sidebar_${path
    .replace(/\//g, "_")
    .replace(/^_/, "")
    .replace(/-/g, "_")}`;
}

const mainNavItemsSource: SidebarNavItem[] = [
  { path: "/console", label: "", icon: HomeOutlined },
  { path: "/console/catalog", label: "", icon: InboxOutlined },
  { path: "/settings/org/users", label: "", icon: TeamOutlined, permissionCode: "users:view" },
  { path: "/settings/auth/roles", label: "", icon: SafetyCertificateOutlined, permissionCode: "roles:view" },
];

const monitorNavItemsSource: SidebarNavItem[] = [
  { path: "/ai/agents", label: "", icon: RobotOutlined, badge: "New" },
  { path: "/ai/model-configs", label: "", icon: ApiOutlined },
  { path: "/monitor/server-info", label: "", icon: DashboardOutlined },
  { path: "/settings/system/datasources", label: "", icon: DatabaseOutlined },
];

const mainNavItems = computed(() =>
  mainNavItemsSource
    .filter((item) => hasPermission(item.permissionCode))
    .map((item) => ({
      ...item,
      label: t(toSidebarLocaleKey(item.path)),
    }))
);

const monitorNavItems = computed(() =>
  monitorNavItemsSource
    .filter((item) => hasPermission(item.permissionCode))
    .map((item) => ({
      ...item,
      label: t(toSidebarLocaleKey(item.path)),
    }))
);

function isActive(path: string) {
  if (path === "/console") return route.path === "/console" || route.path === "/";
  return route.path === path || route.path.startsWith(`${path}/`);
}

function handleSidebarNav(path: string) {
  isMobileOpen.value = false;
  go(path);
}

const tenantShortId = computed(() => {
  const tid = userStore.profile?.tenantId ?? "";
  if (tid.length > 8) return tid.substring(tid.length - 4);
  return tid || "0001";
});

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
.shell {
  display: flex;
  height: 100vh;
  background: #f4f7f9;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  color: #111827;
  overflow: hidden;
}

/* ── Sidebar Overlay (mobile) ── */
.sidebar-overlay {
  position: fixed;
  inset: 0;
  background: rgba(17, 24, 39, 0.2);
  backdrop-filter: blur(4px);
  z-index: 40;
}

/* ── Sidebar ── */
.sidebar {
  position: fixed;
  inset-block: 0;
  left: 0;
  z-index: 50;
  width: 256px;
  background: #fbfcfd;
  border-right: 1px solid rgba(229, 231, 235, 0.8);
  display: flex;
  flex-direction: column;
  transition: transform 0.3s ease-in-out;
  transform: translateX(-100%);
}

@media (min-width: 768px) {
  .sidebar {
    position: static;
    transform: translateX(0);
  }
}

.sidebar--open {
  transform: translateX(0);
}

.sidebar__brand {
  height: 64px;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 0 24px;
  border-bottom: 1px solid #f3f4f6;
  cursor: pointer;
  flex-shrink: 0;
}

.sidebar__logo {
  background: linear-gradient(135deg, #6366f1, #4338ca);
  border-radius: 8px;
  padding: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.1);
}

.sidebar__logo-icon {
  color: #fff;
  font-size: 20px;
}

.sidebar__brand-text {
  font-weight: 600;
  font-size: 18px;
  color: #111827;
  letter-spacing: -0.02em;
}

.sidebar__nav {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.sidebar__group {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.sidebar__group-title {
  padding: 0 12px 8px;
  font-size: 11px;
  font-weight: 600;
  color: #9ca3af;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.sidebar__item {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  border-radius: 8px;
  border: none;
  background: none;
  cursor: pointer;
  font: inherit;
  font-size: 14px;
  color: #4b5563;
  transition: all 0.15s ease;
}

.sidebar__item:hover {
  background: rgba(243, 244, 246, 0.8);
  color: #111827;
}

.sidebar__item--active {
  background: #eef2ff;
  color: #4338ca;
  font-weight: 500;
}

.sidebar__item--active .sidebar__item-icon {
  color: #4f46e5;
}

.sidebar__item-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.sidebar__item-icon {
  font-size: 18px;
  color: #9ca3af;
  flex-shrink: 0;
}

.sidebar__item:hover .sidebar__item-icon {
  color: #6b7280;
}

.sidebar__item--active .sidebar__item-icon {
  color: #4f46e5;
}

.sidebar__badge {
  padding: 1px 8px;
  font-size: 10px;
  font-weight: 700;
  background: #e0e7ff;
  color: #4338ca;
  border-radius: 9999px;
}

.sidebar__footer {
  padding: 16px;
  border-top: 1px solid #f3f4f6;
  flex-shrink: 0;
}

/* ── Main Area ── */
.main-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  position: relative;
}

/* ── Top Bar ── */
.topbar {
  height: 64px;
  background: rgba(255, 255, 255, 0.7);
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  border-bottom: 1px solid rgba(229, 231, 235, 0.6);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 16px;
  flex-shrink: 0;
  position: sticky;
  top: 0;
  z-index: 30;
}

@media (min-width: 768px) {
  .topbar { padding: 0 32px; }
}

.topbar__left {
  display: flex;
  align-items: center;
  gap: 16px;
  flex: 1;
}

.topbar__menu-btn {
  padding: 8px;
  margin-left: -8px;
  color: #6b7280;
  background: none;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 16px;
  display: flex;
  align-items: center;
}

.topbar__menu-btn:hover {
  background: #f3f4f6;
}

@media (min-width: 768px) {
  .topbar__menu-btn { display: none; }
}

.topbar__search-wrapper {
  max-width: 448px;
  width: 100%;
  position: relative;
  display: none;
}

@media (min-width: 640px) {
  .topbar__search-wrapper { display: block; }
}

.topbar__search-icon {
  position: absolute;
  left: 12px;
  top: 50%;
  transform: translateY(-50%);
  color: #9ca3af;
  font-size: 14px;
  pointer-events: none;
}

.topbar__search-input {
  width: 100%;
  background: rgba(243, 244, 246, 0.6);
  border: 1px solid transparent;
  border-radius: 8px;
  padding: 8px 56px 8px 36px;
  font-size: 14px;
  color: #111827;
  outline: none;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.03);
  transition: all 0.15s;
  cursor: pointer;
  font-family: inherit;
}

.topbar__search-input::placeholder {
  color: #6b7280;
}

.topbar__search-input:hover {
  background: rgba(243, 244, 246, 0.8);
}

.topbar__kbd {
  position: absolute;
  right: 8px;
  top: 50%;
  transform: translateY(-50%);
  display: flex;
  align-items: center;
  gap: 2px;
  background: #fff;
  border: 1px solid #e5e7eb;
  border-radius: 4px;
  padding: 2px 6px;
  font-size: 10px;
  color: #6b7280;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
  font-family: inherit;
  pointer-events: none;
}

.topbar__right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.topbar__tenant {
  display: none;
  align-items: center;
  gap: 8px;
  padding: 4px 12px;
  background: rgba(238, 242, 255, 0.5);
  color: #4338ca;
  border-radius: 9999px;
  border: 1px solid rgba(224, 231, 255, 0.5);
  font-size: 14px;
  font-weight: 500;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.03);
}

@media (min-width: 1024px) {
  .topbar__tenant { display: flex; }
}

.topbar__tenant-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #6366f1;
  animation: pulse-dot 2s cubic-bezier(0.4, 0, 0.6, 1) infinite;
}

@keyframes pulse-dot {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.topbar__divider {
  width: 1px;
  height: 24px;
  background: #e5e7eb;
  display: none;
}

@media (min-width: 640px) {
  .topbar__divider { display: block; }
}

.topbar__profile {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 12px 4px 4px;
  border-radius: 9999px;
  border: 1px solid transparent;
  background: none;
  cursor: pointer;
  transition: all 0.15s;
  font: inherit;
}

.topbar__profile:hover {
  background: #f3f4f6;
  border-color: #e5e7eb;
}

.topbar__avatar {
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.08);
}

.topbar__profile-name {
  font-size: 14px;
  font-weight: 500;
  color: #374151;
  display: none;
}

@media (min-width: 640px) {
  .topbar__profile-name { display: inline; }
}

/* ── Main Content ── */
.main-content {
  flex: 1;
  overflow-y: auto;
}

.no-permission-state {
  min-height: calc(100vh - 64px);
  display: flex;
  justify-content: center;
  align-items: center;
}
</style>
