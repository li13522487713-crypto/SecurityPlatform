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

    <a-card title="风险提示" size="small" style="margin-top: 16px">
      <a-list
        :data-source="overview?.riskHints || []"
        bordered
        :renderItem="(item) => (
          <a-list-item>
            <a-space>
              <a-badge status='warning' />
              <span>{{ item }}</span>
            </a-space>
          </a-list-item>
        )"
      />
    </a-card>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { getVisualizationOverview } from "@/services/api";
import type { VisualizationOverview } from "@/types/api";
import { message } from "ant-design-vue";

const overview = ref<VisualizationOverview>();
const loading = ref(false);

const loadOverview = async () => {
  try {
    loading.value = true;
    overview.value = await getVisualizationOverview();
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
  color: #595959;
  font-size: 13px;
}
.kpi-value {
  font-size: 28px;
  font-weight: 600;
}
</style>
