<template>
  <div class="workbench">
    <!-- 顶栏：问候 + 指标卡片，一行展示 -->
    <div class="workbench-top">
      <div class="top-greeting">
        <h1 class="greeting-text">{{ t("home.welcomeBack", { name: welcomeName }) }}</h1>
        <span class="greeting-date">{{ todayDate }}</span>
      </div>
      <div class="metrics-skeleton">
      <a-skeleton :loading="loadingMetrics" active :paragraph="false">
        <div class="metrics-row">
          <div class="metric-tile metric-tile--blue">
            <DatabaseOutlined class="metric-icon" />
            <span class="metric-value">{{ metrics?.assetsTotal ?? 0 }}</span>
            <span class="metric-label">{{ t("home.statAssetsTotal") }}</span>
          </div>
          <div class="metric-tile metric-tile--red">
            <AlertOutlined class="metric-icon" />
            <span class="metric-value metric-value--alert">{{ metrics?.alertsToday ?? 0 }}</span>
            <span class="metric-label">{{ t("home.statAlertsToday") }}</span>
          </div>
          <div class="metric-tile metric-tile--teal">
            <FileSearchOutlined class="metric-icon" />
            <span class="metric-value">{{ metrics?.auditEventsToday ?? 0 }}</span>
            <span class="metric-label">{{ t("home.statAuditToday") }}</span>
          </div>
          <div class="metric-tile metric-tile--amber">
            <ThunderboltOutlined class="metric-icon" />
            <span class="metric-value">{{ metrics?.runningInstances ?? 0 }}</span>
            <span class="metric-label">{{ t("home.statRunningFlows") }}</span>
          </div>
        </div>
      </a-skeleton>
      </div>
    </div>

    <!-- 主内容：三栏平铺 -->
    <div class="workbench-body">
      <!-- 左：待审批 -->
      <div class="dd-card">
        <div class="dd-card-head">
          <div class="card-title-row">
            <span>{{ t("home.pendingApprovals") }}</span>
            <a-badge :count="pendingTasks.length" :overflow-count="99" />
          </div>
          <a-button type="link" size="small" @click="$router.push('/approval/flows')">{{ t("home.viewAll") }}</a-button>
        </div>
        <div class="dd-card-body">
          <a-skeleton :loading="loadingTasks" active :paragraph="{ rows: 3 }">
            <a-list v-if="pendingTasks.length > 0" :data-source="pendingTasks" size="small" :split="false">
              <template #renderItem="{ item }">
                <a-list-item>
                  <a-list-item-meta :title="item.flowName" :description="item.applicantName" />
                  <template #actions>
                    <span class="task-time">{{ formatRelativeTime(item.createdAt) }}</span>
                  </template>
                </a-list-item>
              </template>
            </a-list>
            <a-empty v-else :description="t('home.emptyPendingTasks')" :image="emptyImage" />
          </a-skeleton>
        </div>
      </div>

      <!-- 中：告警 + 最近活动 -->
      <div class="body-center">
        <div class="dd-card dd-card--half">
          <div class="dd-card-head">
            <div class="card-title-row">
              <span>{{ t("home.alertsPendingTitle") }}</span>
              <a-badge :count="recentAlerts.length" :overflow-count="99" />
            </div>
            <a-button type="link" size="small" @click="$router.push('/console/alert')">{{ t("home.viewAll") }}</a-button>
          </div>
          <div class="dd-card-body">
            <a-skeleton :loading="loadingAlerts" active :paragraph="{ rows: 2 }">
              <a-list v-if="recentAlerts.length > 0" :data-source="recentAlerts" size="small" :split="false">
                <template #renderItem="{ item }">
                  <a-list-item>
                    <a-list-item-meta>
                      <template #title>
                        <a-space>
                          <a-tag :color="severityColor(item.severity)" style="margin: 0;">{{ item.severity }}</a-tag>
                          <span>{{ item.title }}</span>
                        </a-space>
                      </template>
                    </a-list-item-meta>
                    <template #actions>
                      <span class="task-time">{{ formatRelativeTime(item.createdAt) }}</span>
                    </template>
                  </a-list-item>
                </template>
              </a-list>
              <a-empty v-else :description="t('home.emptyAlerts')" :image="emptyImage" />
            </a-skeleton>
          </div>
        </div>
        <div class="dd-card dd-card--half">
          <div class="dd-card-head">
            <span class="dd-card-title">{{ t("home.recentActivity") }}</span>
          </div>
          <div class="dd-card-body">
            <a-skeleton :loading="loadingAudits" active :paragraph="{ rows: 2 }">
              <a-timeline v-if="recentAudits.length > 0" class="compact-timeline">
                <a-timeline-item v-for="audit in recentAudits" :key="audit.id">
                  <span class="audit-time">{{ formatTime(audit.createdAt) }}</span>
                  {{ audit.actorName }} {{ audit.action }} {{ audit.targetDescription }}
                </a-timeline-item>
              </a-timeline>
              <a-empty v-else :description="t('home.emptyActivity')" :image="emptyImage" />
            </a-skeleton>
          </div>
        </div>
      </div>

      <!-- 右：快捷入口 -->
      <div class="dd-card">
        <div class="dd-card-head">
          <span class="dd-card-title">{{ t("home.quickLinks") }}</span>
        </div>
        <div class="dd-card-body quick-links-body">
          <button
            v-for="entry in quickEntries"
            :key="entry.path"
            type="button"
            class="quick-link-tile"
            @click="$router.push(entry.path)"
          >
            <span class="quick-link-icon-wrap" aria-hidden="true">
              <component :is="entry.icon" class="quick-link-icon" />
            </span>
            <span class="quick-link-label">{{ entry.label }}</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, onUnmounted } from "vue";
import type { Component } from "vue";
import { useI18n } from "vue-i18n";
import { Empty, message } from "ant-design-vue";
import {
  AlertOutlined,
  ApartmentOutlined,
  AppstoreOutlined,
  BellOutlined,
  DatabaseOutlined,
  FileSearchOutlined,
  FormOutlined,
  MenuOutlined,
  ProjectOutlined,
  SafetyCertificateOutlined,
  ThunderboltOutlined,
  UserOutlined
} from "@ant-design/icons-vue";
import { getAuthProfile, hasPermission } from "@atlas/shared-core";
import type { VisualizationMetricsResponse } from "@atlas/shared-core";
import { getAlertsPaged } from "@/services/api-users";
import { getAuditsPaged } from "@/services/api-users";
import { getMyTasksPaged, getVisualizationMetrics } from "@/services/api-dashboard";

const { t, locale } = useI18n();
const emptyImage = Empty.PRESENTED_IMAGE_SIMPLE;

const isMounted = ref(false);
onMounted(() => {
  isMounted.value = true;
});
onUnmounted(() => {
  isMounted.value = false;
});

const loadingTasks = ref(false);
const loadingAlerts = ref(false);
const loadingAudits = ref(false);
const loadingMetrics = ref(false);

const pendingTasks = ref<Array<{ id: string; flowName: string; applicantName: string; createdAt: string }>>([]);
const recentAlerts = ref<Array<{ id: string; title: string; severity: string; source: string; createdAt: string }>>([]);
const recentAudits = ref<Array<{ id: string; actorName: string; action: string; targetDescription: string; createdAt: string }>>([]);
const metrics = ref<VisualizationMetricsResponse | null>(null);
const profile = ref(getAuthProfile());

const welcomeName = computed(() => {
  const p = profile.value;
  const name = p?.displayName?.trim() || p?.username?.trim();
  return name && name.length > 0 ? name : t("home.welcomeFallbackName");
});

type QuickEntrySource = {
  titleKey: string;
  path: string;
  permissions: string[];
  icon: Component;
};

const organizationEntriesSource: QuickEntrySource[] = [
  { titleKey: "home.quickUsersTitle", path: "/console/settings/org/users", permissions: ["users:view"], icon: UserOutlined },
  { titleKey: "home.quickRolesTitle", path: "/console/settings/auth/roles", permissions: ["roles:view"], icon: SafetyCertificateOutlined },
  { titleKey: "home.quickMenusTitle", path: "/console/settings/auth/menus", permissions: ["menus:view"], icon: MenuOutlined },
  { titleKey: "home.quickProjectsTitle", path: "/console/settings/projects", permissions: ["projects:view"], icon: ProjectOutlined }
];

const businessEntriesSource: QuickEntrySource[] = [
  { titleKey: "home.quickDatasourcesTitle", path: "/console/settings/system/datasources", permissions: ["*:*:*"], icon: DatabaseOutlined },
  { titleKey: "home.quickWorkflowTitle", path: "/console/workflow/designer", permissions: ["workflow:view"], icon: ApartmentOutlined }
];

const securityEntriesSource: QuickEntrySource[] = [
  { titleKey: "home.quickAssetsTitle", path: "/console/assets", permissions: ["assets:view"], icon: AppstoreOutlined },
  { titleKey: "home.quickAuditTitle", path: "/console/audit", permissions: ["audit:view"], icon: FileSearchOutlined },
  { titleKey: "home.quickAlertCenterTitle", path: "/console/alert", permissions: ["alert:view"], icon: BellOutlined },
  { titleKey: "home.quickApprovalTitle", path: "/console/approval/flows", permissions: ["approval:flow:view", "approval:flow:create"], icon: FormOutlined }
];

const canAccess = (permissions: string[]) => permissions.some((code) => hasPermission(profile.value, code));

const quickEntries = computed(() =>
  [...organizationEntriesSource.filter((e) => canAccess(e.permissions)), ...businessEntriesSource.filter((e) => canAccess(e.permissions)), ...securityEntriesSource.filter((e) => canAccess(e.permissions))]
    .slice(0, 8)
    .map((entry) => ({ label: t(entry.titleKey), path: entry.path, icon: entry.icon }))
);

const todayDate = computed(() => new Date().toLocaleDateString(locale.value === "en-US" ? "en-US" : "zh-CN", { weekday: "long", year: "numeric", month: "long", day: "numeric" }));

const severityColor = (severity: string) => {
  const s = severity.toLowerCase();
  if (s.includes("critical") || s.includes("high")) return "red";
  if (s.includes("medium")) return "orange";
  if (s.includes("low")) return "blue";
  return "default";
};

const formatRelativeTime = (value: string) => {
  const now = Date.now();
  const then = new Date(value).getTime();
  if (Number.isNaN(then)) return value;
  const diff = Math.max(0, Math.floor((now - then) / 1000));
  if (diff < 60) return t("home.relativeJustNow");
  if (diff < 3600) return t("home.relativeMinutesAgo", { n: Math.floor(diff / 60) });
  if (diff < 86400) return t("home.relativeHoursAgo", { n: Math.floor(diff / 3600) });
  return t("home.relativeDaysAgo", { n: Math.floor(diff / 86400) });
};

const formatTime = (value: string) => {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString(locale.value === "en-US" ? "en-US" : "zh-CN");
};

const loadPendingTasks = async () => {
  if (!hasPermission(profile.value, "approval:flow:view")) {
    pendingTasks.value = [];
    return;
  }
  loadingTasks.value = true;
  try {
    const result = await getMyTasksPaged({ pageIndex: 1, pageSize: 5 });
    if (!isMounted.value) return;
    pendingTasks.value = (result.items ?? []).map((item) => ({
      id: String(item.id),
      flowName: item.title,
      applicantName: String(item.assigneeValue ?? "-"),
      createdAt: item.createdAt
    }));
  } catch (error) {
    message.error((error as Error).message || t("home.loadTasksFailed"));
  } finally {
    loadingTasks.value = false;
  }
};

const loadRecentAlerts = async () => {
  if (!hasPermission(profile.value, "alert:view")) {
    recentAlerts.value = [];
    return;
  }
  loadingAlerts.value = true;
  try {
    const result = await getAlertsPaged({ pageIndex: 1, pageSize: 5 });
    if (!isMounted.value) return;
    recentAlerts.value = (result.items ?? []).map((item) => ({
      id: String(item.id ?? ""),
      title: String(item.title ?? t("home.unnamedAlert")),
      severity: String(item.severity ?? t("home.unknownSeverity")),
      source: String(item.source ?? "-"),
      createdAt: String(item.createdAt ?? new Date().toISOString())
    }));
  } catch (error) {
    message.error((error as Error).message || t("home.loadAlertsFailed"));
  } finally {
    loadingAlerts.value = false;
  }
};

const loadRecentAudits = async () => {
  if (!hasPermission(profile.value, "audit:view")) {
    recentAudits.value = [];
    return;
  }
  loadingAudits.value = true;
  try {
    const result = await getAuditsPaged({ pageIndex: 1, pageSize: 6 });
    if (!isMounted.value) return;
    recentAudits.value = (result.items ?? []).map((item) => ({
      id: String(item.id),
      actorName: item.actor,
      action: item.action,
      targetDescription: item.target,
      createdAt: item.occurredAt
    }));
  } catch (error) {
    message.error((error as Error).message || t("home.loadAuditsFailed"));
  } finally {
    loadingAudits.value = false;
  }
};

const loadMetrics = async () => {
  if (!hasPermission(profile.value, "visualization:view")) {
    metrics.value = null;
    return;
  }
  loadingMetrics.value = true;
  try {
    metrics.value = await getVisualizationMetrics();
    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("home.loadMetricsFailed"));
  } finally {
    loadingMetrics.value = false;
  }
};

onMounted(() => {
  loadPendingTasks();
  loadRecentAlerts();
  loadRecentAudits();
  loadMetrics();
});
</script>

<style scoped>
.workbench {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 56px);
  margin: 0;
  padding: 16px 20px;
  background: #f2f3f5;
  overflow: hidden;
  box-sizing: border-box;
}

/* ── 顶栏：问候 + 指标，一行横排 ── */
.workbench-top {
  display: flex;
  align-items: center;
  gap: 24px;
  flex-shrink: 0;
  margin-bottom: 12px;
}

.top-greeting {
  flex-shrink: 0;
  min-width: 0;
}

.greeting-text {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  line-height: 1.4;
  color: var(--color-text);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.greeting-date {
  font-size: 12px;
  color: var(--color-text-tertiary);
  white-space: nowrap;
}

.metrics-skeleton {
  flex: 1;
  min-width: 0;
}

.metrics-row {
  display: flex;
  gap: 12px;
  flex: 1;
  min-width: 0;
}

.metric-tile {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 1px 2px rgba(15, 23, 42, 0.06);
  min-width: 0;
}

.metric-icon {
  font-size: 20px;
  flex-shrink: 0;
}

.metric-tile--blue .metric-icon { color: #1677ff; }
.metric-tile--red .metric-icon { color: #ff4d4f; }
.metric-tile--teal .metric-icon { color: #13c2c2; }
.metric-tile--amber .metric-icon { color: #fa8c16; }

.metric-value {
  font-size: 20px;
  font-weight: 600;
  line-height: 1;
  color: var(--color-text);
  flex-shrink: 0;
}

.metric-value--alert { color: #cf1322; }

.metric-label {
  font-size: 12px;
  color: var(--color-text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  min-width: 0;
}

/* ── 主体：三栏 flex 平铺撑满剩余高度 ── */
.workbench-body {
  display: flex;
  gap: 12px;
  flex: 1;
  min-height: 0;
}

.workbench-body > .dd-card {
  flex: 1;
}

.body-center {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-height: 0;
}

.dd-card {
  display: flex;
  flex-direction: column;
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 1px 2px rgba(15, 23, 42, 0.06), 0 2px 8px rgba(15, 23, 42, 0.04);
  min-height: 0;
}

.dd-card--half {
  flex: 1;
  min-height: 0;
}

.dd-card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 16px;
  border-bottom: 1px solid rgba(0, 0, 0, 0.06);
  flex-shrink: 0;
}

.dd-card-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text);
}

.dd-card-body {
  flex: 1;
  padding: 8px 16px 12px;
  overflow-y: auto;
  min-height: 0;
}

.card-title-row {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text);
}

.task-time {
  color: var(--color-text-tertiary);
  font-size: 12px;
}

.audit-time {
  color: var(--color-text-tertiary);
  margin-right: 6px;
  font-size: 12px;
}

.compact-timeline {
  padding-top: 4px;
}

:deep(.compact-timeline .ant-timeline-item) {
  padding-bottom: 10px;
  font-size: 13px;
}

/* ── 快捷入口 2x4 网格 ── */
.quick-links-body {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 8px;
  padding-top: 10px;
}

.quick-link-tile {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 10px 4px;
  margin: 0;
  border: none;
  border-radius: 8px;
  background: #fafafa;
  cursor: pointer;
  font: inherit;
  color: var(--color-text);
  transition: background 0.2s ease, box-shadow 0.2s ease;
}

.quick-link-tile:hover {
  background: #e6f4ff;
  box-shadow: 0 1px 2px rgba(22, 119, 255, 0.12);
}

.quick-link-tile:focus-visible {
  outline: 2px solid #1677ff;
  outline-offset: 2px;
}

.quick-link-icon-wrap {
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 4px;
}

.quick-link-icon {
  font-size: 22px;
  color: #1677ff;
}

.quick-link-label {
  font-size: 11px;
  line-height: 1.3;
  text-align: center;
  word-break: break-word;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
</style>
