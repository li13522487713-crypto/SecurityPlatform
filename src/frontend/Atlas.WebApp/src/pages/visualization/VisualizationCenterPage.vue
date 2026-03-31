<template>
  <a-card :title="t('visualization.centerTitle')" class="page-card" :loading="loading">
    <template #extra>
      <a-button type="primary" @click="loadOverview">{{ t("visualization.refresh") }}</a-button>
    </template>

    <a-row :gutter="[16, 16]">
      <a-col :span="6">
        <a-card size="small" bordered>
          <div class="kpi-title">{{ t("visualization.kpiTotalProcesses") }}</div>
          <div class="kpi-value">{{ overview?.totalProcesses ?? "-" }}</div>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card size="small" bordered>
          <div class="kpi-title">{{ t("visualization.kpiRunningInstances") }}</div>
          <div class="kpi-value">{{ overview?.runningInstances ?? "-" }}</div>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card size="small" bordered>
          <div class="kpi-title">{{ t("visualization.kpiBlockedNodes") }}</div>
          <div class="kpi-value">{{ overview?.blockedNodes ?? "-" }}</div>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card size="small" bordered>
          <div class="kpi-title">{{ t("visualization.kpiAlertsToday") }}</div>
          <div class="kpi-value">{{ overview?.alertsToday ?? "-" }}</div>
        </a-card>
      </a-col>
    </a-row>

    <a-card :title="t('visualization.cardMetrics')" size="small" style="margin-top: 16px">
      <a-row :gutter="[16, 16]">
        <a-col :span="6">
          <a-statistic :title="t('visualization.statPendingTasks')" :value="metrics?.pendingTasks ?? 0" />
        </a-col>
        <a-col :span="6">
          <a-statistic :title="t('visualization.statOverdueTasks')" :value="metrics?.overdueTasks ?? 0" />
        </a-col>
        <a-col :span="6">
          <a-statistic :title="t('visualization.statAssetsTotal')" :value="metrics?.assetsTotal ?? 0" />
        </a-col>
        <a-col :span="6">
          <a-statistic :title="t('visualization.statAuditToday')" :value="metrics?.auditEventsToday ?? 0" />
        </a-col>
      </a-row>
    </a-card>

    <a-card :title="t('visualization.cardRisk')" size="small" style="margin-top: 16px">
      <a-list :data-source="overview?.riskHints || []" bordered>
        <template #renderItem="{ item }">
          <a-list-item>
            <a-space>
              <a-badge status="warning" />
              <span>{{ item }}</span>
            </a-space>
          </a-list-item>
        </template>
      </a-list>
    </a-card>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => {
  isMounted.value = true;
});
onUnmounted(() => {
  isMounted.value = false;
});

import { getVisualizationMetrics, getVisualizationOverview } from "@/services/api";
import type { VisualizationMetricsResponse, VisualizationOverview } from "@/types/api";
import { message } from "ant-design-vue";

const { t } = useI18n();

const overview = ref<VisualizationOverview>();
const metrics = ref<VisualizationMetricsResponse>();
const loading = ref(false);

const loadOverview = async () => {
  try {
    loading.value = true;
    const [overviewResult, metricsResult] = await Promise.all([
      getVisualizationOverview(),
      getVisualizationMetrics()
    ]);
    if (!isMounted.value) return;
    overview.value = overviewResult;
    metrics.value = metricsResult;
    if (!isMounted.value) return;
  } catch (err) {
    message.error((err as Error).message);
  } finally {
    loading.value = false;
  }
};

onMounted(() => {
  loadOverview();
});
</script>

<style scoped>
.kpi-title {
  color: var(--color-text-secondary);
  font-size: 13px;
}
.kpi-value {
  font-size: 28px;
  font-weight: 600;
}
</style>
