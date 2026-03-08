<template>
  <a-layout class="runtime-layout">
    <a-layout-header class="runtime-header">
      <div class="runtime-left">
        <a-button type="link" @click="go('/console')">返回控制台</a-button>
        <span class="runtime-title">{{ runtimeTitle }}</span>
      </div>
      <div class="runtime-right">
        <NotificationBell />
        <a-badge :count="taskTotal" :overflow-count="99">
          <a-button size="small" @click="reloadTasks">待办</a-button>
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
              <a-menu-item key="profile" @click="go('/profile')">个人中心</a-menu-item>
              <a-menu-divider />
              <a-menu-item key="logout" @click="logout">退出登录</a-menu-item>
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
import { computed, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { useTagsViewStore } from "@/stores/tagsView";
import NotificationBell from "@/components/layout/NotificationBell.vue";
import { getRuntimeMenu, getRuntimeTasks, type RuntimeMenuItem } from "@/services/api-productization";

const route = useRoute();
const router = useRouter();
const userStore = useUserStore();
const permissionStore = usePermissionStore();
const tagsViewStore = useTagsViewStore();

const menuItems = ref<RuntimeMenuItem[]>([]);
const taskTotal = ref(0);

const runtimeTitle = computed(() => route.meta.title || "运行交付面");
const appKey = computed(() => String(route.params.appKey || ""));
const pageKey = computed(() => String(route.params.pageKey || ""));
const selectedKeys = computed(() => (pageKey.value ? [pageKey.value] : []));
const isMobile = computed(() => window.innerWidth <= 768);
const profileDisplayName = computed(
  () => userStore.profile?.displayName || userStore.profile?.username || "个人中心"
);
const profileInitials = computed(() => profileDisplayName.value.slice(0, 2));

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
  void loadRuntimeMenu();
  void reloadTasks();
});
</script>

<style scoped>
.runtime-layout {
  min-height: 100vh;
}

.runtime-header {
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid #f0f0f0;
  background: #fff;
  padding: 0 16px;
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
  min-height: calc(100vh - 56px);
  background: #f5f7fb;
  padding: 16px;
}

.runtime-sider {
  border-right: 1px solid #f0f0f0;
  min-height: calc(100vh - 56px);
  background: #fff;
}

.profile-btn {
  color: #1f1f1f;
}

.profile-name {
  color: #1f1f1f;
}
</style>
