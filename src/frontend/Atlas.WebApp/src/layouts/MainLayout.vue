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
        @open-change="handleOpenChange"
      >
        <a-menu-item key="home" @click="go('/')">总览</a-menu-item>

        <a-sub-menu v-if="showSecurityMenu" key="security" title="安全运营">
          <a-menu-item v-if="showAssetsMenu" key="assets" @click="go('/assets')">资产</a-menu-item>
          <a-menu-item v-if="showAuditMenu" key="audit" @click="go('/audit')">审计</a-menu-item>
          <a-menu-item v-if="showAlertMenu" key="alert" @click="go('/alert')">告警</a-menu-item>
        </a-sub-menu>

        <a-sub-menu v-if="showApprovalMenu" key="approval" title="审批中心">
          <a-menu-item v-if="showApprovalFlowMenu" key="approval-flows" @click="go('/approval/flows')">审批流</a-menu-item>
          <a-menu-item v-if="showApprovalDesignerMenu" key="approval-designer" @click="go('/approval/designer')">流程设计</a-menu-item>
          <a-menu-item v-if="showApprovalFlowMenu" key="approval-tasks" @click="go('/approval/tasks')">审批任务</a-menu-item>
          <a-menu-item v-if="showApprovalFlowMenu" key="approval-instances" @click="go('/approval/instances')">流程实例</a-menu-item>
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

        <a-sub-menu key="security" title="安全中心">
          <a-menu-item key="assets" @click="go('/assets')">资产管理</a-menu-item>
          <a-menu-item key="alert" @click="go('/alert')">告警管理</a-menu-item>
          <a-menu-item key="audit" @click="go('/audit')">审计日志</a-menu-item>
        </a-sub-menu>

        <a-sub-menu key="process" title="流程中心">
          <a-menu-item key="process-flows" @click="go('/process/flows')">流程定义</a-menu-item>
          <a-menu-item key="process-tasks" @click="go('/process/tasks')">我的待办</a-menu-item>
          <a-menu-item key="process-instances" @click="go('/process/instances')">我发起的</a-menu-item>
          <a-menu-item key="process-monitor" @click="go('/process/monitor')">流程监控</a-menu-item>
        </a-sub-menu>

        <a-sub-menu key="apps" title="应用中心">
          <a-menu-item key="apps-list" @click="go('/apps/list')">应用管理</a-menu-item>
          <a-menu-item key="apps-forms" @click="go('/apps/forms')">表单管理</a-menu-item>
          <a-menu-item key="apps-data-model" @click="go('/apps/data-model')">数据模型</a-menu-item>
        </a-sub-menu>

        <a-sub-menu v-if="showAmisMenu" key="amis" title="AMIS 管理">
          <a-menu-item v-if="showUsersMenu" key="amis-users" @click="go('/amis/system/users')">
            员工管理
          </a-menu-item>
          <a-menu-item v-if="showDepartmentsMenu" key="amis-departments" @click="go('/amis/system/departments')">
            部门管理
          </a-menu-item>
          <a-menu-item v-if="showPositionsMenu" key="amis-positions" @click="go('/amis/system/positions')">
            职位管理
          </a-menu-item>
          <a-menu-item v-if="showRolesMenu" key="amis-roles" @click="go('/amis/system/roles')">
            角色管理
          </a-menu-item>
          <a-menu-item v-if="showPermissionsMenu" key="amis-permissions" @click="go('/amis/system/permissions')">
            权限管理
          </a-menu-item>
          <a-menu-item v-if="showMenusMenu" key="amis-menus" @click="go('/amis/system/menus')">
            菜单管理
          </a-menu-item>
          <a-menu-item v-if="showProjectsMenu" key="amis-projects" @click="go('/amis/system/projects')">
            项目管理
          </a-menu-item>
          <a-menu-item v-if="showAppsMenu" key="amis-apps" @click="go('/amis/system/apps')">
            应用管理
          </a-menu-item>
        </a-sub-menu>

        <a-menu-item v-if="showNotificationMenu" key="system-notifications" @click="go('/system/notifications')">通知中心</a-menu-item>

        <a-sub-menu v-if="showVisualizationMenu" key="visualization" title="可视化中心">
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
          <a-menu-item key="settings-messages" @click="go('/settings/messages')">
            消息管理
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
          <a-auto-complete
            v-model:value="globalKeyword"
            class="global-search"
            :options="searchOptions"
            placeholder="搜索功能入口（支持拼音）"
            allow-clear
            @select="handleSearchSelect"
          />
          <a-button type="text" class="header-help" @click="openHelp">帮助</a-button>
          <a-dropdown trigger="click" :overlay-style="{ minWidth: '100px' }">
            <a-button type="text" class="header-lang">
              {{ currentLocale === 'zh-CN' ? '中文' : 'English' }}
            </a-button>
            <template #overlay>
              <a-menu @click="handleLocaleChange">
                <a-menu-item key="zh-CN">中文</a-menu-item>
                <a-menu-item key="en-US">English</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
          <NotificationBell />
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
        <a-breadcrumb v-if="breadcrumbItems.length > 0" class="app-breadcrumb">
          <a-breadcrumb-item v-for="(item, index) in breadcrumbItems" :key="index">
            <router-link v-if="item.path" :to="item.path">{{ item.title }}</router-link>
            <span v-else>{{ item.title }}</span>
          </a-breadcrumb-item>
        </a-breadcrumb>
        <router-view :key="contentKey" />
      </a-layout-content>
    </a-layout>

    <!-- AI 助手 Drawer -->
    <a-drawer
      v-model:open="aiDrawerOpen"
      title="AI 助手"
      placement="right"
      :width="480"
      destroy-on-close
    >
      <component :is="AiAssistantContent" v-if="aiDrawerOpen" />
    </a-drawer>
  </a-layout>
</template>

<script setup lang="ts">
import { computed, defineAsyncComponent, onMounted, onUnmounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { HomeOutlined, GlobalOutlined, RobotOutlined } from "@ant-design/icons-vue";
import { useI18n } from "vue-i18n";
import { saveLocale, type SupportedLocale } from "@/i18n";
import { getCurrentUser, logout as apiLogout } from "@/services/api";
import type { AuthProfile } from "@/types/api";
import type { BreadcrumbItem } from "@/router";
import { clearAuthStorage, getAccessToken, getAuthProfile, hasPermission, setAuthProfile } from "@/utils/auth";
import ProjectSwitcher from "@/components/ProjectSwitcher.vue";
import NotificationBell from "@/components/layout/NotificationBell.vue";
import { setLocale, getLocale } from "@/i18n";

const AiAssistantContent = defineAsyncComponent(() => import("@/pages/lowcode/AiAssistantPage.vue"));

const { locale } = useI18n();
const collapsed = ref(false);
const router = useRouter();
const route = useRoute();
const profile = ref<AuthProfile | null>(null);
const contentKey = ref(0);
const openKeys = ref<string[]>([]);
const globalKeyword = ref("");
const aiDrawerOpen = ref(false);

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

// ── 菜单选中 & 展开 ──
const selectedKeys = computed(() => {
  if (route.path.startsWith("/assets")) return ["assets"];
  if (route.path.startsWith("/audit")) return ["audit"];
  if (route.path.startsWith("/alert")) return ["alert"];
  if (route.path.startsWith("/approval/flows")) return ["approval-flows"];
  if (route.path.startsWith("/approval/designer")) return ["approval-designer"];
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
  if (route.path.startsWith("/amis/system/users")) return ["amis-users"];
  if (route.path.startsWith("/amis/system/departments")) return ["amis-departments"];
  if (route.path.startsWith("/amis/system/positions")) return ["amis-positions"];
  if (route.path.startsWith("/amis/system/roles")) return ["amis-roles"];
  if (route.path.startsWith("/amis/system/permissions")) return ["amis-permissions"];
  if (route.path.startsWith("/amis/system/menus")) return ["amis-menus"];
  if (route.path.startsWith("/amis/system/projects")) return ["amis-projects"];
  if (route.path.startsWith("/amis/system/apps")) return ["amis-apps"];
  if (route.path.startsWith("/system/notifications")) return ["system-notifications"];
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

const toggleAiDrawer = () => {
  aiDrawerOpen.value = !aiDrawerOpen.value;
};

const showWorkflowMenu = computed(() => hasPermission(profile.value, "workflow:design"));
const showAssetsMenu = computed(() => hasPermission(profile.value, "assets:view"));
const showAuditMenu = computed(() => hasPermission(profile.value, "audit:view"));
const showAlertMenu = computed(() => hasPermission(profile.value, "alert:view"));
const showApprovalFlowMenu = computed(() => hasPermission(profile.value, "approval:flow:view"));
const showApprovalDesignerMenu = computed(() => hasPermission(profile.value, "approval:flow:create"));
const showUsersMenu = computed(() => hasPermission(profile.value, "users:view"));
const showRolesMenu = computed(() => hasPermission(profile.value, "roles:view"));
const showPermissionsMenu = computed(() => hasPermission(profile.value, "permissions:view"));
const showMenusMenu = computed(() => hasPermission(profile.value, "menus:view"));
const showDepartmentsMenu = computed(() => hasPermission(profile.value, "departments:view"));
const showPositionsMenu = computed(() => hasPermission(profile.value, "positions:view"));
const showAppsMenu = computed(() => hasPermission(profile.value, "apps:view"));
const showProjectsMenu = computed(() => hasPermission(profile.value, "projects:view"));
const showNotificationMenu = computed(() => hasPermission(profile.value, "notification:view"));
const showVisualizationMenu = computed(
  () => hasPermission(profile.value, "visualization:view") || hasPermission(profile.value, "visualization:process:save")
);
const showSecurityMenu = computed(
  () => showAssetsMenu.value || showAuditMenu.value || showAlertMenu.value
);
const showApprovalMenu = computed(
  () => showApprovalFlowMenu.value || showApprovalDesignerMenu.value
);
const showOrganizationMenu = computed(
  () => showUsersMenu.value || showDepartmentsMenu.value || showPositionsMenu.value
);
const showAuthGroup = computed(
  () => showRolesMenu.value || showPermissionsMenu.value || showMenusMenu.value
);
const showBusinessMenu = computed(() => showProjectsMenu.value);
const showApplicationMenu = computed(() => showAppsMenu.value);
const showAmisMenu = computed(
  () =>
    showUsersMenu.value ||
    showDepartmentsMenu.value ||
    showPositionsMenu.value ||
    showRolesMenu.value ||
    showPermissionsMenu.value ||
    showMenusMenu.value ||
    showProjectsMenu.value ||
    showAppsMenu.value
);

const handleOpenChange = (keys: string[]) => {
  openKeys.value = keys;
};

const resolveOpenKeys = (): string[] => {
  const group = route.meta.menuGroup as string | undefined;
  return group ? [group] : [];
};

// ── 全局搜索 ──
const searchIndex = [
  { label: "工作台", keywords: ["工作台", "首页", "gzt", "sy"], path: "/" },
  { label: "资产管理", keywords: ["资产", "zc", "asset"], path: "/assets" },
  { label: "告警管理", keywords: ["告警", "gj", "alert"], path: "/alert" },
  { label: "审计日志", keywords: ["审计", "sj", "audit"], path: "/audit" },
  { label: "流程定义", keywords: ["流程", "审批", "lc", "sp"], path: "/process/flows" },
  { label: "我的待办", keywords: ["待办", "任务", "db", "rw"], path: "/process/tasks" },
  { label: "我发起的", keywords: ["发起", "实例", "fq"], path: "/process/instances" },
  { label: "流程监控", keywords: ["监控", "jk"], path: "/process/monitor" },
  { label: "应用管理", keywords: ["应用", "yy", "app"], path: "/apps/list" },
  { label: "表单管理", keywords: ["表单", "bd", "form"], path: "/apps/forms" },
  { label: "数据模型", keywords: ["数据模型", "sjmx", "model"], path: "/apps/data-model" },
  { label: "员工管理", keywords: ["员工", "用户", "人员", "yg"], path: "/settings/org/users" },
  { label: "部门管理", keywords: ["部门", "组织", "bm"], path: "/settings/org/departments" },
  { label: "职位管理", keywords: ["职位", "岗位", "zw"], path: "/settings/org/positions" },
  { label: "角色管理", keywords: ["角色", "js"], path: "/settings/auth/roles" },
  { label: "权限管理", keywords: ["权限", "qx"], path: "/settings/auth/permissions" },
  { label: "菜单管理", keywords: ["菜单", "cd"], path: "/settings/auth/menus" },
  { label: "项目管理", keywords: ["项目", "xm"], path: "/settings/projects" },
  { label: "应用配置", keywords: ["应用配置", "yyp"], path: "/settings/apps" },
  { label: "消息管理", keywords: ["消息", "通知", "xx"], path: "/settings/messages" },
  { label: "AI 助手", keywords: ["ai", "智能", "助手"], path: "/ai" },
];

const searchOptions = computed(() => {
  const kw = globalKeyword.value.trim().toLowerCase();
  if (!kw) return [];
  return searchIndex
    .filter((item) =>
      item.keywords.some((k) => k.includes(kw)) || item.label.toLowerCase().includes(kw)
    )
    .map((item) => ({ value: item.path, label: item.label }));
});

const handleSearchSelect = (path: string) => {
  router.push(path);
  globalKeyword.value = "";
};

const currentLocale = ref(getLocale());
const handleLocaleChange = ({ key }: { key: string }) => {
  setLocale(key as "zh-CN" | "en-US");
  currentLocale.value = key;
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
  openKeys.value = resolveOpenKeys();
});

onUnmounted(() => {
  window.removeEventListener("project-changed", onProjectChanged);
});

watch(
  () => route.path,
  () => {
    openKeys.value = resolveOpenKeys();
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

.app-breadcrumb {
  margin-bottom: 16px;
}

/* ── Fullscreen mode ── */
.app-fullscreen {
  width: 100vw;
  height: 100vh;
  overflow: hidden;
}
</style>
