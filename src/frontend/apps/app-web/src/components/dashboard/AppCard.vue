<template>
  <div class="app-card" @click="$emit('click')">
    <div class="app-card-icon" :class="colorClass">
      <component :is="icon" class="app-card-icon-inner" />
    </div>
    <div class="app-card-info">
      <h4 class="app-card-title">{{ title }}</h4>
      <p class="app-card-desc">{{ desc }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Component } from "vue";

defineProps<{
  icon: Component;
  title: string;
  desc: string;
  colorClass: string;
}>();

defineEmits<{
  click: [];
}>();
</script>

<style scoped>
.app-card {
  background: rgba(249, 250, 251, 0.5);
  padding: 16px;
  border-radius: 12px;
  border: 1px solid #f3f4f6;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  justify-content: center;
  gap: 12px;
}

.app-card:hover {
  border-color: #e0e7ff;
  background: #fff;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.06);
}

.app-card-icon {
  width: 48px;
  height: 48px;
  border-radius: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
}

.app-card-icon-inner {
  font-size: 24px;
}

/* Color variants */
.color-blue {
  background: #eff6ff;
  color: #2563eb;
}

.color-indigo {
  background: #eef2ff;
  color: #4f46e5;
}

.color-emerald {
  background: #ecfdf5;
  color: #059669;
}

.color-orange {
  background: #fff7ed;
  color: #ea580c;
}

.color-purple {
  background: #faf5ff;
  color: #9333ea;
}

.color-sky {
  background: #f0f9ff;
  color: #0284c7;
}

.color-rose {
  background: #fff1f2;
  color: #e11d48;
}

.app-card-title {
  font-size: 14px;
  font-weight: 600;
  color: #111827;
  margin: 0;
}

.app-card-desc {
  font-size: 12px;
  color: #6b7280;
  margin: 2px 0 0 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
