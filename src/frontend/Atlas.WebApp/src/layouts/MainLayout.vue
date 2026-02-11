<template>
  <router-view v-if="isLogin" />
  <!-- 全屏模式：无 Sider 无 Header，页面自行管理工具栏 -->
  <div v-else-if="isFullscreen" class="app-fullscreen">
    <router-view :key="contentKey" />
  </div>
  <a-layout v-else class="app-shell">
    <a-layout-sider collapsible :collapsed="collapsed" @collapse="toggle">
      <div class="brand">Atlas 安全平台</div>
      <a-menu
        theme="dark"
        mode="inline"
        :selected-keys="selectedKeys"
        :open-keys="openKeys"
        @openChange="handleOpenChange"
      >
        <a-menu-item key="home" @click="go('/')">总览</a-menu-item>

        <a-sub-menu key="security" title="安全运营">
          <a-menu-item key="assets" @click="go('/assets')">资产</a-menu-item>
          <a-menu-item key="audit" @click="go('/audit')">审计</a-menu-item>
          <a-menu-item key="alert" @click="go('/alert')">告警</a-menu-item>
        </a-sub-menu>

        <a-sub-menu key="approval" title="审批中心">
          <a-menu-item key="approval-flows" @click="go('/approval/flows')">审批流</a-menu-item>
          <a-menu-item key="approval-tasks" @click="go('/approval/tasks')">审批任务</a-menu-item>
          <a-menu-item key="approval-instances" @click="go('/approval/instances')">流程实例</a-menu-item>
        </a-sub-menu>

        <a-sub-menu v-if="showOrganizationMenu" key="organization" title="组织管理">
          <a-menu-item v-if="showUsersMenu" key="org-users" @click="go('/system/users')">
            员工管理
          </a-menu-item>
          <a-menu-item v-if="showDepartmentsMenu" key="org-departments" @click="go('/system/departments')">
            部门管理
          </a-menu-item>
          <a-menu-item v-if="showPositionsMenu" key="org-positions" @click="go('/system/positions')">
            职位管理
          </a-menu-item>
        </a-sub-menu>

        <a-sub-menu v-if="showPermissionMenu" key="permission" title="权限管理">
          <a-menu-item v-if="showRolesMenu" key="permission-roles" @click="go('/system/roles')">
            角色管理
          </a-menu-item>
          <a-menu-item v-if="showPermissionsMenu" key="permission-permissions" @click="go('/system/permissions')">
            权限管理
          </a-menu-item>
          <a-menu-item v-if="showMenusMenu" key="permission-menus" @click="go('/system/menus')">
            菜单管理
          </a-menu-item>
        </a-sub-menu>

        <a-sub-menu v-if="showBusinessMenu" key="business" title="业务管理">
          <a-menu-item v-if="showProjectsMenu" key="business-projects" @click="go('/system/projects')">
            项目管理
          </a-menu-item>
        </a-sub-menu>

        <a-sub-menu v-if="showApplicationMenu" key="application" title="应用管理">
          <a-menu-item v-if="showAppsMenu" key="application-apps" @click="go('/system/apps')">
            应用配置
          </a-menu-item>
        </a-sub-menu>

        <a-sub-menu key="lowcode" title="低代码平台">
          <a-menu-item key="lowcode-forms" @click="go('/lowcode/forms')">
            表单管理
          </a-menu-item>
          <a-menu-item key="lowcode-apps" @click="go('/lowcode/apps')">
            应用管理
          </a-menu-item>
          <a-menu-item key="lowcode-dynamic-tables" @click="go('/dynamic-tables')">
            数据模型
          </a-menu-item>
          <a-menu-item key="lowcode-reports" @click="go('/lowcode/reports')">
            报表中心
          </a-menu-item>
          <a-menu-item key="lowcode-dashboards" @click="go('/lowcode/dashboards')">
            仪表盘
          </a-menu-item>
          <a-menu-item key="lowcode-messages" @click="go('/lowcode/messages')">
            消息中心
          </a-menu-item>
          <a-menu-item key="lowcode-ai" @click="go('/lowcode/ai')">
            AI 助手
          </a-menu-item>
          <a-menu-item key="lowcode-process-monitor" @click="go('/lowcode/process-monitor')">
            流程监控
          </a-menu-item>
        </a-sub-menu>

        <a-sub-menu key="amis" title="AMIS 管理">
          <a-menu-item key="amis-users" @click="go('/amis/system/users')">
            员工管理
          </a-menu-item>
          <a-menu-item key="amis-departments" @click="go('/amis/system/departments')">
            部门管理
          </a-menu-item>
          <a-menu-item key="amis-positions" @click="go('/amis/system/positions')">
            职位管理
          </a-menu-item>
          <a-menu-item key="amis-roles" @click="go('/amis/system/roles')">
            角色管理
          </a-menu-item>
          <a-menu-item key="amis-permissions" @click="go('/amis/system/permissions')">
            权限管理
          </a-menu-item>
          <a-menu-item key="amis-menus" @click="go('/amis/system/menus')">
            菜单管理
          </a-menu-item>
          <a-menu-item key="amis-projects" @click="go('/amis/system/projects')">
            项目管理
          </a-menu-item>
          <a-menu-item key="amis-apps" @click="go('/amis/system/apps')">
            应用管理
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
        <div class="header-left">
          <span class="header-title">多租户安全支撑平台</span>
          <ProjectSwitcher />
        </div>
        <div class="header-right">
          <a-input-search
            v-model:value="globalKeyword"
            class="global-search"
            placeholder="搜索员工、角色、项目或功能入口"
            allow-clear
            @search="handleGlobalSearch"
          />
          <a-dropdown trigger="click">
            <a-button type="text" class="header-locale">
              <GlobalOutlined /> {{ currentLocaleName }}
            </a-button>
            <template #overlay>
              <a-menu @click="switchLocale">
                <a-menu-item key="zh-CN">简体中文</a-menu-item>
                <a-menu-item key="en-US">English</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
          <a-button type="text" class="header-help" @click="openHelp">帮助</a-button>
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
        <router-view :key="contentKey" />
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { LeftOutlined, GlobalOutlined } from "@ant-design/icons-vue";
import { useI18n } from "vue-i18n";
import { saveLocale, type SupportedLocale } from "@/i18n";
import { getCurrentUser, logout as apiLogout } from "@/services/api";
import type { AuthProfile } from "@/types/api";
import { clearAuthStorage, getAccessToken, getAuthProfile, hasPermission, setAuthProfile } from "@/utils/auth";
import ProjectSwitcher from "@/components/ProjectSwitcher.vue";

const { locale } = useI18n();
const collapsed = ref(false);
const router = useRouter();
const route = useRoute();
const profile = ref<AuthProfile | null>(null);
const contentKey = ref(0);
const openKeys = ref<string[]>([]);
const globalKeyword = ref("");

const currentLocaleName = computed(() => locale.value === "zh-CN" ? "中文" : "EN");
const switchLocale = ({ key }: { key: string }) => {
  const newLocale = key as SupportedLocale;
  locale.value = newLocale;
  saveLocale(newLocale);
};

const isLogin = computed(() => route.name === "login");
const isFullscreen = computed(() => !!route.meta.fullscreen);
const profileDisplayName = computed(() => profile.value?.displayName || profile.value?.username || "个人中心");
const profileInitials = computed(() => {
  const name = profile.value?.displayName || profile.value?.username || "";
  return name.trim().slice(0, 2) || "我";
});

const selectedKeys = computed(() => {
  if (route.path.startsWith("/assets")) return ["assets"];
  if (route.path.startsWith("/audit")) return ["audit"];
  if (route.path.startsWith("/alert")) return ["alert"];
  if (route.path.startsWith("/approval/flows") || route.path.startsWith("/approval/designer")) return ["approval-flows"];
  if (route.path.startsWith("/approval/tasks")) return ["approval-tasks"];
  if (route.path.startsWith("/approval/instances")) return ["approval-instances"];
  if (route.path.startsWith("/system/users")) return ["org-users"];
  if (route.path.startsWith("/system/departments")) return ["org-departments"];
  if (route.path.startsWith("/system/positions")) return ["org-positions"];
  if (route.path.startsWith("/system/roles")) return ["permission-roles"];
  if (route.path.startsWith("/system/permissions")) return ["permission-permissions"];
  if (route.path.startsWith("/system/menus")) return ["permission-menus"];
  if (route.path.startsWith("/system/projects")) return ["business-projects"];
  if (route.path.startsWith("/system/apps")) return ["application-apps"];
  if (route.path.startsWith("/lowcode/forms")) return ["lowcode-forms"];
  if (route.path.startsWith("/lowcode/apps")) return ["lowcode-apps"];
  if (route.path.startsWith("/lowcode/reports")) return ["lowcode-reports"];
  if (route.path.startsWith("/lowcode/dashboards")) return ["lowcode-dashboards"];
  if (route.path.startsWith("/lowcode/messages")) return ["lowcode-messages"];
  if (route.path.startsWith("/lowcode/ai")) return ["lowcode-ai"];
  if (route.path.startsWith("/lowcode/process-monitor")) return ["lowcode-process-monitor"];
  if (route.path.startsWith("/dynamic-tables")) return ["lowcode-dynamic-tables"];
  if (route.path.startsWith("/amis/system/users")) return ["amis-users"];
  if (route.path.startsWith("/amis/system/departments")) return ["amis-departments"];
  if (route.path.startsWith("/amis/system/positions")) return ["amis-positions"];
  if (route.path.startsWith("/amis/system/roles")) return ["amis-roles"];
  if (route.path.startsWith("/amis/system/permissions")) return ["amis-permissions"];
  if (route.path.startsWith("/amis/system/menus")) return ["amis-menus"];
  if (route.path.startsWith("/amis/system/projects")) return ["amis-projects"];
  if (route.path.startsWith("/amis/system/apps")) return ["amis-apps"];
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

const goBack = () => {
  if (window.history.length > 1) {
    router.back();
  } else {
    router.push('/');
  }
};

const showWorkflowMenu = computed(() => hasPermission(profile.value, "workflow:design"));
const showUsersMenu = computed(() => hasPermission(profile.value, "users:view"));
const showRolesMenu = computed(() => hasPermission(profile.value, "roles:view"));
const showPermissionsMenu = computed(() => hasPermission(profile.value, "permissions:view"));
const showMenusMenu = computed(() => hasPermission(profile.value, "menus:view"));
const showDepartmentsMenu = computed(() => hasPermission(profile.value, "departments:view"));
const showPositionsMenu = computed(() => hasPermission(profile.value, "positions:view"));
const showAppsMenu = computed(() => hasPermission(profile.value, "apps:view"));
const showProjectsMenu = computed(() => hasPermission(profile.value, "projects:view"));
const showOrganizationMenu = computed(
  () => showUsersMenu.value || showDepartmentsMenu.value || showPositionsMenu.value
);
const showPermissionMenu = computed(
  () => showRolesMenu.value || showPermissionsMenu.value || showMenusMenu.value
);
const showBusinessMenu = computed(() => showProjectsMenu.value);
const showApplicationMenu = computed(() => showAppsMenu.value);

const handleOpenChange = (keys: string[]) => {
  openKeys.value = keys;
};

const resolveOpenKeys = (path: string) => {
  if (path.startsWith("/assets") || path.startsWith("/audit") || path.startsWith("/alert")) {
    return ["security"];
  }
  if (path.startsWith("/approval")) {
    return ["approval"];
  }
  if (path.startsWith("/system/users") || path.startsWith("/system/departments") || path.startsWith("/system/positions")) {
    return ["organization"];
  }
  if (path.startsWith("/system/roles") || path.startsWith("/system/permissions") || path.startsWith("/system/menus")) {
    return ["permission"];
  }
  if (path.startsWith("/system/projects")) {
    return ["business"];
  }
  if (path.startsWith("/system/apps")) {
    return ["application"];
  }
  if (path.startsWith("/lowcode") || path.startsWith("/dynamic-tables")) {
    return ["lowcode"];
  }
  if (path.startsWith("/amis/system")) {
    return ["amis"];
  }
  if (path.startsWith("/visualization")) {
    return ["visualization"];
  }
  if (path.startsWith("/workflow")) {
    return ["workflow"];
  }
  return [];
};

const handleGlobalSearch = (value: string) => {
  const keyword = value.trim();
  if (!keyword) return;
  const map: Array<{ keywords: string[]; path: string }> = [
    { keywords: ["员工", "用户", "人员"], path: "/system/users" },
    { keywords: ["部门", "组织"], path: "/system/departments" },
    { keywords: ["职位", "岗位"], path: "/system/positions" },
    { keywords: ["角色"], path: "/system/roles" },
    { keywords: ["权限"], path: "/system/permissions" },
    { keywords: ["菜单"], path: "/system/menus" },
    { keywords: ["项目"], path: "/system/projects" },
    { keywords: ["应用"], path: "/system/apps" },
    { keywords: ["审计"], path: "/audit" },
    { keywords: ["告警"], path: "/alert" },
    { keywords: ["资产"], path: "/assets" }
  ];
  const matched = map.find((entry) => entry.keywords.some((item) => keyword.includes(item)));
  if (matched) {
    router.push(matched.path);
    return;
  }
  message.info("全局搜索索引建设中");
};

const openHelp = () => {
  message.info("帮助中心建设中");
};

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

const onProjectChanged = () => {
  contentKey.value += 1;
};

onMounted(() => {
  loadProfile();
  window.addEventListener("project-changed", onProjectChanged);
  openKeys.value = resolveOpenKeys(route.path);
});

onUnmounted(() => {
  window.removeEventListener("project-changed", onProjectChanged);
});

watch(
  () => route.path,
  (path) => {
    openKeys.value = resolveOpenKeys(path);
  }
);
</script>

<style scoped>
.header-left {
  display: flex;
  align-items: center;
  gap: var(--spacing-md);
}

.header-title {
  color: var(--color-text-primary);
  font-weight: 500;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.global-search {
  width: 280px;
}

.header-locale {
  color: var(--color-text-primary);
  display: flex;
  align-items: center;
  gap: 4px;
}

.header-help {
  color: var(--color-text-primary);
}

/* ── Fullscreen mode ── */
.app-fullscreen {
  width: 100vw;
  height: 100vh;
  overflow: hidden;
}
</style>
