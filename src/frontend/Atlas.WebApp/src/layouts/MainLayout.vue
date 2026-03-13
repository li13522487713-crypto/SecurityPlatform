<template>
  <router-view v-if="isAuthPage" />
  <a-layout v-else class="app-shell" :class="{ mobile: isMobile }">
    <!-- Mobile Mask -->
    <div v-if="isMobile && !collapsed" class="drawer-bg" @click="collapsed = true" />

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
    
    <a-layout :class="{ 'main-container': true, 'mobile-main': isMobile }">
      <a-layout-header class="app-header">
        <div class="header-left">
          <MenuUnfoldOutlined v-if="collapsed" class="trigger" @click="() => (collapsed = !collapsed)" />
          <MenuFoldOutlined v-else class="trigger" @click="() => (collapsed = !collapsed)" />
          <BreadcrumbView />
          <ProjectSwitcher v-if="!isMobile" class="project-switcher-wrapper" />
        </div>
        <div class="header-right">
          <Screenfull />
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
                <a-menu-item key="profile" @click="openProfile">个人中心</a-menu-item>
                <a-menu-divider />
                <a-menu-item key="logout" @click="logout">退出登录</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
        </div>
      </a-layout-header>
      <div class="tags-container">
        <TagsView />
      </div>
      <a-layout-content class="app-content">
        <router-view v-slot="{ Component, route }">
          <transition name="fade-transform" mode="out-in">
            <keep-alive :include="cachedViews">
              <component :is="Component" :key="route.path" />
            </keep-alive>
          </transition>
        </router-view>
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { useTagsViewStore } from "@/stores/tagsView";
import { useResize } from "@/composables/useResize";
import SidebarLogo from "@/components/layout/SidebarLogo.vue";
import SidebarMenu from "@/components/layout/SidebarMenu.vue";
import TagsView from "@/components/layout/TagsView.vue";
import BreadcrumbView from "@/components/layout/BreadcrumbView.vue";
import NotificationBell from "@/components/layout/NotificationBell.vue";
import ProjectSwitcher from "@/components/ProjectSwitcher.vue";
import Screenfull from "@/components/layout/Screenfull.vue";
import { MenuUnfoldOutlined, MenuFoldOutlined } from "@ant-design/icons-vue";

const route = useRoute();
const router = useRouter();
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
const profileDisplayName = computed(
  () => userStore.profile?.displayName || userStore.profile?.username || "个人中心"
);
const profileInitials = computed(() => {
  const name = profileDisplayName.value;
  return name.slice(0, 2);
});

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
  background: #2b2f3a !important; /* Sidebar 基础颜色 */
  box-shadow: 2px 0 6px rgba(0,21,41,.35);
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

.brand {
  height: 48px;
  line-height: 48px;
  text-align: center;
  color: var(--color-text-white);
  font-weight: 600;
  font-size: 16px;
  background: rgba(255, 255, 255, 0.1);
  overflow: hidden;
  white-space: nowrap;
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

/* 移动端样式 */
.mobile .sidebar-container {
  transition: transform .28s;
  width: 210px !important;
}

.mobile .hide-sidebar {
  pointer-events: none;
  transition: transform .28s;
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

/* fade-transform transition */
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

/* breadcrumb transition */
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
