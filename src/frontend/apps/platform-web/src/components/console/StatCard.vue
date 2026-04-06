<template>
  <div class="stat-card">
    <div class="stat-card__header">
      <div class="stat-card__info">
        <h3 class="stat-card__value">{{ formattedValue }}</h3>
        <p class="stat-card__title">{{ title }}</p>
      </div>
      <div class="stat-card__icon-wrapper" :style="iconWrapperStyle">
        <component :is="icon" class="stat-card__icon" :style="{ color: iconColor }" />
      </div>
    </div>
    <div class="stat-card__footer">
      <span class="stat-card__trend" :class="trendClass">
        <RiseOutlined v-if="trend >= 0" class="stat-card__trend-icon" />
        <FallOutlined v-else class="stat-card__trend-icon" />
        {{ Math.abs(trend) }}%
      </span>
      <span class="stat-card__trend-label">{{ trendLabel }}</span>
      <svg class="stat-card__sparkline" viewBox="0 0 80 32" preserveAspectRatio="none">
        <polyline
          :points="sparklinePoints"
          fill="none"
          :stroke="trend >= 0 ? '#10b981' : '#ef4444'"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
        />
      </svg>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type { Component } from "vue";
import { RiseOutlined, FallOutlined } from "@ant-design/icons-vue";

interface Props {
  title: string;
  value: number | string;
  trend: number;
  trendLabel: string;
  icon: Component;
  color: "blue" | "emerald" | "purple" | "orange" | "red" | "indigo" | "teal" | "sky";
  sparklineData: number[];
}

const props = defineProps<Props>();

const colorMap: Record<string, { bg: string; text: string; ring: string }> = {
  blue: { bg: "#eff6ff", text: "#2563eb", ring: "rgba(59, 130, 246, 0.2)" },
  red: { bg: "#fef2f2", text: "#dc2626", ring: "rgba(239, 68, 68, 0.2)" },
  teal: { bg: "#f0fdfa", text: "#0d9488", ring: "rgba(20, 184, 166, 0.2)" },
  orange: { bg: "#fff7ed", text: "#ea580c", ring: "rgba(249, 115, 22, 0.2)" },
  indigo: { bg: "#eef2ff", text: "#4f46e5", ring: "rgba(99, 102, 241, 0.2)" },
  emerald: { bg: "#ecfdf5", text: "#059669", ring: "rgba(16, 185, 129, 0.2)" },
  purple: { bg: "#faf5ff", text: "#9333ea", ring: "rgba(147, 51, 234, 0.2)" },
  sky: { bg: "#f0f9ff", text: "#0284c7", ring: "rgba(14, 165, 233, 0.2)" },
};

const iconWrapperStyle = computed(() => {
  const c = colorMap[props.color] ?? colorMap.blue;
  return {
    background: c.bg,
    boxShadow: `0 0 0 1px ${c.ring}`,
  };
});

const iconColor = computed(() => (colorMap[props.color] ?? colorMap.blue).text);

const formattedValue = computed(() => {
  if (typeof props.value === "string") return props.value;
  return props.value.toLocaleString();
});

const trendClass = computed(() =>
  props.trend >= 0 ? "stat-card__trend--positive" : "stat-card__trend--negative"
);

const sparklinePoints = computed(() => {
  const data = props.sparklineData;
  if (!data || data.length < 2) return "";
  const max = Math.max(...data);
  const min = Math.min(...data);
  const range = max - min || 1;
  const step = 80 / (data.length - 1);
  return data
    .map((v, i) => `${i * step},${32 - ((v - min) / range) * 28 - 2}`)
    .join(" ");
});
</script>

<style scoped>
.stat-card {
  background: #fff;
  border-radius: 16px;
  padding: 20px;
  border: 1px solid rgba(229, 231, 235, 0.6);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  transition: box-shadow 0.2s ease;
}

.stat-card:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
}

.stat-card__header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 16px;
}

.stat-card__value {
  margin: 0;
  font-size: 30px;
  font-weight: 700;
  line-height: 1.2;
  color: #111827;
  letter-spacing: -0.02em;
}

.stat-card__title {
  margin: 4px 0 0;
  font-size: 14px;
  font-weight: 500;
  color: #6b7280;
}

.stat-card__icon-wrapper {
  padding: 10px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: transform 0.2s ease;
}

.stat-card:hover .stat-card__icon-wrapper {
  transform: scale(1.1);
}

.stat-card__icon {
  font-size: 20px;
}

.stat-card__footer {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  padding-top: 8px;
}

.stat-card__trend {
  display: flex;
  align-items: center;
  gap: 2px;
  font-size: 12px;
  font-weight: 600;
}

.stat-card__trend--positive {
  color: #059669;
}

.stat-card__trend--negative {
  color: #dc2626;
}

.stat-card__trend-icon {
  font-size: 12px;
}

.stat-card__trend-label {
  font-size: 12px;
  color: #9ca3af;
  font-weight: 500;
  margin-left: 6px;
}

.stat-card__sparkline {
  width: 80px;
  height: 32px;
  opacity: 0.8;
  transition: opacity 0.2s ease;
}

.stat-card:hover .stat-card__sparkline {
  opacity: 1;
}
</style>
