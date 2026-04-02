<template>
  <a-layout class="backend-capability-studio">
    <a-layout-header class="studio-header">
      <div class="header-row">
        <a-breadcrumb class="studio-breadcrumb">
          <a-breadcrumb-item>
            <router-link :to="{ name: 'app-workspace-dashboard', params: { appId } }">
              {{ t("route.appDashboard") }}
            </router-link>
          </a-breadcrumb-item>
          <a-breadcrumb-item>{{ t("logicFlow.studio.breadcrumb") }}</a-breadcrumb-item>
        </a-breadcrumb>
        <a-radio-group
          :value="studioMode"
          button-style="solid"
          size="small"
          @update:value="onModeChange"
        >
          <a-radio-button value="developer">{{ t("logicFlow.studio.modeDeveloper") }}</a-radio-button>
          <a-radio-button value="viewer">{{ t("logicFlow.studio.modeViewer") }}</a-radio-button>
        </a-radio-group>
      </div>
    </a-layout-header>

    <a-layout class="studio-body">
      <a-layout-sider
        v-model:collapsed="navCollapsed"
        class="studio-nav-sider"
        :width="navWidth"
        collapsible
        breakpoint="lg"
        :trigger="null"
      >
        <div class="nav-trigger-wrap">
          <a-button type="text" block @click="navCollapsed = !navCollapsed">
            <MenuUnfoldOutlined v-if="navCollapsed" />
            <MenuFoldOutlined v-else />
          </a-button>
        </div>
        <a-menu
          :selected-keys="[activeSection]"
          mode="inline"
          :inline-collapsed="navCollapsed"
          @click="onMenuClick"
        >
          <a-menu-item key="logic-designer">
            <template #icon><BranchesOutlined /></template>
            {{ t("logicFlow.studio.nav.logicDesigner") }}
          </a-menu-item>
          <a-menu-item key="function-designer">
            <template #icon><CodeOutlined /></template>
            {{ t("logicFlow.studio.nav.functionDesigner") }}
          </a-menu-item>
          <a-menu-item key="node-panel">
            <template #icon><AppstoreOutlined /></template>
            {{ t("logicFlow.studio.nav.nodePanel") }}
          </a-menu-item>
          <a-menu-item key="batch-jobs">
            <template #icon><ScheduleOutlined /></template>
            {{ t("logicFlow.studio.nav.batchJobs") }}
          </a-menu-item>
          <a-menu-item key="execution-monitor">
            <template #icon><DashboardOutlined /></template>
            {{ t("logicFlow.studio.nav.executionMonitor") }}
          </a-menu-item>
          <a-menu-item key="dead-letters">
            <template #icon><WarningOutlined /></template>
            {{ t("logicFlow.studio.nav.deadLetters") }}
          </a-menu-item>
        </a-menu>
      </a-layout-sider>

      <a-layout>
        <a-layout-content class="studio-center">
          <div class="center-scroll">
            <component :is="activeComponent" v-if="activeComponent" />
            <a-empty v-else :description="t('logicFlow.studio.emptySection')" />
          </div>
        </a-layout-content>

        <a-layout-sider
          v-model:collapsed="helpCollapsed"
          class="studio-help-sider"
          :width="helpWidth"
          collapsible
          reverse-arrow
          breakpoint="xl"
        >
          <div class="help-inner">
            <div class="help-title">{{ t("logicFlow.studio.contextHelpTitle") }}</div>
            <p class="help-text">{{ t(`logicFlow.studio.help.${helpKey}`) }}</p>
          </div>
        </a-layout-sider>
      </a-layout>
    </a-layout>
  </a-layout>
</template>

<script setup lang="ts">
import {
  AppstoreOutlined,
  BranchesOutlined,
  CodeOutlined,
  DashboardOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  ScheduleOutlined,
  WarningOutlined
} from "@ant-design/icons-vue";
import { computed, defineAsyncComponent, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import type { MenuInfo } from "ant-design-vue/es/menu/src/interface";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

type StudioSection =
  | "logic-designer"
  | "function-designer"
  | "node-panel"
  | "batch-jobs"
  | "execution-monitor"
  | "dead-letters";

type StudioMode = "developer" | "viewer";

const SECTION_KEYS: readonly StudioSection[] = [
  "logic-designer",
  "function-designer",
  "node-panel",
  "batch-jobs",
  "execution-monitor",
  "dead-letters"
] as const;

function isStudioSection(value: unknown): value is StudioSection {
  return typeof value === "string" && (SECTION_KEYS as readonly string[]).includes(value);
}

function isStudioMode(value: unknown): value is StudioMode {
  return value === "developer" || value === "viewer";
}

const navCollapsed = ref(false);
const helpCollapsed = ref(false);
const navWidth = 240;
const helpWidth = 280;

const appId = computed(() => String(route.params.appId ?? ""));

const activeSection = computed<StudioSection>(() => {
  const raw = route.query.section;
  const q = Array.isArray(raw) ? raw[0] : raw;
  return isStudioSection(q) ? q : "logic-designer";
});

const studioMode = computed<StudioMode>(() => {
  const raw = route.query.mode;
  const q = Array.isArray(raw) ? raw[0] : raw;
  return isStudioMode(q) ? q : "developer";
});

const helpKey = computed(() => {
  const map: Record<StudioSection, string> = {
    "logic-designer": "logicDesigner",
    "function-designer": "functionDesigner",
    "node-panel": "nodePanel",
    "batch-jobs": "batchJobs",
    "execution-monitor": "executionMonitor",
    "dead-letters": "deadLetters"
  };
  return map[activeSection.value];
});

const sectionComponents: Record<StudioSection, ReturnType<typeof defineAsyncComponent>> = {
  "logic-designer": defineAsyncComponent(() => import("./LogicFlowDesignerPage.vue")),
  "function-designer": defineAsyncComponent(() => import("./FunctionDesignerPage.vue")),
  "node-panel": defineAsyncComponent(() => import("./NodePanelPage.vue")),
  "batch-jobs": defineAsyncComponent(() => import("./BatchJobDesignerPage.vue")),
  "execution-monitor": defineAsyncComponent(() => import("./FlowExecutionMonitorPage.vue")),
  "dead-letters": defineAsyncComponent(() => import("./BatchDeadLetterPage.vue"))
};

const activeComponent = computed(() => sectionComponents[activeSection.value]);

function syncQuery(partial: Record<string, string | undefined>): void {
  void router.replace({
    name: "app-logic-flow-studio",
    params: { appId: appId.value },
    query: {
      ...route.query,
      ...partial
    }
  });
}

function onMenuClick(info: MenuInfo): void {
  const key = String(info.key);
  if (isStudioSection(key)) {
    syncQuery({ section: key });
  }
}

function onModeChange(mode: StudioMode | string): void {
  if (isStudioMode(mode)) {
    syncQuery({ mode });
  }
}

watch(
  () => route.query.section,
  (raw) => {
    const q = Array.isArray(raw) ? raw[0] : raw;
    if (q === undefined || q === "" || !isStudioSection(q)) {
      syncQuery({ section: "logic-designer" });
    }
  },
  { immediate: true }
);
</script>

<style scoped>
.backend-capability-studio {
  min-height: calc(100vh - 48px);
  background: var(--atlas-content-bg, #f5f5f5);
}

.studio-header {
  height: auto;
  line-height: 1.4;
  padding: 12px 24px;
  background: #fff;
  border-bottom: 1px solid #f0f0f0;
}

.header-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.studio-breadcrumb {
  margin: 0;
}

.studio-body {
  min-height: 560px;
}

.studio-nav-sider {
  background: #fff;
  border-right: 1px solid #f0f0f0;
}

.nav-trigger-wrap {
  padding: 8px;
  border-bottom: 1px solid #f0f0f0;
}

.studio-center {
  margin: 0;
  min-height: 520px;
  background: #fff;
}

.center-scroll {
  min-height: 520px;
  overflow: auto;
  padding: 0;
}

.studio-help-sider {
  background: #fafafa;
  border-left: 1px solid #f0f0f0;
}

.help-inner {
  padding: 16px;
}

.help-title {
  font-weight: 600;
  margin-bottom: 8px;
}

.help-text {
  margin: 0;
  color: rgba(0, 0, 0, 0.65);
  font-size: 13px;
  line-height: 1.6;
  white-space: pre-wrap;
}
</style>
