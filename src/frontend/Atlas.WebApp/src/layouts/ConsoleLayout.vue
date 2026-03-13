<template>
  <a-layout class="console-layout">
    <a-layout-header class="console-header">
      <div class="left">
        <div class="brand" @click="go('/console')">Atlas Console</div>
        <a-menu
          mode="horizontal"
          theme="dark"
          :selected-keys="selectedKeys"
          @click="onMenuClick"
        >
          <a-menu-item key="/console">平台首页</a-menu-item>
          <a-menu-item key="/console/apps">应用管理</a-menu-item>
          <a-menu-item key="/console/datasources">数据源管理</a-menu-item>
          <a-menu-item key="/console/settings/system/configs">系统设置</a-menu-item>
        </a-menu>
      </div>
      <div class="right">
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
              <a-menu-item key="profile" @click="go('/profile')">个人中心</a-menu-item>
              <a-menu-divider />
              <a-menu-item key="logout" @click="logout">退出登录</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
      </div>
    </a-layout-header>

    <a-layout-content class="console-content">
      <router-view />
    </a-layout-content>
  </a-layout>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { useTagsViewStore } from "@/stores/tagsView";
import NotificationBell from "@/components/layout/NotificationBell.vue";

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
}

.left {
  display: flex;
  align-items: center;
  min-width: 0;
}

.brand {
  color: var(--color-text-white);
  font-weight: 600;
  margin-right: 20px;
  cursor: pointer;
  white-space: nowrap;
}

.right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.profile-btn {
  color: var(--color-text-white);
}

.profile-name {
  color: var(--color-text-white);
}

.console-content {
  min-height: calc(100vh - 56px);
  background: var(--color-bg-layout);
}
</style>
