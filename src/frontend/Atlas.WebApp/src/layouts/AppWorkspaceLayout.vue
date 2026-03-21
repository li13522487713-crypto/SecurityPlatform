<template>
  <div data-testid="e2e-app-workspace-layout">
    <a-layout class="workspace-layout">
      <div data-testid="e2e-app-workspace-sider">
        <a-layout-sider theme="light" :width="220" class="workspace-sider">
      <div class="sider-title">
        <div class="app-name" :title="appName" data-testid="e2e-app-workspace-name">{{ appName }}</div>
        <span data-testid="e2e-app-workspace-back-console">
          <a-button type="link" size="small" @click="goConsole">
            Back to console
          </a-button>
        </span>
      </div>
      <div data-testid="e2e-app-workspace-menu">
        <a-menu mode="inline" :selected-keys="selectedKeys" @click="onMenuClick">
          <a-menu-item :key="dashboardPath">
            <span data-testid="e2e-app-workspace-menu-dashboard">Dashboard</span>
          </a-menu-item>
          <a-menu-item :key="builderPath">
            <span data-testid="e2e-app-workspace-menu-builder">Builder</span>
          </a-menu-item>
          <a-menu-item :key="pagesPath">
            <span data-testid="e2e-app-workspace-menu-pages">Pages</span>
          </a-menu-item>
          <a-menu-item :key="formsPath">
            <span data-testid="e2e-app-workspace-menu-forms">Forms</span>
          </a-menu-item>
          <a-menu-item :key="flowsPath">
            <span data-testid="e2e-app-workspace-menu-flows">Flows</span>
          </a-menu-item>
          <a-menu-item :key="agentsPath">
            <span data-testid="e2e-app-workspace-menu-agents">Agents</span>
          </a-menu-item>
          <a-menu-item :key="workflowsPath">
            <span data-testid="e2e-app-workspace-menu-workflows">Workflows</span>
          </a-menu-item>
          <a-menu-item :key="promptsPath">
            <span data-testid="e2e-app-workspace-menu-prompts">Prompts</span>
          </a-menu-item>
          <a-menu-item :key="pluginsPath">
            <span data-testid="e2e-app-workspace-menu-plugins">Plugins</span>
          </a-menu-item>
          <a-menu-item :key="dataPath">
            <span data-testid="e2e-app-workspace-menu-data">Data</span>
          </a-menu-item>
          <a-menu-item :key="usersPath">
            <span data-testid="e2e-app-workspace-menu-users">Users</span>
          </a-menu-item>
          <a-menu-item :key="rolesPath">
            <span data-testid="e2e-app-workspace-menu-roles">Roles</span>
          </a-menu-item>
          <a-menu-item :key="runtimeHomePath">
            <span data-testid="e2e-app-workspace-menu-runtime">Runtime</span>
          </a-menu-item>
          <a-menu-item :key="settingsPath">
            <span data-testid="e2e-app-workspace-menu-settings">Settings</span>
          </a-menu-item>
        </a-menu>
      </div>
        </a-layout-sider>
      </div>

      <a-layout>
        <div data-testid="e2e-app-workspace-header">
          <a-layout-header class="workspace-header">
            <div class="header-left">
              <span>Workspace</span>
              <UnifiedContextBar />
            </div>
            <div class="header-right" data-testid="e2e-app-workspace-header-actions">
              <NotificationBell />
              <a-dropdown trigger="click">
                <span data-testid="e2e-app-workspace-user-menu-trigger">
                  <a-button type="text">
                    <a-space>
                      <a-avatar size="small">{{ profileInitials }}</a-avatar>
                      <span>{{ profileDisplayName }}</span>
                    </a-space>
                  </a-button>
                </span>
                <template #overlay>
                  <div data-testid="e2e-app-workspace-user-menu">
                    <a-menu>
                      <a-menu-item key="profile" @click="go('/profile')">
                        <span data-testid="e2e-app-workspace-user-menu-profile">Profile</span>
                      </a-menu-item>
                      <a-menu-divider />
                      <a-menu-item key="logout" @click="logout">
                        <span data-testid="e2e-app-workspace-user-menu-logout">Logout</span>
                      </a-menu-item>
                    </a-menu>
                  </div>
                </template>
              </a-dropdown>
            </div>
          </a-layout-header>
        </div>
        <div data-testid="e2e-app-workspace-content">
          <a-layout-content class="workspace-content">
            <router-view />
          </a-layout-content>
        </div>
      </a-layout>
    </a-layout>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, watch, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import NotificationBell from "@/components/layout/NotificationBell.vue";
import UnifiedContextBar from "@/components/context/UnifiedContextBar.vue";
import { getTenantAppInstanceDetail } from "@/services/api-tenant-app-instances";
import { usePermissionStore } from "@/stores/permission";
import { useTagsViewStore } from "@/stores/tagsView";
import { useUserStore } from "@/stores/user";

const route = useRoute();
const router = useRouter();
const userStore = useUserStore();
const permissionStore = usePermissionStore();
const tagsViewStore = useTagsViewStore();

const appName = computed(() => {
  const metaName = route.meta?.title;
  if (typeof metaName === "string" && metaName.trim()) {
    return metaName;
  }
  return "App";
});

const appId = computed(() => String(route.params.appId ?? ""));
const dashboardPath = computed(() => `/apps/${appId.value}/dashboard`);
const builderPath = computed(() => `/apps/${appId.value}/builder`);
const pagesPath = computed(() => `/apps/${appId.value}/pages`);
const formsPath = computed(() => `/apps/${appId.value}/forms`);
const flowsPath = computed(() => `/apps/${appId.value}/flows`);
const agentsPath = computed(() => `/apps/${appId.value}/agents`);
const workflowsPath = computed(() => `/apps/${appId.value}/workflows`);
const promptsPath = computed(() => `/apps/${appId.value}/prompts`);
const pluginsPath = computed(() => `/apps/${appId.value}/plugins`);
const dataPath = computed(() => `/apps/${appId.value}/data`);
const usersPath = computed(() => `/apps/${appId.value}/users`);
const rolesPath = computed(() => `/apps/${appId.value}/roles`);
const runtimeHomePath = computed(() => `/apps/${appId.value}/run/home`);
const settingsPath = computed(() => `/apps/${appId.value}/settings`);

const selectedKeys = computed(() => {
  if (route.path.startsWith(builderPath.value)) {
    return [builderPath.value];
  }
  if (route.path.startsWith(pagesPath.value)) {
    return [pagesPath.value];
  }
  if (route.path.startsWith(formsPath.value)) {
    return [formsPath.value];
  }
  if (route.path.startsWith(flowsPath.value)) {
    return [flowsPath.value];
  }
  if (route.path.startsWith(agentsPath.value)) {
    return [agentsPath.value];
  }
  if (route.path.startsWith(workflowsPath.value)) {
    return [workflowsPath.value];
  }
  if (route.path.startsWith(promptsPath.value)) {
    return [promptsPath.value];
  }
  if (route.path.startsWith(pluginsPath.value)) {
    return [pluginsPath.value];
  }
  if (route.path.startsWith(dataPath.value)) {
    return [dataPath.value];
  }
  if (route.path.startsWith(usersPath.value)) {
    return [usersPath.value];
  }
  if (route.path.startsWith(rolesPath.value)) {
    return [rolesPath.value];
  }
  if (route.path.startsWith(`/apps/${appId.value}/run/`)) {
    return [runtimeHomePath.value];
  }
  if (route.path.startsWith(settingsPath.value)) {
    return [settingsPath.value];
  }
  return [dashboardPath.value];
});

const profileDisplayName = computed(() => userStore.profile?.displayName || userStore.profile?.username || "Profile");
const profileInitials = computed(() => profileDisplayName.value.slice(0, 2));

function onMenuClick(info: { key: string }) {
  go(info.key);
}

function go(path: string) {
  router.push(path);
}

function goConsole() {
  router.push("/console");
}

async function logout() {
  await userStore.logout();

  if (!isMounted.value) return;
  permissionStore.reset();
  tagsViewStore.delAllViews();
  router.push("/login");
}

async function syncTitle() {
  if (!appId.value) {
    return;
  }

  try {
    const detail  = await getTenantAppInstanceDetail(appId.value);

    if (!isMounted.value) return;
    if (detail?.name) {
      document.title = `${detail.name} - Workspace - Atlas Security Platform`;
    }
  } catch {
    // ignore
  }
}

onMounted(syncTitle);
watch(appId, () => {
  void syncTitle();
});
</script>

<style scoped>
.workspace-layout {
  min-height: 100vh;
}

.workspace-sider {
  border-right: 1px solid var(--color-border);
}

.sider-title {
  padding: 12px;
  border-bottom: 1px solid var(--color-border);
}

.app-name {
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 6px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.workspace-header {
  height: var(--header-height);
  line-height: var(--header-height);
  padding: 0 var(--spacing-md);
  background: var(--color-bg-container);
  border-bottom: 1px solid var(--color-border);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.workspace-content {
  margin: var(--spacing-md);
}
</style>
