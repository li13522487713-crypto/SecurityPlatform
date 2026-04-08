<template>
  <aside class="app-sidebar" data-testid="app-sidebar">
    <div class="sidebar-brand" data-testid="app-sidebar-brand">
      <div class="brand-icon">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <polygon points="12 2 2 7 12 12 22 7 12 2" />
          <polyline points="2 17 12 22 22 17" />
          <polyline points="2 12 12 17 22 12" />
        </svg>
      </div>
      <span v-if="!collapsed" class="brand-text">Atlas Console</span>
    </div>

    <nav class="sidebar-nav">
      <div v-for="(group, gi) in navGroups" :key="gi" class="nav-group">
        <h4 v-if="group.title" class="nav-group-title">{{ group.title }}</h4>
        <div class="nav-items">
          <router-link
            v-for="item in group.items"
            :key="item.key"
            :to="item.path"
            custom
            v-slot="{ isActive, navigate }"
          >
            <div
              class="nav-item"
              :data-testid="`app-sidebar-item-${item.key}`"
              :class="{ active: isActive || currentKey === item.key }"
              @click="navigate"
            >
              <component :is="item.icon" class="nav-item-icon" />
              <span class="nav-item-label">{{ item.name }}</span>
              <span v-if="item.badgeText" class="nav-item-badge">{{ item.badgeText }}</span>
            </div>
          </router-link>
        </div>
      </div>
    </nav>

    <div class="sidebar-footer">
      <router-link :to="`/apps/${appKey}/settings`" custom v-slot="{ isActive, navigate }">
        <div
          class="nav-item"
          data-testid="app-sidebar-item-settings"
          :class="{ active: isActive || currentKey === 'settings' }"
          @click="navigate"
        >
          <SettingOutlined class="nav-item-icon" />
          <span class="nav-item-label">{{ t('sidebar.settings') }}</span>
        </div>
      </router-link>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";
import { useI18n } from "vue-i18n";
import {
  DashboardOutlined,
  AppstoreOutlined,
  TeamOutlined,
  SafetyCertificateOutlined,
  ApartmentOutlined,
  IdcardOutlined,
  RobotOutlined,
  ClusterOutlined,
  PartitionOutlined,
  ThunderboltOutlined,
  DatabaseOutlined,
  MessageOutlined,
  ExperimentOutlined,
  FileTextOutlined,
  SettingOutlined,
  ControlOutlined
} from "@ant-design/icons-vue";

const { t } = useI18n();
const route = useRoute();

const props = defineProps<{
  appKey: string;
  collapsed?: boolean;
}>();

const basePath = computed(() => `/apps/${props.appKey}`);

const currentKey = computed(() => {
  const path = route.path;
  const base = basePath.value;
  if (path.startsWith(`${base}/builder`)) return "builder";
  if (path.startsWith(`${base}/roles`)) return "roles";
  if (path.startsWith(`${base}/users`)) return "users";
  if (path.startsWith(`${base}/departments`)) return "departments";
  if (path.startsWith(`${base}/positions`)) return "positions";
  if (path.startsWith(`${base}/agents`) || path.startsWith(`${base}/ai/chat`)) return "agents";
  if (path.startsWith(`${base}/multi-agent`)) return "multi-agent";
  if (path.startsWith(`${base}/workflows`)) return "workflows";
  if (path.startsWith(`${base}/logic-flow`)) return "logic-flow";
  if (path.startsWith(`${base}/knowledge-bases`)) return "knowledge-bases";
  if (path.startsWith(`${base}/prompts`)) return "prompts";
  if (path.startsWith(`${base}/model-configs`)) return "model-configs";
  if (path.startsWith(`${base}/evaluations`)) return "evaluations";
  if (path.startsWith(`${base}/data`)) return "data";
  if (path.startsWith(`${base}/org`)) return "org";
  if (path.startsWith(`${base}/settings`)) return "settings";
  if (path === base || path === `${base}/` || path.startsWith(`${base}/dashboard`)) return "dashboard";
  return "";
});

const navGroups = computed(() => [
  {
    title: "",
    items: [
      { key: "dashboard", name: t("sidebar.overview"), path: `${basePath.value}/dashboard`, icon: DashboardOutlined },
      { key: "builder", name: t("sidebar.appBuilder"), path: `${basePath.value}/builder`, icon: AppstoreOutlined },
      { key: "users", name: t("sidebar.userManagement"), path: `${basePath.value}/users`, icon: TeamOutlined },
      { key: "roles", name: t("sidebar.roleManagement"), path: `${basePath.value}/roles`, icon: SafetyCertificateOutlined },
      { key: "departments", name: t("sidebar.departmentManagement"), path: `${basePath.value}/departments`, icon: ApartmentOutlined },
      { key: "positions", name: t("sidebar.positionManagement"), path: `${basePath.value}/positions`, icon: IdcardOutlined },
    ]
  },
  {
    title: t("sidebar.groupAI"),
    items: [
      { key: "agents", name: t("sidebar.aiAssistant"), path: `${basePath.value}/ai/chat`, icon: RobotOutlined, badgeText: "R1" },
      { key: "multi-agent", name: t("sidebar.multiAgent"), path: `${basePath.value}/multi-agent`, icon: ClusterOutlined },
      { key: "workflows", name: t("sidebar.workflows"), path: `${basePath.value}/workflows`, icon: PartitionOutlined },
      { key: "logic-flow", name: t("sidebar.logicFlow"), path: `${basePath.value}/logic-flow`, icon: ThunderboltOutlined },
    ]
  },
  {
    title: t("sidebar.groupKnowledge"),
    items: [
      { key: "knowledge-bases", name: t("sidebar.knowledgeBases"), path: `${basePath.value}/knowledge-bases`, icon: DatabaseOutlined },
      { key: "prompts", name: t("sidebar.prompts"), path: `${basePath.value}/prompts`, icon: MessageOutlined },
      { key: "model-configs", name: t("sidebar.modelConfigs"), path: `${basePath.value}/model-configs`, icon: ControlOutlined },
      { key: "evaluations", name: t("sidebar.evaluations"), path: `${basePath.value}/evaluations`, icon: ExperimentOutlined },
      { key: "data", name: t("sidebar.formsData"), path: `${basePath.value}/data`, icon: FileTextOutlined },
    ]
  }
]);
</script>

<style scoped>
.app-sidebar {
  width: 256px;
  min-width: 256px;
  background: #fff;
  border-right: 1px solid #f3f4f6;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  height: 100%;
  overflow: hidden;
}

.sidebar-brand {
  height: 64px;
  display: flex;
  align-items: center;
  padding: 0 24px;
  gap: 12px;
  flex-shrink: 0;
}

.brand-icon {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  background: #4f46e5;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  flex-shrink: 0;
}

.brand-text {
  font-size: 20px;
  font-weight: 700;
  color: #111827;
  letter-spacing: -0.025em;
  white-space: nowrap;
}

.sidebar-nav {
  flex: 1;
  padding: 16px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.nav-group-title {
  padding: 0 12px;
  margin: 0 0 8px 0;
  font-size: 11px;
  font-weight: 600;
  color: #9ca3af;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.nav-items {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  border-radius: 12px;
  font-size: 14px;
  font-weight: 500;
  color: #4b5563;
  cursor: pointer;
  transition: all 0.15s ease;
  user-select: none;
}

.nav-item:hover {
  background: #f9fafb;
  color: #111827;
}

.nav-item.active {
  background: #eef2ff;
  color: #4338ca;
}

.nav-item-icon {
  font-size: 20px;
  width: 20px;
  height: 20px;
  flex-shrink: 0;
}

.nav-item-label {
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.nav-item-badge {
  padding: 2px 8px;
  border-radius: 9999px;
  background: #e0e7ff;
  color: #4338ca;
  font-size: 10px;
  font-weight: 700;
  line-height: 1;
}

.sidebar-footer {
  padding: 16px;
  flex-shrink: 0;
}
</style>
