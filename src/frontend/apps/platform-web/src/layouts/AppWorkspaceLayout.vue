<template>
  <a-layout class="app-workspace-layout">
    <a-layout-sider
      v-model:collapsed="collapsed"
      :width="220"
      :collapsed-width="64"
      collapsible
      theme="light"
      class="app-sider"
    >
      <div class="app-brand" :title="appName">
        <span v-if="!collapsed" class="app-brand-name">{{ appName }}</span>
        <span v-else class="app-brand-icon">{{ appName.charAt(0) }}</span>
      </div>

      <a-menu
        mode="inline"
        theme="light"
        :selected-keys="selectedKeys"
        @click="onMenuClick"
      >
        <a-menu-item key="dashboard">
          <template #icon><DashboardOutlined /></template>
          {{ t("appWorkspace.menuDashboard") }}
        </a-menu-item>
        <a-menu-item key="pages">
          <template #icon><FileOutlined /></template>
          {{ t("appWorkspace.menuPages") }}
        </a-menu-item>
        <a-menu-item key="builder">
          <template #icon><LayoutOutlined /></template>
          {{ t("appWorkspace.menuBuilder") }}
        </a-menu-item>
        <a-menu-item key="data">
          <template #icon><DatabaseOutlined /></template>
          {{ t("appWorkspace.menuData") }}
        </a-menu-item>
        <a-menu-item key="workflows">
          <template #icon><ApartmentOutlined /></template>
          {{ t("appWorkspace.menuWorkflows") }}
        </a-menu-item>
        <a-menu-item key="flows">
          <template #icon><AuditOutlined /></template>
          {{ t("appWorkspace.menuApprovalFlows") }}
        </a-menu-item>
        <a-menu-item key="settings">
          <template #icon><SettingOutlined /></template>
          {{ t("appWorkspace.menuSettings") }}
        </a-menu-item>
      </a-menu>
    </a-layout-sider>

    <a-layout>
      <a-layout-header class="app-header">
        <a-breadcrumb>
          <a-breadcrumb-item>
            <a @click.prevent="router.push('/console')">{{ t("appWorkspace.consoleHome") }}</a>
          </a-breadcrumb-item>
          <a-breadcrumb-item>{{ appName }}</a-breadcrumb-item>
          <a-breadcrumb-item v-if="currentSection">{{ currentSection }}</a-breadcrumb-item>
        </a-breadcrumb>
      </a-layout-header>

      <a-layout-content class="app-content">
        <router-view />
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import {
  DashboardOutlined,
  FileOutlined,
  LayoutOutlined,
  DatabaseOutlined,
  ApartmentOutlined,
  AuditOutlined,
  SettingOutlined
} from "@ant-design/icons-vue";
import { getTenantAppInstanceDetail } from "@/services/api-console";
import { useUserStore } from "@/stores/user";
import { provideCapabilityHostContext } from "@atlas/shared-kernel/context";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const userStore = useUserStore();
const collapsed = ref(false);
const appName = ref("...");
const appKey = ref("");
const isMounted = ref(false);

const appId = computed(() => String(route.params.appId ?? ""));

const capabilityHostContext = provideCapabilityHostContext({
  hostMode: "platform",
  tenantId: userStore.profile?.tenantId,
  appId: appId.value || undefined,
  appKey: undefined,
  appInstanceId: appId.value || undefined,
  permissionSet: userStore.permissions
});

const menuPathMap: Record<string, string> = {
  dashboard: "dashboard",
  pages: "pages",
  builder: "builder",
  data: "data",
  workflows: "workflows",
  flows: "flows",
  settings: "settings"
};

const selectedKeys = computed(() => {
  const path = route.path;
  for (const [key, segment] of Object.entries(menuPathMap)) {
    if (path.includes(`/apps/${appId.value}/${segment}`)) {
      return [key];
    }
  }
  return ["dashboard"];
});

const sectionLabelMap = computed<Record<string, string>>(() => ({
  dashboard: t("appWorkspace.menuDashboard"),
  pages: t("appWorkspace.menuPages"),
  builder: t("appWorkspace.menuBuilder"),
  data: t("appWorkspace.menuData"),
  workflows: t("appWorkspace.menuWorkflows"),
  flows: t("appWorkspace.menuApprovalFlows"),
  settings: t("appWorkspace.menuSettings")
}));

const currentSection = computed(() => {
  const key = selectedKeys.value[0];
  return key ? sectionLabelMap.value[key] : "";
});

function onMenuClick({ key }: { key: string }) {
  const segment = menuPathMap[key] ?? key;
  void router.push(`/apps/${appId.value}/${segment}`);
}

async function loadAppInfo() {
  if (!appId.value) return;
  try {
    const detail = await getTenantAppInstanceDetail(appId.value);
    if (!isMounted.value) return;
    appKey.value = detail.appKey || "";
    appName.value = detail.name || t("appWorkspace.defaultAppName");
  } catch {
    appKey.value = "";
    appName.value = t("appWorkspace.defaultAppName");
  }
}

watch(
  [appId, appKey, () => userStore.profile?.tenantId, () => userStore.permissions],
  () => {
    capabilityHostContext.patchContext({
      hostMode: "platform",
      tenantId: userStore.profile?.tenantId,
      appId: appId.value || undefined,
      appKey: appKey.value || undefined,
      appInstanceId: appId.value || undefined,
      permissionSet: userStore.permissions
    });
  },
  { immediate: true }
);

onMounted(() => {
  isMounted.value = true;
  void loadAppInfo();
});

onUnmounted(() => {
  isMounted.value = false;
});

watch(appId, () => {
  void loadAppInfo();
});
</script>

<style scoped>
.app-workspace-layout {
  height: calc(100vh - 56px);
}

.app-sider {
  border-right: 1px solid #f0f0f0;
}

.app-brand {
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-bottom: 1px solid #f0f0f0;
  font-weight: 600;
  font-size: 15px;
  overflow: hidden;
  white-space: nowrap;
  padding: 0 12px;
}

.app-brand-name {
  overflow: hidden;
  text-overflow: ellipsis;
}

.app-brand-icon {
  font-size: 18px;
  font-weight: 700;
}

.app-header {
  background: #fff;
  padding: 0 24px;
  height: 48px;
  line-height: 48px;
  border-bottom: 1px solid #f0f0f0;
}

.app-content {
  padding: 16px;
  overflow: auto;
}
</style>
