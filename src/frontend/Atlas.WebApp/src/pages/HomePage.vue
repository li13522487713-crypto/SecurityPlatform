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
import { message, Empty } from "ant-design-vue";
import {
  DatabaseOutlined,
  AlertOutlined,
  FileSearchOutlined,
  ThunderboltOutlined
} from "@ant-design/icons-vue";
import { getMyTasksPaged, getAlertsPaged, getAuditsPaged, getVisualizationMetrics } from "@/services/api";
import type { VisualizationMetricsResponse } from "@/types/api";

const emptyImage = Empty.PRESENTED_IMAGE_SIMPLE;

const loadingTasks = ref(false);
const loadingAlerts = ref(false);
const loadingAudits = ref(false);
const loadingMetrics = ref(false);

const pendingTasks = ref<Array<{ id: string; flowName: string; applicantName: string; createdAt: string }>>([]);
const recentAlerts = ref<Array<{ id: string; title: string; severity: string; source: string; createdAt: string }>>([]);
const recentAudits = ref<Array<{ id: string; actorName: string; action: string; targetDescription: string; createdAt: string }>>([]);
const metrics = ref<VisualizationMetricsResponse | null>(null);

const todayDate = new Date().toLocaleDateString("zh-CN", {
  year: "numeric",
  month: "long",
  day: "numeric",
  weekday: "long"
});

const alertsStyle = computed(() => {
  const count = metrics.value?.alertsToday ?? 0;
  return count > 0 ? { color: "#ff4d4f" } : {};
});

const quickEntries = [
  { label: "资产管理", path: "/assets" },
  { label: "告警管理", path: "/alert" },
  { label: "流程定义", path: "/process/flows" },
  { label: "我的待办", path: "/process/tasks" },
  { label: "员工管理", path: "/settings/org/users" },
  { label: "角色管理", path: "/settings/auth/roles" }
];

const severityColor = (severity: string) => {
  switch (severity) {
    case "Critical": return "red";
    case "High": return "orange";
    case "Medium": return "gold";
    case "Low": return "blue";
    default: return "default";
  }
};

const formatRelativeTime = (dateStr: string) => {
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMin = Math.floor(diffMs / 60000);
  if (diffMin < 1) return "刚刚";
  if (diffMin < 60) return `${diffMin}分钟前`;
  const diffHour = Math.floor(diffMin / 60);
  if (diffHour < 24) return `${diffHour}小时前`;
  const diffDay = Math.floor(diffHour / 24);
  return `${diffDay}天前`;
};

const formatTime = (dateStr: string) => {
  const date = new Date(dateStr);
  return date.toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit" });
};

const loadPendingTasks = async () => {
  loadingTasks.value = true;
  try {
    const result = await getMyTasksPaged({ pageIndex: 1, pageSize: 5, keyword: "" });
    pendingTasks.value = result.items.map((item) => ({
      id: item.id,
      flowName: item.title ?? "未命名流程",
      applicantName: item.assigneeValue ?? "",
      createdAt: item.createdAt
    }));
  } catch {
    // 静默处理
  } finally {
    loadingTasks.value = false;
  }
};

const loadRecentAlerts = async () => {
  loadingAlerts.value = true;
  try {
    const result = await getAlertsPaged({ pageIndex: 1, pageSize: 5, keyword: "" });
    recentAlerts.value = result.items.map((item) => ({
      id: item.id,
      title: item.title ?? "",
      severity: "Low",
      source: "",
      createdAt: item.createdAt ?? ""
    }));
  } catch {
    // 静默处理
  } finally {
    loadingAlerts.value = false;
  }
};

const loadRecentAudits = async () => {
  loadingAudits.value = true;
  try {
    const result = await getAuditsPaged({ pageIndex: 1, pageSize: 5, keyword: "" });
    recentAudits.value = result.items.map((item) => ({
      id: item.id,
      actorName: item.actor ?? "",
      action: item.action ?? "",
      targetDescription: item.target ?? "",
      createdAt: item.occurredAt ?? ""
    }));
  } catch {
    // 静默处理
  } finally {
    loadingAudits.value = false;
  }
};

const loadMetrics = async () => {
  loadingMetrics.value = true;
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
