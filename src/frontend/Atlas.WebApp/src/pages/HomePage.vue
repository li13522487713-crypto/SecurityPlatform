<template>
  <div class="workbench">
    <div class="workbench-header">
      <h2 class="workbench-title">工作台</h2>
      <span class="workbench-date">{{ todayDate }}</span>
    </div>

    <a-row :gutter="24">
      <!-- 左侧：待处理事项 -->
      <a-col :span="16">
        <!-- 待审批任务 -->
        <a-card class="workbench-card" :bordered="false">
          <template #title>
            <div class="card-title-row">
              <span>待审批任务</span>
              <a-badge :count="pendingTasks.length" :overflow-count="99" />
            </div>
          </template>
          <template #extra>
            <a-button type="link" @click="$router.push('/process/tasks')">查看全部</a-button>
          </template>
          <a-skeleton :loading="loadingTasks" active :paragraph="{ rows: 3 }">
            <a-list
              v-if="pendingTasks.length > 0"
              :data-source="pendingTasks"
              size="small"
            >
              <template #renderItem="{ item }">
                <a-list-item>
                  <a-list-item-meta :title="item.flowName" :description="item.applicantName" />
                  <template #actions>
                    <span class="task-time">{{ formatRelativeTime(item.createdAt) }}</span>
                  </template>
                </a-list-item>
              </template>
            </a-list>
            <a-empty v-else description="暂无待审批任务" :image="emptyImage" />
          </a-skeleton>
        </a-card>

        <!-- 告警待处理 -->
        <a-card class="workbench-card" :bordered="false">
          <template #title>
            <div class="card-title-row">
              <span>告警待处理</span>
              <a-badge :count="recentAlerts.length" :overflow-count="99" />
            </div>
          </template>
          <template #extra>
            <a-button type="link" @click="$router.push('/alert')">查看全部</a-button>
          </template>
          <a-skeleton :loading="loadingAlerts" active :paragraph="{ rows: 3 }">
            <a-list
              v-if="recentAlerts.length > 0"
              :data-source="recentAlerts"
              size="small"
            >
              <template #renderItem="{ item }">
                <a-list-item>
                  <a-list-item-meta>
                    <template #title>
                      <a-space>
                        <a-tag :color="severityColor(item.severity)">{{ item.severity }}</a-tag>
                        <span>{{ item.title }}</span>
                      </a-space>
                    </template>
                    <template #description>{{ item.source }}</template>
                  </a-list-item-meta>
                  <template #actions>
                    <span class="task-time">{{ formatRelativeTime(item.createdAt) }}</span>
                  </template>
                </a-list-item>
              </template>
            </a-list>
            <a-empty v-else description="暂无待处理告警" :image="emptyImage" />
          </a-skeleton>
        </a-card>

        <!-- 最近审计活动 -->
        <a-card class="workbench-card" :bordered="false" title="最近活动">
          <a-skeleton :loading="loadingAudits" active :paragraph="{ rows: 3 }">
            <a-timeline v-if="recentAudits.length > 0">
              <a-timeline-item v-for="audit in recentAudits" :key="audit.id">
                <span class="audit-time">{{ formatTime(audit.createdAt) }}</span>
                {{ audit.actorName }} {{ audit.action }} {{ audit.targetDescription }}
              </a-timeline-item>
            </a-timeline>
            <a-empty v-else description="暂无最近活动" :image="emptyImage" />
          </a-skeleton>
        </a-card>
      </a-col>

      <!-- 右侧：数据概览 -->
      <a-col :span="8">
        <a-card class="workbench-card" :bordered="false" title="数据概览">
          <a-skeleton :loading="loadingMetrics" active>
            <a-row :gutter="[0, 20]">
              <a-col :span="24">
                <a-statistic title="资产总量" :value="metrics?.assetsTotal ?? 0">
                  <template #prefix>
                    <DatabaseOutlined />
                  </template>
                </a-statistic>
              </a-col>
              <a-col :span="24">
                <a-statistic title="今日告警" :value="metrics?.alertsToday ?? 0" :value-style="alertsStyle">
                  <template #prefix>
                    <AlertOutlined />
                  </template>
                </a-statistic>
              </a-col>
              <a-col :span="24">
                <a-statistic title="今日审计事件" :value="metrics?.auditEventsToday ?? 0">
                  <template #prefix>
                    <FileSearchOutlined />
                  </template>
                </a-statistic>
              </a-col>
              <a-col :span="24">
                <a-statistic title="运行中流程" :value="metrics?.runningInstances ?? 0">
                  <template #prefix>
                    <ThunderboltOutlined />
                  </template>
                </a-statistic>
              </a-col>
            </a-row>
          </a-skeleton>
        </a-card>

        <a-card class="workbench-card" :bordered="false" title="快捷入口">
          <a-row :gutter="[12, 12]">
            <a-col v-for="entry in quickEntries" :key="entry.path" :span="12">
              <a-button block @click="$router.push(entry.path)">{{ entry.label }}</a-button>
            </a-col>
          </a-row>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { getVisualizationMetrics } from "@/services/api";
import type { VisualizationMetricsResponse } from "@/types/api";
import { getAuthProfile, hasPermission } from "@/utils/auth";

const emptyImage = Empty.PRESENTED_IMAGE_SIMPLE;

const loadingTasks = ref(false);
const loadingAlerts = ref(false);
const loadingAudits = ref(false);
const loadingMetrics = ref(false);

const pendingTasks = ref<Array<{ id: string; flowName: string; applicantName: string; createdAt: string }>>([]);
const recentAlerts = ref<Array<{ id: string; title: string; severity: string; source: string; createdAt: string }>>([]);
const recentAudits = ref<Array<{ id: string; actorName: string; action: string; targetDescription: string; createdAt: string }>>([]);
const metrics = ref<VisualizationMetricsResponse | null>(null);
const profile = ref(getAuthProfile());

type QuickEntrySource = {
  title: string;
  description: string;
  path: string;
  permissions: string[];
};

const organizationEntriesSource: QuickEntrySource[] = [
  { title: "员工管理", description: "人员信息 / 角色与权限 / 账号状态", path: "/system/users", permissions: ["users:view"] },
  { title: "部门管理", description: "组织层级 / 负责人 / 可见范围", path: "/system/departments", permissions: ["departments:view"] },
  { title: "职位管理", description: "岗位序列 / 影响面提示 / 状态", path: "/system/positions", permissions: ["positions:view"] },
  { title: "角色管理", description: "成员 / 权限 / 数据范围", path: "/system/roles", permissions: ["roles:view"] }
];

const businessEntriesSource: QuickEntrySource[] = [
  { title: "权限管理", description: "功能权限 + 数据权限配置", path: "/system/permissions", permissions: ["permissions:view"] },
  { title: "菜单管理", description: "菜单层级 / 权限绑定 / 隐藏", path: "/system/menus", permissions: ["menus:view"] },
  { title: "项目管理", description: "成员分配 / 数据隔离", path: "/system/projects", permissions: ["projects:view"] },
  { title: "应用配置", description: "可见范围 / 项目模式 / 状态", path: "/system/apps", permissions: ["apps:view"] }
];

const securityEntriesSource: QuickEntrySource[] = [
  { title: "资产中心", description: "资产盘点 / 分类 / 风险定位", path: "/assets", permissions: ["assets:view"] },
  { title: "审计中心", description: "操作留痕 / 风险回溯", path: "/audit", permissions: ["audit:view"] },
  { title: "告警中心", description: "告警聚合 / 处置跟踪", path: "/alert", permissions: ["alert:view"] },
  { title: "审批中心", description: "流程编排 / 审批任务", path: "/approval/flows", permissions: ["approval:flow:view", "approval:flow:create"] }
];

const canAccess = (permissions: string[]) => permissions.some((code) => hasPermission(profile.value, code));

const organizationEntries = computed(() =>
  organizationEntriesSource
    .filter((entry) => canAccess(entry.permissions))
    .map((entry) => ({ title: entry.title, description: entry.description, path: entry.path }))
);

const businessEntries = computed(() =>
  businessEntriesSource
    .filter((entry) => canAccess(entry.permissions))
    .map((entry) => ({ title: entry.title, description: entry.description, path: entry.path }))
);

const securityEntries = computed(() =>
  securityEntriesSource
    .filter((entry) => canAccess(entry.permissions))
    .map((entry) => ({ title: entry.title, description: entry.description, path: entry.path }))
);

const go = (path: string) => router.push(path);

const loadMetrics = async () => {
  if (!hasPermission(profile.value, "visualization:view")) {
    metrics.value = null;
    return;
  }

  loading.value = true;
  try {
    metrics.value = await getVisualizationMetrics();
  } catch (error) {
    message.error((error as Error).message || "加载指标失败");
  } finally {
    loadingMetrics.value = false;
  }
};

onMounted(() => {
  loadPendingTasks();
  loadRecentAlerts();
  loadRecentAudits();
  loadMetrics();
});
</script>

<style scoped>
.workbench {
  max-width: 1200px;
}

.workbench-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.workbench-title {
  margin: 0;
  font-size: 22px;
  font-weight: 600;
}

.workbench-date {
  color: var(--color-text-tertiary);
}

.workbench-card {
  margin-bottom: 16px;
}

.card-title-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.task-time {
  color: var(--color-text-tertiary);
  font-size: 12px;
}

.audit-time {
  color: var(--color-text-tertiary);
  margin-right: 8px;
  font-size: 12px;
}
</style>
