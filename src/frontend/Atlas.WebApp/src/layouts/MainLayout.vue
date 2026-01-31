<template>
  <router-view v-if="isLogin" />
  <a-layout v-else class="app-shell">
    <a-layout-sider collapsible :collapsed="collapsed" @collapse="toggle">
      <div class="brand">Atlas 安全平台</div>
      <a-menu theme="dark" mode="inline" :selected-keys="selectedKeys">
        <a-menu-item key="home" @click="go('/')">总览</a-menu-item>
        <a-menu-item key="assets" @click="go('/assets')">资产</a-menu-item>
        <a-menu-item key="audit" @click="go('/audit')">审计</a-menu-item>
        <a-menu-item key="alert" @click="go('/alert')">告警</a-menu-item>
        <a-menu-item key="approval" @click="go('/approval/flows')">审批流</a-menu-item>
        <a-sub-menu v-if="showSystemMenu" key="system" title="系统管理">
          <a-menu-item v-if="showUsersMenu" key="system-users" @click="go('/system/users')">
            员工管理
          </a-menu-item>
          <a-menu-item v-if="showRolesMenu" key="system-roles" @click="go('/system/roles')">
            角色管理
          </a-menu-item>
          <a-menu-item v-if="showPermissionsMenu" key="system-permissions" @click="go('/system/permissions')">
            权限管理
          </a-menu-item>
          <a-menu-item v-if="showMenusMenu" key="system-menus" @click="go('/system/menus')">
            菜单管理
          </a-menu-item>
          <a-menu-item v-if="showDepartmentsMenu" key="system-departments" @click="go('/system/departments')">
            部门管理
          </a-menu-item>
          <a-menu-item v-if="showPositionsMenu" key="system-positions" @click="go('/system/positions')">
            职位管理
          </a-menu-item>
        </a-sub-menu>
        <a-sub-menu key="visualization" title="可视化中心">
          <a-menu-item key="visualization-center" @click="go('/visualization/center')">
            总览
          </a-menu-item>
          <a-menu-item key="visualization-designer" @click="go('/visualization/designer')">
            设计器
          </a-menu-item>
          <a-menu-item key="visualization-runtime" @click="go('/visualization/runtime')">
            运行态
          </a-menu-item>
          <a-menu-item key="visualization-governance" @click="go('/visualization/governance')">
            治理中心
          </a-menu-item>
        </a-sub-menu>
        <a-sub-menu v-if="showWorkflowMenu" key="workflow" title="工作流引擎">
          <a-menu-item key="workflow-designer" @click="go('/workflow/designer')">
            工作流设计器
          </a-menu-item>
          <a-menu-item key="workflow-instances" @click="go('/workflow/instances')">
            实例监控
          </a-menu-item>
        </a-sub-menu>
      </a-menu>
    </a-layout-sider>

    <a-layout>
      <a-layout-header class="app-header">
        <div class="header-left">多租户安全支撑平台</div>
        <div class="header-right">
          <a-dropdown trigger="click">
            <a-button type="text">
              <a-space>
                <a-avatar size="small">
                  {{ profileInitials }}
                </a-avatar>
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
      <a-layout-content class="app-content">
        <router-view />
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { getCurrentUser, logout as apiLogout } from "@/services/api";
import type { AuthProfile } from "@/types/api";
import { clearAuthStorage, getAccessToken, getAuthProfile, hasPermission, setAuthProfile } from "@/utils/auth";

const collapsed = ref(false);
const router = useRouter();
const route = useRoute();
const profile = ref<AuthProfile | null>(null);

const isLogin = computed(() => route.name === "login");
const profileDisplayName = computed(() => profile.value?.displayName || profile.value?.username || "个人中心");
const profileInitials = computed(() => {
  const name = profile.value?.displayName || profile.value?.username || "";
  return name.trim().slice(0, 2) || "我";
});

const selectedKeys = computed(() => {
  if (route.path.startsWith("/assets")) return ["assets"];
  if (route.path.startsWith("/audit")) return ["audit"];
  if (route.path.startsWith("/alert")) return ["alert"];
  if (route.path.startsWith("/approval")) return ["approval"];
  if (route.path.startsWith("/system/users")) return ["system-users"];
  if (route.path.startsWith("/system/roles")) return ["system-roles"];
  if (route.path.startsWith("/system/permissions")) return ["system-permissions"];
  if (route.path.startsWith("/system/menus")) return ["system-menus"];
  if (route.path.startsWith("/system/departments")) return ["system-departments"];
  if (route.path.startsWith("/system/positions")) return ["system-positions"];
  if (route.path.startsWith("/visualization")) {
    if (route.path.includes("designer")) return ["visualization-designer"];
    if (route.path.includes("runtime")) return ["visualization-runtime"];
    if (route.path.includes("governance")) return ["visualization-governance"];
    return ["visualization-center"];
  }
  if (route.path === "/workflow/designer") return ["workflow-designer"];
  if (route.path === "/workflow/instances") return ["workflow-instances"];
  return ["home"];
});

const toggle = (value: boolean) => {
  collapsed.value = value;
};

const go = (path: string) => {
  router.push(path);
};

const showWorkflowMenu = computed(() => hasPermission(profile.value, "workflow:design"));
const showUsersMenu = computed(() => hasPermission(profile.value, "users:view"));
const showRolesMenu = computed(() => hasPermission(profile.value, "roles:view"));
const showPermissionsMenu = computed(() => hasPermission(profile.value, "permissions:view"));
const showMenusMenu = computed(() => hasPermission(profile.value, "menus:view"));
const showDepartmentsMenu = computed(() => hasPermission(profile.value, "departments:view"));
const showPositionsMenu = computed(() => hasPermission(profile.value, "positions:view"));
const showSystemMenu = computed(
  () =>
    showUsersMenu.value ||
    showRolesMenu.value ||
    showPermissionsMenu.value ||
    showMenusMenu.value ||
    showDepartmentsMenu.value ||
    showPositionsMenu.value
);

const loadProfile = async () => {
  const cached = getAuthProfile();
  if (cached) {
    profile.value = cached;
  }

  if (!getAccessToken()) {
    return;
  }

  try {
    const result = await getCurrentUser();
    profile.value = result;
    setAuthProfile(result);
  } catch (error) {
    message.error((error as Error).message || "获取用户信息失败");
  }
};

const openProfile = () => {
  router.push("/profile");
};

const logout = async () => {
  try {
    await apiLogout();
  } catch (error) {
    message.error((error as Error).message || "退出失败");
  } finally {
    clearAuthStorage();
    router.push("/login");
  }
};

onMounted(loadProfile);
</script>
