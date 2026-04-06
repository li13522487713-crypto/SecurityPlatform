<template>
  <div class="activity-card">
    <div class="activity-card__header">
      <h2 class="activity-card__title">{{ t("home.recentActivity") }}</h2>
      <a-button type="text" size="small" class="activity-card__more">
        <EllipsisOutlined />
      </a-button>
    </div>

    <div class="activity-card__body">
      <a-skeleton :loading="loading" active :paragraph="{ rows: 4 }">
        <div v-if="items.length > 0" class="timeline">
          <div
            v-for="(item, index) in items"
            :key="item.id"
            class="timeline__item"
          >
            <div class="timeline__dot-col">
              <div
                class="timeline__dot"
                :style="{ boxShadow: `0 0 0 2px ${dotRingColors[index % dotRingColors.length]}` }"
              >
                <div
                  class="timeline__dot-inner"
                  :style="{ background: dotInnerColors[index % dotInnerColors.length] }"
                />
              </div>
              <div v-if="index < items.length - 1" class="timeline__line" />
            </div>
            <div class="timeline__content">
              <div class="timeline__row">
                <span class="timeline__event">{{ item.title }}</span>
                <span class="timeline__time">{{ item.time }}</span>
              </div>
              <span class="timeline__user">{{ item.user }}</span>
            </div>
          </div>
        </div>
        <a-empty v-else :description="t('home.emptyActivity')" />
      </a-skeleton>
    </div>

    <button type="button" class="activity-card__footer" @click="$emit('viewAll')">
      {{ t("home.viewFullLog") }}
    </button>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import { EllipsisOutlined } from "@ant-design/icons-vue";

interface TimelineItem {
  id: string;
  title: string;
  user: string;
  time: string;
}

defineProps<{
  items: TimelineItem[];
  loading: boolean;
}>();

defineEmits<{
  viewAll: [];
}>();

const { t } = useI18n();

const dotRingColors = ["#e0e7ff", "#d1fae5", "#dbeafe", "#ffedd5"];
const dotInnerColors = ["#6366f1", "#10b981", "#3b82f6", "#f97316"];
</script>

<style scoped>
.activity-card {
  background: #fff;
  border-radius: 16px;
  padding: 20px;
  border: 1px solid rgba(229, 231, 235, 0.6);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
  display: flex;
  flex-direction: column;
}

.activity-card__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
}

.activity-card__title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #111827;
}

.activity-card__more {
  color: #9ca3af;
}

.activity-card__body {
  flex: 1;
  min-height: 0;
}

.timeline {
  padding-left: 4px;
}

.timeline__item {
  display: flex;
  gap: 16px;
  padding-bottom: 16px;
  position: relative;
}

.timeline__dot-col {
  display: flex;
  flex-direction: column;
  align-items: center;
  flex-shrink: 0;
}

.timeline__dot {
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
  z-index: 1;
  margin-top: 4px;
}

.timeline__dot-inner {
  width: 6px;
  height: 6px;
  border-radius: 50%;
}

.timeline__line {
  width: 1px;
  flex: 1;
  background: #e5e7eb;
  margin-top: 4px;
}

.timeline__content {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.timeline__row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.timeline__event {
  font-size: 14px;
  font-weight: 500;
  color: #111827;
}

.timeline__time {
  font-size: 12px;
  color: #9ca3af;
  flex-shrink: 0;
}

.timeline__user {
  margin-top: 4px;
  font-size: 12px;
  font-weight: 500;
  color: #6b7280;
  background: #f3f4f6;
  display: inline-block;
  padding: 1px 6px;
  border-radius: 4px;
  width: fit-content;
}

.activity-card__footer {
  width: 100%;
  margin-top: 16px;
  padding: 8px 0;
  font-size: 14px;
  font-weight: 500;
  color: #4b5563;
  background: #f9fafb;
  border: 1px solid #f3f4f6;
  border-radius: 8px;
  cursor: pointer;
  font: inherit;
  transition: all 0.15s;
}

.activity-card__footer:hover {
  color: #4f46e5;
  background: rgba(238, 242, 255, 0.5);
}
</style>
