<template>
  <a-layout class="runtime-layout">
    <a-layout-header class="runtime-header">
      <div class="runtime-left">
        <a-button type="link" @click="go('/console')">{{ t("runtime.backToConsole") }}</a-button>
        <span class="runtime-title">{{ runtimeTitle }}</span>
        <UnifiedContextBar />
      </div>
      <div class="runtime-right">
        <LocaleSwitch />
        <NotificationBell />
        <a-badge :count="taskTotal" :overflow-count="99">
          <a-button size="small" @click="reloadTasks">{{ t("runtime.pendingTasks") }}</a-button>
        </a-badge>
        <a-dropdown trigger="click">
          <a-button type="text" class="profile-btn">
            <a-space>
              <a-avatar size="small">{{ profileInitials }}</a-avatar>
              <span class="profile-name">{{ profileDisplayName }}</span>
            </a-space>
          </a-button>
          <template #overlay>
            <a-menu>
              <a-menu-item key="profile" @click="go('/profile')">{{ t("layout.profile") }}</a-menu-item>
              <a-menu-divider />
              <a-menu-item key="logout" @click="logout">{{ t("layout.logout") }}</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
      </div>
    </a-layout-header>

    <a-layout has-sider>
      <a-layout-sider v-if="!isMobile" theme="light" width="220" class="runtime-sider">
        <a-menu :selected-keys="selectedKeys" mode="inline">
          <a-menu-item v-for="item in menuItems" :key="item.pageKey" @click="goRuntime(item.pageKey)">
            {{ item.title }}
          </a-menu-item>
        </a-menu>
      </a-layout-sider>
      <a-layout-content class="runtime-content">
        <RouterView />
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import UnifiedContextBar from "@/components/context/UnifiedContextBar.vue";
import LocaleSwitch from "@/components/layout/LocaleSwitch.vue";
import NotificationBell from "@/components/layout/NotificationBell.vue";
import { getRuntimeMenu, getRuntimeTasks, type RuntimeMenuItem } from "@/services/api-productization";
import { usePermissionStore } from "@/stores/permission";
import { useTagsViewStore } from "@/stores/tagsView";
import { useUserStore } from "@/stores/user";
import { resolveRouteTitle } from "@/utils/i18n-navigation";

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const userStore = useUserStore();
const permissionStore = usePermissionStore();
const tagsViewStore = useTagsViewStore();

const menuItems = ref<RuntimeMenuItem[]>([]);
const taskTotal = ref(0);
const viewportWidth = ref(typeof window === "undefined" ? 1024 : window.innerWidth);

const runtimeTitle = computed(() =>
  resolveRouteTitle(route.meta, route.path, typeof route.meta.title === "string" ? route.meta.title : t("route.runtimeDelivery"))
);
const appKey = computed(() => String(route.params.appKey || ""));
const pageKey = computed(() => String(route.params.pageKey || ""));
const selectedKeys = computed(() => (pageKey.value ? [pageKey.value] : []));
const isMobile = computed(() => viewportWidth.value <= 768);
const profileDisplayName = computed(
  () => userStore.profile?.displayName || userStore.profile?.username || t("layout.profile")
);
const profileInitials = computed(() => profileDisplayName.value.slice(0, 2));

function handleResize() {
  viewportWidth.value = window.innerWidth;
}

async function loadRuntimeMenu() {
  if (!appKey.value) {
    menuItems.value = [];
    return;
  }
  const response = await getRuntimeMenu(appKey.value);
  menuItems.value = response.items ?? [];
}

async function reloadTasks() {
  const tasks = await getRuntimeTasks(1, 20);
  taskTotal.value = tasks.total;
}

function go(path: string) {
  router.push(path);
}

function goRuntime(targetPageKey: string) {
  if (!appKey.value || !targetPageKey) {
    return;
  }
  router.push(`/r/${appKey.value}/${targetPageKey}`);
}

async function logout() {
  await userStore.logout();
  permissionStore.reset();
  tagsViewStore.delAllViews();
  router.push("/login");
}

watch(() => appKey.value, () => {
  void loadRuntimeMenu();
});

onMounted(() => {
  window.addEventListener("resize", handleResize);
  void loadRuntimeMenu();
  void reloadTasks();
});

onUnmounted(() => {
  window.removeEventListener("resize", handleResize);
});
</script>

<style scoped>
.runtime-layout {
  min-height: 100vh;
}

.runtime-header {
  height: var(--header-height);
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid var(--color-border);
  background: var(--color-bg-container);
  padding: 0 var(--spacing-md);
}

.runtime-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.runtime-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.runtime-title {
  font-size: 16px;
  font-weight: 600;
}

.runtime-content {
  min-height: calc(100vh - var(--header-height));
  background: var(--color-bg-layout);
  padding: var(--spacing-md);
}

.runtime-sider {
  border-right: 1px solid var(--color-border);
  min-height: calc(100vh - var(--header-height));
  background: var(--color-bg-container);
}

.profile-btn {
  color: var(--color-text-primary);
}

.profile-name {
  color: var(--color-text-primary);
}
</style>
