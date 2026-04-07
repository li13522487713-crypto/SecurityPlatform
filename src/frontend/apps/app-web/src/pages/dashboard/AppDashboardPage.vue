<template>
  <div class="dashboard-page">
    <!-- Header Section -->
    <div class="dashboard-header">
      <div>
        <h1 class="greeting">
          {{ greeting }}，<span class="greeting-name">{{ displayName }}</span>
          <span class="greeting-wave">👋</span>
        </h1>
        <p class="system-status">
          <span class="status-dot"></span>
          {{ t('dashboard.systemNormal') }}
        </p>
      </div>
      <div class="header-actions">
        <button class="btn-secondary">
          <FileTextOutlined />
          {{ t('dashboard.exportReport') }}
        </button>
        <button class="btn-primary">
          <span class="btn-plus">+</span>
          {{ t('dashboard.newResource') }}
        </button>
      </div>
    </div>

    <!-- Stats Grid -->
    <div class="stats-grid">
      <StatCard
        :title="t('dashboard.statAssets')"
        value="12,480"
        :change="'12.5%'"
        trend="up"
        :icon="DatabaseOutlined"
        icon-color="#3b82f6"
        icon-bg="#dbeafe"
        :chart-data="[10, 15, 12, 20, 25, 22, 30]"
      />
      <StatCard
        :title="t('dashboard.statAlerts')"
        value="0"
        :change="'100%'"
        trend="down"
        :icon="AlertOutlined"
        icon-color="#10b981"
        icon-bg="#d1fae5"
        :chart-data="[30, 25, 28, 20, 18, 15, 10]"
      />
      <StatCard
        :title="t('dashboard.statAuditEvents')"
        value="3,210"
        :change="'5.2%'"
        trend="up"
        :icon="HistoryOutlined"
        icon-color="#a855f7"
        icon-bg="#f3e8ff"
        :chart-data="[5, 8, 12, 10, 15, 18, 22]"
      />
      <StatCard
        :title="t('dashboard.statActiveFlows')"
        value="42"
        :change="'2.4%'"
        trend="down"
        :icon="ThunderboltOutlined"
        icon-color="#f43f5e"
        icon-bg="#ffe4e6"
        :chart-data="[20, 18, 15, 15, 16, 18, 20]"
      />
    </div>

    <!-- Main Content Area -->
    <div class="content-grid">
      <!-- Left Column -->
      <div class="content-left">
        <!-- Main Chart -->
        <div class="card">
          <div class="card-header">
            <div>
              <h3 class="card-title">{{ t('dashboard.chartTitle') }}</h3>
              <p class="card-subtitle">{{ t('dashboard.chartSubtitle') }}</p>
            </div>
            <select class="chart-select">
              <option>{{ t('dashboard.today') }}</option>
              <option>{{ t('dashboard.yesterday') }}</option>
              <option>{{ t('dashboard.last7Days') }}</option>
            </select>
          </div>
          <div class="chart-container">
            <v-chart :option="areaChartOption" :autoresize="true" />
          </div>
        </div>

        <!-- Core Apps Grid -->
        <div class="card">
          <div class="card-header">
            <h3 class="card-title">{{ t('dashboard.coreApps') }}</h3>
            <button class="link-btn">
              {{ t('dashboard.appMarket') }} <span class="link-arrow">›</span>
            </button>
          </div>
          <div class="apps-grid">
            <AppCard
              :icon="TeamOutlined"
              :title="t('dashboard.appEmployees')"
              :desc="t('dashboard.appEmployeesDesc')"
              color-class="color-blue"
            />
            <AppCard
              :icon="SafetyCertificateOutlined"
              :title="t('dashboard.appRoles')"
              :desc="t('dashboard.appRolesDesc')"
              color-class="color-indigo"
            />
            <AppCard
              :icon="ProjectOutlined"
              :title="t('dashboard.appProjects')"
              :desc="t('dashboard.appProjectsDesc')"
              color-class="color-emerald"
            />
            <AppCard
              :icon="DatabaseOutlined"
              :title="t('dashboard.appDataSource')"
              :desc="t('dashboard.appDataSourceDesc')"
              color-class="color-emerald"
            />
            <AppCard
              :icon="PartitionOutlined"
              :title="t('dashboard.appWorkflow')"
              :desc="t('dashboard.appWorkflowDesc')"
              color-class="color-orange"
            />
            <AppCard
              :icon="LaptopOutlined"
              :title="t('dashboard.appAssets')"
              :desc="t('dashboard.appAssetsDesc')"
              color-class="color-purple"
            />
            <AppCard
              :icon="HistoryOutlined"
              :title="t('dashboard.appAuditLog')"
              :desc="t('dashboard.appAuditLogDesc')"
              color-class="color-sky"
            />
            <AppCard
              :icon="RobotOutlined"
              :title="t('dashboard.appAI')"
              :desc="t('dashboard.appAIDesc')"
              color-class="color-rose"
            />
          </div>
        </div>
      </div>

      <!-- Right Column -->
      <div class="content-right">
        <!-- Todo Widget -->
        <div class="card todo-card">
          <div class="todo-tabs">
            <button class="todo-tab active">
              {{ t('dashboard.todoPending') }}
              <span class="todo-tab-badge">0</span>
            </button>
            <button class="todo-tab">
              {{ t('dashboard.todoAlerts') }}
              <span class="todo-tab-badge alert">0</span>
            </button>
          </div>
          <div class="todo-empty">
            <div class="todo-empty-icon">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                <path d="M5 13l4 4L19 7" />
              </svg>
            </div>
            <h4 class="todo-empty-title">{{ t('dashboard.noTodos') }}</h4>
            <p class="todo-empty-desc">{{ t('dashboard.noTodosDesc') }}</p>
          </div>
        </div>

        <!-- Recent Activity -->
        <div class="card">
          <div class="card-header">
            <h3 class="card-title">{{ t('dashboard.recentActivity') }}</h3>
            <button class="icon-more-btn">
              <EllipsisOutlined />
            </button>
          </div>
          <div class="activity-list">
            <ActivityItem
              :title="t('dashboard.actRefreshToken')"
              user="admin"
              time="15:16"
              dot-color="#6366f1"
            />
            <ActivityItem
              :title="t('dashboard.actLogin')"
              user="admin"
              time="14:59"
              dot-color="#10b981"
            />
            <ActivityItem
              :title="t('dashboard.actExportReport')"
              user="system"
              time="11:20"
              dot-color="#d1d5db"
            />
            <ActivityItem
              :title="t('dashboard.actEditPermission')"
              user="admin"
              time="09:30"
              dot-color="#d1d5db"
              :is-last="true"
            />
          </div>
          <button class="view-all-btn">
            {{ t('dashboard.viewFullLog') }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";
import { useI18n } from "vue-i18n";
import VChart from "vue-echarts";
import {
  FileTextOutlined,
  DatabaseOutlined,
  AlertOutlined,
  HistoryOutlined,
  ThunderboltOutlined,
  TeamOutlined,
  SafetyCertificateOutlined,
  ProjectOutlined,
  PartitionOutlined,
  LaptopOutlined,
  RobotOutlined,
  EllipsisOutlined
} from "@ant-design/icons-vue";
import { useAppUserStore } from "@/stores/user";
import StatCard from "@/components/dashboard/StatCard.vue";
import AppCard from "@/components/dashboard/AppCard.vue";
import ActivityItem from "@/components/dashboard/ActivityItem.vue";

const { t } = useI18n();
const route = useRoute();
const userStore = useAppUserStore();

const displayName = computed(() => userStore.name || "Admin");

const greeting = computed(() => {
  const hour = new Date().getHours();
  if (hour < 6) return t("dashboard.greetingNight");
  if (hour < 12) return t("dashboard.greetingMorning");
  if (hour < 18) return t("dashboard.greetingAfternoon");
  return t("dashboard.greetingEvening");
});

const areaChartOption = computed(() => ({
  grid: { top: 20, right: 20, bottom: 30, left: 40 },
  xAxis: {
    type: "category" as const,
    data: ["00:00", "04:00", "08:00", "12:00", "16:00", "20:00", "24:00"],
    axisLine: { show: false },
    axisTick: { show: false },
    axisLabel: { color: "#9ca3af", fontSize: 12 },
  },
  yAxis: {
    type: "value" as const,
    axisLine: { show: false },
    axisTick: { show: false },
    axisLabel: { color: "#9ca3af", fontSize: 12 },
    splitLine: { lineStyle: { color: "#f3f4f6", type: "dashed" as const } },
  },
  tooltip: {
    trigger: "axis" as const,
    backgroundColor: "#fff",
    borderColor: "transparent",
    borderRadius: 12,
    padding: [8, 12],
    textStyle: { color: "#111827", fontSize: 13 },
    extraCssText: "box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);",
  },
  series: [{
    type: "line" as const,
    data: [3000, 2000, 2800, 1900, 2200, 2900, 3800],
    smooth: true,
    symbol: "none",
    lineStyle: { width: 3, color: "#4f46e5" },
    areaStyle: {
      color: {
        type: "linear" as const,
        x: 0, y: 0, x2: 0, y2: 1,
        colorStops: [
          { offset: 0, color: "rgba(79, 70, 229, 0.2)" },
          { offset: 1, color: "rgba(79, 70, 229, 0)" },
        ],
      },
    },
  }],
  animation: false,
}));
</script>

<style scoped>
.dashboard-page {
  max-width: 1280px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

/* Header */
.dashboard-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.greeting {
  font-size: 30px;
  font-weight: 800;
  color: #111827;
  letter-spacing: -0.025em;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.greeting-name {
  color: #4f46e5;
}

.greeting-wave {
  font-size: 24px;
  margin-left: 4px;
}

.system-status {
  font-size: 14px;
  font-weight: 500;
  color: #059669;
  margin: 8px 0 0 0;
  display: flex;
  align-items: center;
  gap: 6px;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #10b981;
  display: inline-block;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.btn-secondary {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: #fff;
  border: 1px solid #e5e7eb;
  color: #374151;
  border-radius: 12px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.15s;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
}

.btn-secondary:hover {
  background: #f9fafb;
}

.btn-primary {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: #4f46e5;
  color: #fff;
  border: none;
  border-radius: 12px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.15s;
  box-shadow: 0 1px 3px rgba(79, 70, 229, 0.3);
}

.btn-primary:hover {
  background: #4338ca;
}

.btn-plus {
  font-size: 18px;
  line-height: 1;
  margin-bottom: 1px;
}

/* Stats Grid */
.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 24px;
}

@media (max-width: 1024px) {
  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 640px) {
  .stats-grid {
    grid-template-columns: 1fr;
  }
}

/* Content Grid */
.content-grid {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 24px;
}

@media (max-width: 1024px) {
  .content-grid {
    grid-template-columns: 1fr;
  }
}

.content-left {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.content-right {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

/* Card */
.card {
  background: #fff;
  padding: 24px;
  border-radius: 16px;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
  border: 1px solid rgba(243, 244, 246, 0.5);
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 24px;
}

.card-title {
  font-size: 18px;
  font-weight: 700;
  color: #111827;
  margin: 0;
}

.card-subtitle {
  font-size: 14px;
  color: #6b7280;
  margin: 0;
}

.chart-select {
  background: #f9fafb;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  color: #374151;
  padding: 8px 32px 8px 12px;
  cursor: pointer;
  outline: none;
}

.chart-container {
  height: 288px;
  width: 100%;
  min-width: 0;
}

/* Apps Grid */
.apps-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

@media (max-width: 768px) {
  .apps-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

.link-btn {
  background: none;
  border: none;
  font-size: 14px;
  font-weight: 500;
  color: #4f46e5;
  cursor: pointer;
  display: flex;
  align-items: center;
  padding: 0;
}

.link-btn:hover {
  color: #4338ca;
}

.link-arrow {
  margin-left: 4px;
}

/* Todo Widget */
.todo-card {
  padding: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.todo-tabs {
  display: flex;
  border-bottom: 1px solid rgba(243, 244, 246, 0.6);
  background: #fff;
}

.todo-tab {
  flex: 1;
  padding: 16px;
  font-size: 14px;
  font-weight: 500;
  color: #6b7280;
  background: none;
  border: none;
  border-bottom: 3px solid transparent;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  transition: color 0.15s;
}

.todo-tab:hover {
  color: #111827;
}

.todo-tab.active {
  font-weight: 600;
  color: #4f46e5;
  border-bottom-color: #4f46e5;
}

.todo-tab-badge {
  background: rgba(243, 244, 246, 0.8);
  color: #6b7280;
  padding: 2px 8px;
  border-radius: 9999px;
  font-size: 12px;
  font-weight: 700;
  line-height: 1;
}

.todo-tab-badge.alert {
  background: #fff1f2;
  color: #f43f5e;
}

.todo-empty {
  padding: 32px;
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  min-height: 220px;
}

.todo-empty-icon {
  width: 48px;
  height: 48px;
  background: #ecfdf5;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 16px;
  color: #10b981;
  box-shadow: 0 0 0 8px rgba(236, 253, 245, 0.5);
}

.todo-empty-title {
  font-size: 14px;
  font-weight: 600;
  color: #111827;
  margin: 0 0 4px 0;
}

.todo-empty-desc {
  font-size: 14px;
  color: #6b7280;
  margin: 0;
}

/* Activity */
.activity-list {
  padding-top: 8px;
}

.icon-more-btn {
  background: none;
  border: none;
  color: #9ca3af;
  cursor: pointer;
  font-size: 20px;
  display: flex;
  align-items: center;
  padding: 4px;
}

.icon-more-btn:hover {
  color: #4b5563;
}

.view-all-btn {
  width: 100%;
  margin-top: 24px;
  padding: 10px;
  background: #f9fafb;
  color: #4b5563;
  border: none;
  border-radius: 12px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.15s;
}

.view-all-btn:hover {
  background: #f3f4f6;
  color: #111827;
}
</style>
