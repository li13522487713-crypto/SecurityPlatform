<template>
  <div class="console-overview">
    <div class="console-overview__container">
      <!-- Hero Section -->
      <div class="hero-section">
        <div class="hero-section__left">
          <h1 class="hero-section__greeting">
            {{ greetingPrefix }}{{ t("home.heroComma") }}<span class="hero-section__name">{{ welcomeName }}</span> 👋
          </h1>
          <p class="hero-section__status">
            <span class="hero-section__pulse">
              <span class="hero-section__pulse-ring" />
              <span class="hero-section__pulse-dot" />
            </span>
            {{ t("home.systemHealthy") }}
          </p>
        </div>
        <div class="hero-section__actions">
          <button type="button" class="hero-btn hero-btn--outline" @click="$router.push('/console/audit')">
            <FileTextOutlined class="hero-btn__icon" />
            {{ t("home.exportReport") }}
          </button>
          <button type="button" class="hero-btn hero-btn--primary" @click="$router.push('/console/catalog')">
            <PlusOutlined class="hero-btn__icon" />
            {{ t("home.newResource") }}
          </button>
        </div>
      </div>

      <!-- Metrics Grid -->
      <div class="metrics-grid">
        <a-skeleton :loading="loadingMetrics" active :paragraph="{ rows: 2 }">
          <div class="metrics-grid__row">
            <StatCard
              :title="t('home.statAssetsTotal')"
              :value="metrics?.assetsTotal ?? 0"
              :trend="12.5"
              :trend-label="t('home.trendVsLastMonth')"
              :icon="CloudServerOutlined"
              color="blue"
              :sparkline-data="[400, 300, 550, 400, 700]"
            />
            <StatCard
              :title="t('home.statAlertsToday')"
              :value="metrics?.alertsToday ?? 0"
              :trend="-100"
              :trend-label="t('home.trendVsYesterday')"
              :icon="ExclamationCircleOutlined"
              color="emerald"
              :sparkline-data="[100, 120, 90, 60, 40]"
            />
            <StatCard
              :title="t('home.statAuditToday')"
              :value="metrics?.auditEventsToday ?? 0"
              :trend="5.2"
              :trend-label="t('home.trendVsYesterday')"
              :icon="FileTextOutlined"
              color="purple"
              :sparkline-data="[20, 50, 100, 120, 150]"
            />
            <StatCard
              :title="t('home.statRunningFlows')"
              :value="metrics?.runningInstances ?? 0"
              :trend="-2.4"
              :trend-label="t('home.trendVsLastWeek')"
              :icon="ThunderboltOutlined"
              color="orange"
              :sparkline-data="[80, 60, 90, 110, 130]"
            />
          </div>
        </a-skeleton>
      </div>

      <!-- Main Content Grid -->
      <div class="main-grid">
        <!-- Left Column -->
        <div class="main-grid__left">
          <SystemLoadChart />
          <CoreAppsGrid />
        </div>

        <!-- Right Column -->
        <div class="main-grid__right">
          <ActionCenter
            :tasks="pendingTaskItems"
            :alerts="alertItems"
            :task-count="pendingTasks.length"
            :alert-count="recentAlerts.length"
            :loading="loadingTasks || loadingAlerts"
          />
          <ActivityTimeline
            :items="timelineItems"
            :loading="loadingAudits"
            @view-all="$router.push('/console/audit')"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import {
  CloudServerOutlined,
  ExclamationCircleOutlined,
  FileTextOutlined,
  PlusOutlined,
  ThunderboltOutlined,
} from "@ant-design/icons-vue";
import { getAuthProfile, hasPermission } from "@atlas/shared-core";
import type { VisualizationMetricsResponse } from "@atlas/shared-core";
import { getAlertsPaged, getAuditsPaged } from "@/services/api-users";
import { getMyTasksPaged, getVisualizationMetrics } from "@/services/api-dashboard";
import StatCard from "@/components/console/StatCard.vue";
import SystemLoadChart from "@/components/console/SystemLoadChart.vue";
import CoreAppsGrid from "@/components/console/CoreAppsGrid.vue";
import ActionCenter from "@/components/console/ActionCenter.vue";
import ActivityTimeline from "@/components/console/ActivityTimeline.vue";

const { t, locale } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

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

const greetingPrefix = computed(() => {
  const hour = new Date().getHours();
  if (hour < 12) return t("home.greetingMorning");
  if (hour < 18) return t("home.greetingAfternoon");
  return t("home.greetingEvening");
});

const pendingTaskItems = computed(() =>
  pendingTasks.value.map((task) => ({
    id: task.id,
    title: task.flowName,
    assignee: task.applicantName,
  }))
);

const alertItems = computed(() =>
  recentAlerts.value.map((alert) => ({
    id: alert.id,
    title: alert.title,
    severity: alert.severity,
  }))
);

const timelineItems = computed(() =>
  recentAudits.value.map((audit) => ({
    id: audit.id,
    title: `${audit.action} ${audit.targetDescription}`,
    user: audit.actorName,
    time: formatTime(audit.createdAt),
  }))
);

const formatTime = (value: string) => {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleTimeString(locale.value === "en-US" ? "en-US" : "zh-CN", {
    hour: "2-digit",
    minute: "2-digit",
  });
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
      createdAt: item.createdAt,
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
      createdAt: String(item.createdAt ?? new Date().toISOString()),
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
      createdAt: item.occurredAt,
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
.console-overview {
  background: #f4f7f9;
  padding: 16px 24px;
}

@media (min-width: 768px) {
  .console-overview {
    padding: 24px 32px;
  }
}

@media (min-width: 1024px) {
  .console-overview {
    padding: 32px;
  }
}

.console-overview__container {
  max-width: 1280px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

/* ── Hero Section ── */
.hero-section {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 16px;
  padding-bottom: 8px;
}

.hero-section__greeting {
  margin: 0;
  font-size: 24px;
  font-weight: 700;
  color: #111827;
  letter-spacing: -0.02em;
  line-height: 1.3;
}

@media (min-width: 640px) {
  .hero-section__greeting {
    font-size: 30px;
  }
}

.hero-section__name {
  color: #4f46e5;
}

.hero-section__status {
  margin: 4px 0 0;
  font-size: 14px;
  color: #6b7280;
  display: flex;
  align-items: center;
  gap: 8px;
}

.hero-section__pulse {
  position: relative;
  display: inline-flex;
  width: 8px;
  height: 8px;
}

.hero-section__pulse-ring {
  position: absolute;
  inset: 0;
  border-radius: 50%;
  background: #34d399;
  opacity: 0.75;
  animation: hero-ping 1s cubic-bezier(0, 0, 0.2, 1) infinite;
}

.hero-section__pulse-dot {
  position: relative;
  display: inline-flex;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #10b981;
}

@keyframes hero-ping {
  75%, 100% {
    transform: scale(2);
    opacity: 0;
  }
}

.hero-section__actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.hero-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border-radius: 8px;
  padding: 8px 16px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  font-family: inherit;
  transition: all 0.15s ease;
  white-space: nowrap;
  line-height: 1.5;
}

.hero-btn__icon {
  font-size: 14px;
}

.hero-btn--outline {
  background: #fff;
  border: 1px solid #e5e7eb;
  color: #374151;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
}

.hero-btn--outline:hover {
  background: #f9fafb;
}

.hero-btn--outline:focus-visible {
  outline: 2px solid #4f46e5;
  outline-offset: 2px;
}

.hero-btn--primary {
  background: #4f46e5;
  border: 1px solid transparent;
  color: #fff;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
}

.hero-btn--primary:hover {
  background: #4338ca;
}

.hero-btn--primary:focus-visible {
  outline: 2px solid #4f46e5;
  outline-offset: 2px;
}

/* ── Metrics Grid ── */
.metrics-grid__row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 24px;
}

/* ── Main Content Grid ── */
.main-grid {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 24px;
}

.main-grid__left {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.main-grid__right {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

/* ── Responsive ── */
@media (max-width: 1024px) {
  .console-overview {
    padding: 16px;
  }

  .metrics-grid__row {
    grid-template-columns: repeat(2, 1fr);
    gap: 16px;
  }

  .main-grid {
    grid-template-columns: 1fr;
  }

  .hero-section {
    flex-direction: column;
    align-items: flex-start;
  }
}

@media (max-width: 640px) {
  .metrics-grid__row {
    grid-template-columns: 1fr;
  }
}
</style>
