<template>
  <a-layout class="workspace-layout">
    <a-layout-sider class="workspace-sider" :width="220">
      <div class="workspace-app-title">
        <div class="app-name">{{ appTitle }}</div>
        <a-button type="link" size="small" @click="router.push('/console')">返回控制台</a-button>
      </div>

      <a-menu mode="inline" theme="dark" :selected-keys="[activeMenuKey]">
        <a-menu-item key="dashboard" @click="go('dashboard')">仪表盘</a-menu-item>
        <a-menu-item key="forms" @click="go('forms')">表单</a-menu-item>
        <a-menu-item key="builder" @click="go('builder')">低代码设计器</a-menu-item>
        <a-menu-item key="approval" @click="go('approval')">审批</a-menu-item>
        <a-menu-item key="workflow" @click="go('workflow')">工作流</a-menu-item>
        <a-sub-menu key="settings-group" title="设置">
          <a-menu-item key="settings-datasource" @click="go('settings/datasource')">数据源</a-menu-item>
          <a-menu-item key="settings-sharing" @click="go('settings/sharing')">共享策略</a-menu-item>
          <a-menu-item key="settings-aliases" @click="go('settings/aliases')">实体别名</a-menu-item>
        </a-sub-menu>
      </a-menu>
    </a-layout-sider>

    <a-layout>
      <a-layout-header class="workspace-header">
        <a-breadcrumb>
          <a-breadcrumb-item>控制台</a-breadcrumb-item>
          <a-breadcrumb-item>{{ appTitle }}</a-breadcrumb-item>
          <a-breadcrumb-item>{{ route.meta?.title || "工作台" }}</a-breadcrumb-item>
        </a-breadcrumb>

        <div class="workspace-header-actions">
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

      <a-layout-content class="workspace-content">
        <router-view />
      </a-layout-content>
    </a-layout>
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

const appId = computed(() => route.params.appId as string);
const appTitle = computed(() => {
  const maybeName = typeof route.query.appName === "string" ? route.query.appName : null;
  return maybeName || `应用 ${appId.value}`;
});

const activeMenuKey = computed(() => {
  const path = route.path;
  if (path.endsWith("/dashboard")) return "dashboard";
  if (path.endsWith("/forms")) return "forms";
  if (path.endsWith("/builder")) return "builder";
  if (path.endsWith("/approval")) return "approval";
  if (path.endsWith("/workflow")) return "workflow";
  if (path.endsWith("/settings/datasource")) return "settings-datasource";
  if (path.endsWith("/settings/sharing")) return "settings-sharing";
  if (path.endsWith("/settings/aliases")) return "settings-aliases";
  return "dashboard";
});

const profileDisplayName = computed(
  () => userStore.profile?.displayName || userStore.profile?.username || "个人中心"
);
const profileInitials = computed(() => profileDisplayName.value.slice(0, 2));

const go = (childPath: string) => {
  router.push(`/apps/${appId.value}/${childPath}`);
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
.workspace-layout {
  min-height: 100vh;
}

.workspace-sider {
  box-shadow: 2px 0 8px rgba(0, 0, 0, 0.15);
}

.workspace-app-title {
  color: #fff;
  padding: 14px 12px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.16);
}

.app-name {
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 4px;
}

.workspace-header {
  background: #fff;
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 16px;
}

.workspace-header-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.workspace-content {
  padding: 16px;
  background: #f5f7fa;
}
</style>
