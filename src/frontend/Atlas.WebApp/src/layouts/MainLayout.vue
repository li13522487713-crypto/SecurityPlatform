<template>
  <router-view v-if="isAuthPage" />
  <div v-else data-testid="e2e-shell-main">
    <a-layout class="app-shell" :class="{ mobile: isMobile }">
      <div v-if="isMobile && !collapsed" class="drawer-bg" @click="collapsed = true" />

      <div data-testid="e2e-sidebar">
        <a-layout-sider
          v-model:collapsed="collapsed"
          :trigger="null"
          collapsible
          class="sidebar-container"
          :class="{ 'hide-sidebar': isMobile && collapsed, 'open-sidebar': !collapsed }"
          :width="210"
        >
          <SidebarLogo :collapse="collapsed" />
          <div class="scrollbar-wrapper">
            <SidebarMenu />
          </div>
        </a-layout-sider>
      </div>

      <a-layout :class="{ 'main-container': true, 'mobile-main': isMobile }">
        <div data-testid="e2e-header">
          <a-layout-header class="app-header">
            <div class="header-left">
              <MenuUnfoldOutlined
                v-if="collapsed"
                class="trigger"
                data-testid="e2e-sidebar-toggle"
                @click="() => (collapsed = !collapsed)"
              />
              <MenuFoldOutlined
                v-else
                class="trigger"
                data-testid="e2e-sidebar-toggle"
                @click="() => (collapsed = !collapsed)"
              />
              <BreadcrumbView />
              <ProjectSwitcher v-if="!isMobile" class="project-switcher-wrapper" />
            </div>
            <div class="header-right" data-testid="e2e-header-actions">
              <LocaleSwitch />
              <Screenfull />
              <NotificationBell />
              <a-dropdown trigger="click">
                <span data-testid="e2e-user-menu-trigger">
                  <a-button type="text">
                    <a-space>
                      <a-avatar size="small">{{ profileInitials }}</a-avatar>
                      <span>{{ profileDisplayName }}</span>
                    </a-space>
                  </a-button>
                </span>
                <template #overlay>
                  <div data-testid="e2e-user-menu">
                    <a-menu>
                      <a-menu-item key="profile" @click="openProfile">
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
        <div class="tags-container" data-testid="e2e-tags-container">
          <TagsView />
        </div>
        <div data-testid="e2e-content">
          <a-layout-content class="app-content">
            <router-view v-slot="{ Component, route }">
              <transition name="fade-transform" mode="out-in">
                <keep-alive :include="cachedViews">
                  <component :is="Component" :key="route.path" />
                </keep-alive>
              </transition>
            </router-view>
          </a-layout-content>
        </div>
      </a-layout>
    </a-layout>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { MenuFoldOutlined, MenuUnfoldOutlined } from "@ant-design/icons-vue";
import { useI18n } from "vue-i18n";
import SidebarLogo from "@/components/layout/SidebarLogo.vue";
import SidebarMenu from "@/components/layout/SidebarMenu.vue";
import TagsView from "@/components/layout/TagsView.vue";
import BreadcrumbView from "@/components/layout/BreadcrumbView.vue";
import NotificationBell from "@/components/layout/NotificationBell.vue";
import ProjectSwitcher from "@/components/ProjectSwitcher.vue";
import LocaleSwitch from "@/components/layout/LocaleSwitch.vue";
import Screenfull from "@/components/layout/Screenfull.vue";
import { useResize } from "@/composables/useResize";
import { usePermissionStore } from "@/stores/permission";
import { useTagsViewStore } from "@/stores/tagsView";
import { useUserStore } from "@/stores/user";

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const userStore = useUserStore();
const permissionStore = usePermissionStore();
const tagsViewStore = useTagsViewStore();
const { isMobile } = useResize();
const collapsed = ref(false);

watch(isMobile, (mobile) => {
  if (mobile) {
    collapsed.value = true;
  }
});

watch(
  () => route.fullPath,
  () => {
    if (isMobile.value && !collapsed.value) {
      collapsed.value = true;
    }
  }
);

const isAuthPage = computed(() => route.path === "/login" || route.path === "/register");
const profileDisplayName = computed(() => userStore.profile?.displayName || userStore.profile?.username || t("layout.profile"));
const profileInitials = computed(() => profileDisplayName.value.slice(0, 2));
const cachedViews = computed(() => tagsViewStore.cachedViews);

function openProfile() {
  router.push("/profile");
}

async function logout() {
  await userStore.logout();
  permissionStore.reset();
  tagsViewStore.delAllViews();
  router.push("/login");
}
</script>

<style scoped>
.app-shell {
  min-height: 100vh;
  position: relative;
  width: 100%;
}

.drawer-bg {
  background: rgba(0, 0, 0, 0.3);
  width: 100%;
  top: 0;
  height: 100%;
  position: absolute;
  z-index: 999;
}

.sidebar-container {
  transition: width 0.28s;
  background: var(--color-bg-container) !important;
  border-right: 1px solid var(--color-border);
  z-index: 1001;
}

.scrollbar-wrapper {
  height: calc(100vh - 50px);
  overflow-y: auto;
  overflow-x: hidden;
}

.scrollbar-wrapper::-webkit-scrollbar {
  width: 6px;
}

.scrollbar-wrapper::-webkit-scrollbar-thumb {
  background: rgba(144, 147, 153, 0.3);
  border-radius: 3px;
}

.app-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: var(--color-bg-container);
  padding: 0 var(--spacing-md);
  height: var(--header-height);
  line-height: var(--header-height);
  border-bottom: 1px solid var(--color-border);
  z-index: 10;
  transition: width 0.28s;
}

.mobile .sidebar-container {
  transition: transform 0.28s;
  width: 210px !important;
}

.mobile .hide-sidebar {
  pointer-events: none;
  transition: transform 0.28s;
  transform: translate3d(-210px, 0, 0);
}

.mobile .main-container {
  margin-left: 0;
}

.trigger {
  font-size: 18px;
  cursor: pointer;
  transition: color 0.3s;
  margin-right: 16px;
}

.trigger:hover {
  color: var(--color-primary);
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 16px;
}

.project-switcher-wrapper {
  margin-left: 4px;
}

.tags-container {
  background: var(--color-bg-container);
}

.app-content {
  margin: var(--spacing-md);
  position: relative;
}

.fade-transform-leave-active,
.fade-transform-enter-active {
  transition: all 0.3s;
}

.fade-transform-enter-from {
  opacity: 0;
  transform: translateX(-30px);
}

.fade-transform-leave-to {
  opacity: 0;
  transform: translateX(30px);
}

.breadcrumb-enter-active,
.breadcrumb-leave-active {
  transition: all 0.5s;
}

.breadcrumb-enter-from,
.breadcrumb-leave-active {
  opacity: 0;
  transform: translateX(20px);
}

.breadcrumb-leave-active {
  position: absolute;
}
</style>
