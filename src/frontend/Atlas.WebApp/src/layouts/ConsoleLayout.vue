<template>
  <a-layout class="console-layout">
    <a-layout-header class="console-header">
      <div class="console-brand" @click="goConsoleHome">
        <div class="brand-logo">Atlas</div>
        <div class="brand-text">
          <div class="brand-title">平台控制台</div>
          <div class="brand-subtitle">Console</div>
        </div>
      </div>

      <a-menu mode="horizontal" :selected-keys="[activeTab]" class="console-menu">
        <a-menu-item key="apps" @click="router.push('/console')">应用</a-menu-item>
        <a-menu-item key="datasources" @click="router.push('/console/datasources')">数据源</a-menu-item>
        <a-menu-item key="settings" @click="router.push('/console/settings/users')">设置</a-menu-item>
      </a-menu>

      <div class="console-actions">
        <ProjectSwitcher />
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

    <a-layout-content class="console-content">
      <router-view />
    </a-layout-content>
  </a-layout>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { usePermissionStore } from "@/stores/permission";
import { useTagsViewStore } from "@/stores/tagsView";
import { useUserStore } from "@/stores/user";
import ProjectSwitcher from "@/components/ProjectSwitcher.vue";

const route = useRoute();
const router = useRouter();
const userStore = useUserStore();
const permissionStore = usePermissionStore();
const tagsViewStore = useTagsViewStore();

const activeTab = computed(() => {
  if (route.path.startsWith("/console/datasources")) {
    return "datasources";
  }
  if (route.path.startsWith("/console/settings")) {
    return "settings";
  }
  return "apps";
});

const profileDisplayName = computed(
  () => userStore.profile?.displayName || userStore.profile?.username || "个人中心"
);
const profileInitials = computed(() => profileDisplayName.value.slice(0, 2));

const goConsoleHome = () => {
  router.push("/console");
};

const openProfile = () => {
  router.push("/profile");
};

const logout = async () => {
  await userStore.logout();
  permissionStore.reset();
  tagsViewStore.delAllViews();
  router.push("/login");
};
</script>

<style scoped>
.console-layout {
  min-height: 100vh;
  background: #f5f7fa;
}

.console-header {
  display: flex;
  align-items: center;
  background: #fff;
  border-bottom: 1px solid #f0f0f0;
  padding: 0 16px;
  height: 56px;
  line-height: 56px;
}

.console-brand {
  display: flex;
  align-items: center;
  cursor: pointer;
  min-width: 180px;
  margin-right: 24px;
}

.brand-logo {
  width: 34px;
  height: 34px;
  border-radius: 8px;
  background: #1677ff;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 700;
  margin-right: 10px;
}

.brand-title {
  font-size: 14px;
  line-height: 1.2;
  font-weight: 600;
}

.brand-subtitle {
  font-size: 12px;
  line-height: 1.2;
  color: #999;
}

.console-menu {
  flex: 1;
  border-bottom: none;
}

.console-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.console-content {
  padding: 16px;
}
</style>
