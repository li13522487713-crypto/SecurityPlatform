<template>
  <div class="action-center">
    <div class="action-center__tabs">
      <button
        class="action-center__tab"
        :class="{ 'action-center__tab--active': activeTab === 'tasks' }"
        @click="activeTab = 'tasks'"
      >
        {{ t("home.pendingTasks") }}
        <span class="action-center__badge">{{ taskCount }}</span>
        <div v-if="activeTab === 'tasks'" class="action-center__tab-indicator action-center__tab-indicator--indigo" />
      </button>
      <button
        class="action-center__tab"
        :class="{ 'action-center__tab--active-alert': activeTab === 'alerts' }"
        @click="activeTab = 'alerts'"
      >
        {{ t("home.criticalAlerts") }}
        <span class="action-center__badge action-center__badge--red">{{ alertCount }}</span>
        <div v-if="activeTab === 'alerts'" class="action-center__tab-indicator action-center__tab-indicator--red" />
      </button>
    </div>

    <div class="action-center__body">
      <a-skeleton :loading="loading" active :paragraph="{ rows: 3 }">
        <template v-if="activeTab === 'tasks'">
          <div v-if="tasks.length > 0" class="action-center__list">
            <div v-for="task in tasks" :key="task.id" class="action-center__item">
              <span class="action-center__item-title">{{ task.title }}</span>
              <span class="action-center__item-meta">{{ task.assignee }}</span>
            </div>
          </div>
          <div v-else class="action-center__empty">
            <div class="action-center__empty-icon">
              <CheckCircleOutlined class="action-center__check-icon" />
            </div>
            <h3 class="action-center__empty-title">{{ t("home.noTasks") }}</h3>
            <p class="action-center__empty-desc">{{ t("home.noTasksDesc") }}</p>
          </div>
        </template>
        <template v-else>
          <div v-if="alerts.length > 0" class="action-center__list">
            <div v-for="alert in alerts" :key="alert.id" class="action-center__item">
              <span class="action-center__item-title">{{ alert.title }}</span>
              <span class="action-center__item-meta">{{ alert.severity }}</span>
            </div>
          </div>
          <div v-else class="action-center__empty">
            <div class="action-center__empty-icon">
              <CheckCircleOutlined class="action-center__check-icon" />
            </div>
            <h3 class="action-center__empty-title">{{ t("home.noAlerts") }}</h3>
            <p class="action-center__empty-desc">{{ t("home.noAlertsDesc") }}</p>
          </div>
        </template>
      </a-skeleton>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import { CheckCircleOutlined } from "@ant-design/icons-vue";

interface TaskItem {
  id: string;
  title: string;
  assignee: string;
}

interface AlertItem {
  id: string;
  title: string;
  severity: string;
}

withDefaults(defineProps<{
  tasks: TaskItem[];
  alerts: AlertItem[];
  taskCount: number;
  alertCount: number;
  loading: boolean;
}>(), {
  tasks: () => [],
  alerts: () => [],
  taskCount: 0,
  alertCount: 0,
  loading: false,
});

const { t } = useI18n();
const activeTab = ref<"tasks" | "alerts">("tasks");
</script>

<style scoped>
.action-center {
  background: #fff;
  border-radius: 16px;
  border: 1px solid rgba(229, 231, 235, 0.6);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  height: 320px;
}

.action-center__tabs {
  display: flex;
  border-bottom: 1px solid #f3f4f6;
}

.action-center__tab {
  flex: 1;
  padding: 14px 0;
  font-size: 14px;
  font-weight: 500;
  text-align: center;
  background: none;
  border: none;
  cursor: pointer;
  color: #6b7280;
  position: relative;
  transition: color 0.15s;
  font: inherit;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
}

.action-center__tab:hover {
  color: #374151;
}

.action-center__tab--active {
  color: #4f46e5;
}

.action-center__tab--active-alert {
  color: #dc2626;
}

.action-center__tab-indicator {
  position: absolute;
  bottom: 0;
  left: 0;
  width: 100%;
  height: 2px;
  border-radius: 2px 2px 0 0;
}

.action-center__tab-indicator--indigo {
  background: #4f46e5;
}

.action-center__tab-indicator--red {
  background: #dc2626;
}

.action-center__badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0 6px;
  height: 18px;
  font-size: 10px;
  font-weight: 700;
  background: #f3f4f6;
  color: #4b5563;
  border-radius: 9px;
}

.action-center__badge--red {
  background: #fef2f2;
  color: #dc2626;
}

.action-center__body {
  flex: 1;
  display: flex;
  flex-direction: column;
  padding: 24px;
  overflow: hidden;
}

.action-center__list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  overflow-y: auto;
}

.action-center__item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 0;
}

.action-center__item-title {
  font-size: 14px;
  font-weight: 500;
  color: #111827;
}

.action-center__item-meta {
  font-size: 12px;
  color: #9ca3af;
}

.action-center__empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
}

.action-center__empty-icon {
  width: 64px;
  height: 64px;
  background: #f9fafb;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 16px;
  box-shadow: 0 0 0 4px rgba(249, 250, 251, 0.5);
}

.action-center__check-icon {
  font-size: 32px;
  color: #34d399;
}

.action-center__empty-title {
  margin: 0 0 4px;
  font-size: 14px;
  font-weight: 500;
  color: #111827;
}

.action-center__empty-desc {
  margin: 0;
  font-size: 12px;
  color: #9ca3af;
}
</style>
