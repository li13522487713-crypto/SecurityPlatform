<template>
  <a-card title="可视化中心" class="page-card" :loading="loading">
    <template #extra>
      <a-button type="primary" @click="loadOverview">刷新</a-button>
    </template>

    <a-row :gutter="[16, 16]">
      <a-col :span="6">
        <a-card size="small" bordered>
          <div class="kpi-title">流程总数</div>
          <div class="kpi-value">{{ overview?.totalProcesses ?? "-" }}</div>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card size="small" bordered>
          <div class="kpi-title">运行实例</div>
          <div class="kpi-value">{{ overview?.runningInstances ?? "-" }}</div>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card size="small" bordered>
          <div class="kpi-title">阻塞节点</div>
          <div class="kpi-value">{{ overview?.blockedNodes ?? "-" }}</div>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card size="small" bordered>
          <div class="kpi-title">今日告警</div>
          <div class="kpi-value">{{ overview?.alertsToday ?? "-" }}</div>
        </a-card>
      </a-col>
    </a-row>

    <a-card title="运行指标" size="small" style="margin-top: 16px">
      <a-row :gutter="[16, 16]">
        <a-col :span="6">
          <a-statistic title="待办任务" :value="metrics?.pendingTasks ?? 0" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="超时待办" :value="metrics?.overdueTasks ?? 0" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="资产总量" :value="metrics?.assetsTotal ?? 0" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="今日审计" :value="metrics?.auditEventsToday ?? 0" />
        </a-col>
      </a-row>
    </a-card>

    <a-card title="风险提示" size="small" style="margin-top: 16px">
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
import { onMounted, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { getVisualizationMetrics, getVisualizationOverview } from "@/services/api";
import type { VisualizationMetricsResponse, VisualizationOverview } from "@/types/api";
import { message } from "ant-design-vue";

const overview = ref<VisualizationOverview>();
const metrics = ref<VisualizationMetricsResponse>();
const loading = ref(false);

const loadOverview = async () => {
  try {
    loading.value = true;
    overview.value = await getVisualizationOverview();
    if (!isMounted.value) return;
    metrics.value = await getVisualizationMetrics();
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
