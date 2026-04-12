<template>
  <div class="stat-card">
    <div class="stat-top">
      <div class="stat-info">
        <h3 class="stat-value">
          {{ value }} <span class="stat-unit">{{ unit }}</span>
        </h3>
        <p class="stat-title">{{ title }}</p>
      </div>
      <div class="stat-icon-wrapper" :style="{ background: iconBg }">
        <component :is="icon" class="stat-icon" :style="{ color: iconColor }" />
      </div>
    </div>
    <div class="stat-bottom">
      <div class="stat-change">
        <span class="stat-change-label">{{ t('dashboard.vsYesterday') }}</span>
        <span class="stat-change-value" :class="trend === 'up' ? 'trend-up' : 'trend-down'">
          {{ trend === 'up' ? '↑' : '↓' }} {{ change }}
        </span>
      </div>
      <div class="stat-mini-chart">
        <v-chart :option="chartOption" :autoresize="chartAutoresize" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import VChart from "vue-echarts";
import type { Component } from "vue";

const { t } = useI18n();

const props = defineProps<{
  title: string;
  value: string;
  unit?: string;
  change: string;
  trend: "up" | "down";
  icon: Component;
  iconColor: string;
  iconBg: string;
  chartData: number[];
}>();

const chartAutoresize = {
  throttle: 120
} as const;

const chartOption = computed(() => ({
  grid: { top: 0, right: 0, bottom: 0, left: 0 },
  xAxis: { type: "category" as const, show: false, data: props.chartData.map((_, i) => i) },
  yAxis: { type: "value" as const, show: false },
  series: [{
    type: "line" as const,
    data: props.chartData,
    smooth: true,
    symbol: "none",
    lineStyle: { width: 2, color: props.iconColor },
    areaStyle: { color: "transparent" },
  }],
  animation: false,
}));
</script>

<style scoped>
.stat-card {
  background: #fff;
  padding: 20px;
  border-radius: 16px;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
  border: 1px solid rgba(243, 244, 246, 0.5);
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.stat-top {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
}

.stat-value {
  font-size: 30px;
  font-weight: 700;
  color: #111827;
  letter-spacing: -0.025em;
  margin: 0;
  display: flex;
  align-items: baseline;
  gap: 4px;
}

.stat-unit {
  font-size: 14px;
  font-weight: 500;
  color: #6b7280;
}

.stat-title {
  font-size: 14px;
  color: #6b7280;
  margin: 4px 0 0 0;
}

.stat-icon-wrapper {
  padding: 8px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.stat-icon {
  font-size: 24px;
}

.stat-bottom {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  margin-top: 24px;
}

.stat-change {
  display: flex;
  flex-direction: column;
}

.stat-change-label {
  font-size: 12px;
  color: #9ca3af;
  margin-bottom: 2px;
}

.stat-change-value {
  font-size: 14px;
  font-weight: 700;
  display: flex;
  align-items: center;
  gap: 4px;
}

.trend-up {
  color: #10b981;
}

.trend-down {
  color: #f43f5e;
}

.stat-mini-chart {
  width: 96px;
  height: 40px;
}
</style>
