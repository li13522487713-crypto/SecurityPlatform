<template>
  <div class="load-chart-card">
    <div class="load-chart-card__header">
      <div>
        <h2 class="load-chart-card__title">{{ t("home.systemLoadTitle") }}</h2>
        <p class="load-chart-card__desc">{{ t("home.systemLoadDesc") }}</p>
      </div>
      <select v-model="timeRange" class="load-chart-card__select">
        <option value="today">{{ t("home.timeRangeToday") }}</option>
        <option value="7d">{{ t("home.timeRange7d") }}</option>
        <option value="month">{{ t("home.timeRangeMonth") }}</option>
      </select>
    </div>
    <div ref="chartRef" class="load-chart-card__chart" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from "vue";
import { useI18n } from "vue-i18n";
import * as echarts from "echarts/core";
import { LineChart as ELineChart, BarChart } from "echarts/charts";
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
} from "echarts/components";
import { CanvasRenderer } from "echarts/renderers";

echarts.use([ELineChart, BarChart, GridComponent, TooltipComponent, LegendComponent, CanvasRenderer]);

const { t } = useI18n();

const timeRange = ref("today");

const chartRef = ref<HTMLElement>();
let chartInstance: echarts.ECharts | null = null;

const activityData = [
  { time: "00:00", assets: 4000, alerts: 24 },
  { time: "04:00", assets: 3000, alerts: 13 },
  { time: "08:00", assets: 2000, alerts: 98 },
  { time: "12:00", assets: 2780, alerts: 39 },
  { time: "16:00", assets: 1890, alerts: 48 },
  { time: "20:00", assets: 2390, alerts: 38 },
  { time: "24:00", assets: 3490, alerts: 43 },
];

function initChart() {
  if (!chartRef.value) return;
  chartInstance = echarts.init(chartRef.value);
  chartInstance.setOption({
    tooltip: {
      trigger: "axis",
      backgroundColor: "#fff",
      borderColor: "transparent",
      borderRadius: 12,
      padding: [12, 16],
      textStyle: { fontSize: 14, fontWeight: 500, color: "#374151" },
      boxShadow: "0 4px 6px -1px rgb(0 0 0 / 0.1)",
      extraCssText: "box-shadow: 0 4px 6px -1px rgb(0 0 0 / 0.1);",
    },
    grid: { top: 10, right: 10, bottom: 24, left: 40 },
    xAxis: {
      type: "category",
      data: activityData.map((d) => d.time),
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { fontSize: 12, color: "#6b7280" },
    },
    yAxis: {
      type: "value",
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { fontSize: 12, color: "#6b7280" },
      splitLine: { lineStyle: { color: "#e5e7eb", type: "dashed" } },
    },
    series: [
      {
        name: t("home.chartAssetCalls"),
        type: "line",
        smooth: true,
        data: activityData.map((d) => d.assets),
        lineStyle: { width: 2, color: "#6366f1" },
        itemStyle: { color: "#6366f1" },
        areaStyle: {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: "rgba(99, 102, 241, 0.2)" },
            { offset: 1, color: "rgba(99, 102, 241, 0)" },
          ]),
        },
        symbol: "none",
      },
      {
        name: t("home.chartAlertCount"),
        type: "line",
        smooth: true,
        data: activityData.map((d) => d.alerts),
        lineStyle: { width: 2, color: "#f43f5e" },
        itemStyle: { color: "#f43f5e" },
        symbol: "none",
      },
    ],
  });
}

function handleResize() {
  chartInstance?.resize();
}

onMounted(() => {
  initChart();
  window.addEventListener("resize", handleResize);
});

onUnmounted(() => {
  window.removeEventListener("resize", handleResize);
  chartInstance?.dispose();
});

watch(timeRange, () => {
  initChart();
});
</script>

<style scoped>
.load-chart-card {
  background: #fff;
  border-radius: 16px;
  padding: 20px;
  border: 1px solid rgba(229, 231, 235, 0.6);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
}

.load-chart-card__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 24px;
}

.load-chart-card__title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #111827;
}

.load-chart-card__desc {
  margin: 2px 0 0;
  font-size: 12px;
  color: #6b7280;
}

.load-chart-card__select {
  min-width: 100px;
  background: #f9fafb;
  border: 1px solid #e5e7eb;
  color: #374151;
  font-size: 14px;
  border-radius: 8px;
  padding: 4px 8px;
  cursor: pointer;
  font-family: inherit;
  outline: none;
}

.load-chart-card__select:focus {
  border-color: rgba(99, 102, 241, 0.5);
  box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.1);
}

.load-chart-card__chart {
  height: 288px;
  width: 100%;
}
</style>
