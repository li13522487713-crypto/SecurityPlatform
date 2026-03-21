<template>
  <div class="process-monitor-page">
    <h2>流程监控</h2>
    <a-row :gutter="16" class="stats-row">
      <a-col :span="6">
        <a-card>
          <a-statistic title="活跃流程" :value="stats.activeInstances" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card>
          <a-statistic title="今日完成" :value="stats.completedToday" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card>
          <a-statistic title="待处理任务" :value="stats.pendingTasks" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card>
          <a-statistic title="超时预警" :value="stats.overdueTasks" value-style="color: #cf1322" />
        </a-card>
      </a-col>
    </a-row>

    <a-divider />
    <h3>流程实例列表</h3>
    <a-table :columns="columns" :data-source="instances" :pagination="pagination" :loading="loading" row-key="id" @change="onTableChange">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="statusColor(record.status)">{{ statusLabel(record.status) }}</a-tag>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-button type="link" @click="viewTrace(record.id)">查看轨迹</a-button>
        </template>
      </template>
    </a-table>

    <a-modal v-model:open="traceVisible" title="流程轨迹" width="700px" :footer="null">
      <a-timeline v-if="traceData.length">
        <a-timeline-item
          v-for="node in traceData"
          :key="node.nodeId"
          :color="node.status === 'Completed' ? 'green' : node.status === 'Active' ? 'blue' : 'gray'"
        >
          <p><strong>{{ node.nodeName }}</strong> ({{ node.nodeType }})</p>
          <p v-if="node.assigneeName">处理人: {{ node.assigneeName }}</p>
          <p>{{ node.startedAt || "-" }} → {{ node.endedAt || "进行中" }}</p>
          <p v-if="node.comment" style="color: #999">{{ node.comment }}</p>
        </a-timeline-item>
      </a-timeline>
      <a-empty v-else description="暂无轨迹数据" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { getAdminInstancesPaged } from "@/services/api";
import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@/types/api";

interface DashboardStats {
  activeInstances: number;
  completedToday: number;
  pendingTasks: number;
  overdueTasks: number;
}

interface InstanceItem {
  id: string;
  flowName: string;
  initiatorUserId: string;
  status: number;
  startedAt: string;
  endedAt?: string;
  currentNodeName?: string;
}

interface ProcessNodeTrace {
  nodeId: string;
  nodeName: string;
  nodeType: string;
  status: string;
  startedAt?: string;
  endedAt?: string;
  durationMinutes?: number;
  assigneeName?: string;
  comment?: string;
}

interface ProcessInstanceTrace {
  instanceId: string;
  flowName: string;
  status: string;
  initiatorUserId: string;
  startedAt: string;
  endedAt?: string;
  nodes: ProcessNodeTrace[];
}

const stats = reactive<DashboardStats>({
  activeInstances: 0,
  completedToday: 0,
  pendingTasks: 0,
  overdueTasks: 0
});
const instances = ref<InstanceItem[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({ current: 1, pageSize: 10, total: 0 });
const traceVisible = ref(false);
const traceData = ref<ProcessNodeTrace[]>([]);

const columns = [
  { title: "流程名称", dataIndex: "flowName", key: "flowName" },
  { title: "发起人", dataIndex: "initiatorUserId", key: "initiatorUserId", width: 120 },
  { title: "当前节点", dataIndex: "currentNodeName", key: "currentNodeName", width: 140 },
  { title: "状态", key: "status", width: 100 },
  { title: "发起时间", dataIndex: "startedAt", key: "startedAt", width: 180 },
  { title: "操作", key: "actions", width: 120 }
];

const statusLabel = (status: number) => {
  if (status === 0) return "运行中";
  if (status === 1) return "已完成";
  if (status === 2) return "已拒绝";
  if (status === 3) return "已取消";
  return `未知(${status})`;
};

const statusColor = (status: number) => {
  if (status === 0) return "blue";
  if (status === 1) return "green";
  if (status === 2 || status === 3) return "red";
  return "default";
};

const fetchStats = async () => {
  try {
    const resp  = await requestApi<ApiResponse<DashboardStats>>("/process-monitor/dashboard");

    if (!isMounted.value) return;
    if (resp.data) {
      Object.assign(stats, resp.data);
    }
  } catch {
    // 忽略统计加载错误，主列表仍可用
  }
};

const fetchInstances = async () => {
  loading.value = true;
  try {
    const result  = await getAdminInstancesPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10
    });

    if (!isMounted.value) return;
    instances.value = result.items.map((item) => ({
      id: String(item.id),
      flowName: item.flowName,
      initiatorUserId: String(item.initiatorUserId),
      status: Number(item.status),
      startedAt: item.startedAt,
      endedAt: item.endedAt,
      currentNodeName: item.currentNodeName
    }));
    pagination.total = result.total;
  } catch (error) {
    message.error((error as Error).message);
  } finally {
    loading.value = false;
  }
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  void fetchInstances();
};

const viewTrace = async (instanceId: string) => {
  traceData.value = [];
  traceVisible.value = true;
  try {
    const resp  = await requestApi<ApiResponse<ProcessInstanceTrace>>(`/process-monitor/instances/${instanceId}/trace`);

    if (!isMounted.value) return;
    if (resp.data) {
      traceData.value = resp.data.nodes ?? [];
    }
  } catch (error) {
    message.error((error as Error).message);
  }
};

onMounted(() => {
  void fetchStats();
  void fetchInstances();
});
</script>

<style scoped>
.process-monitor-page { padding: 24px; }
.process-monitor-page h2 { margin: 0 0 16px; font-size: 20px; }
.process-monitor-page h3 { margin: 0 0 12px; font-size: 16px; }
.stats-row { margin-bottom: 16px; }
</style>
