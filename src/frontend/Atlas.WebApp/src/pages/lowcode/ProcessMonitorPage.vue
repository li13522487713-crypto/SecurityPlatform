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
          <a-statistic title="超时预警" :value="stats.overdueCount" value-style="color: #cf1322" />
        </a-card>
      </a-col>
    </a-row>

    <a-divider />
    <h3>流程实例列表</h3>
    <a-table :columns="columns" :data-source="instances" :pagination="pagination" :loading="loading" row-key="instanceId" @change="onTableChange">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="statusColor(record.status)">{{ record.status }}</a-tag>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-button type="link" @click="viewTrace(record.instanceId)">查看轨迹</a-button>
        </template>
      </template>
    </a-table>

    <a-modal v-model:open="traceVisible" title="流程轨迹" width="700px" :footer="null">
      <a-timeline v-if="traceData.length">
        <a-timeline-item v-for="(node, idx) in traceData" :key="idx" :color="node.status === 'Completed' ? 'green' : node.status === 'Active' ? 'blue' : 'gray'">
          <p><strong>{{ node.nodeName }}</strong> ({{ node.nodeType }})</p>
          <p v-if="node.handlerName">处理人: {{ node.handlerName }}</p>
          <p>{{ node.startedAt }} → {{ node.completedAt || '进行中' }}</p>
          <p v-if="node.remark" style="color: #999">{{ node.remark }}</p>
        </a-timeline-item>
      </a-timeline>
      <a-empty v-else description="暂无轨迹数据" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { requestApi } from "@/services/api";
import type { ApiResponse, PagedResult } from "@/types/api";

interface DashboardStats { activeInstances: number; completedToday: number; pendingTasks: number; overdueCount: number; }
interface InstanceItem { instanceId: string; flowName: string; initiatorName: string; status: string; startedAt: string; completedAt?: string; currentNode?: string; }
interface TraceNode { nodeName: string; nodeType: string; handlerName?: string; status: string; startedAt: string; completedAt?: string; remark?: string; }

const stats = reactive<DashboardStats>({ activeInstances: 0, completedToday: 0, pendingTasks: 0, overdueCount: 0 });
const instances = ref<InstanceItem[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({ current: 1, pageSize: 10, total: 0 });
const traceVisible = ref(false);
const traceData = ref<TraceNode[]>([]);

const columns = [
  { title: "流程名称", dataIndex: "flowName", key: "flowName" },
  { title: "发起人", dataIndex: "initiatorName", key: "initiatorName", width: 120 },
  { title: "当前节点", dataIndex: "currentNode", key: "currentNode", width: 140 },
  { title: "状态", key: "status", width: 100 },
  { title: "发起时间", dataIndex: "startedAt", key: "startedAt", width: 180 },
  { title: "操作", key: "actions", width: 120 }
];

const statusColor = (status: string) => {
  if (status === "Running" || status === "Active") return "blue";
  if (status === "Completed") return "green";
  if (status === "Cancelled" || status === "Failed") return "red";
  return "default";
};

const fetchStats = async () => {
  try {
    const resp = await requestApi<ApiResponse<DashboardStats>>("/process-monitor/dashboard");
    if (resp.data) { Object.assign(stats, resp.data); }
  } catch { /* ignore */ }
};

const fetchInstances = async () => {
  loading.value = true;
  try {
    const q = new URLSearchParams({ pageIndex: (pagination.current ?? 1).toString(), pageSize: (pagination.pageSize ?? 10).toString() });
    const resp = await requestApi<ApiResponse<PagedResult<InstanceItem>>>(`/process-monitor/instances?${q}`);
    if (resp.data) { instances.value = resp.data.items; pagination.total = resp.data.total; }
  } catch (e) { message.error((e as Error).message); } finally { loading.value = false; }
};

const onTableChange = (pager: TablePaginationConfig) => { pagination.current = pager.current; pagination.pageSize = pager.pageSize; fetchInstances(); };

const viewTrace = async (instanceId: string) => {
  traceData.value = [];
  traceVisible.value = true;
  try {
    const resp = await requestApi<ApiResponse<TraceNode[]>>(`/process-monitor/instances/${instanceId}/trace`);
    if (resp.data) { traceData.value = resp.data; }
  } catch (e) { message.error((e as Error).message); }
};

onMounted(() => { fetchStats(); fetchInstances(); });
</script>

<style scoped>
.process-monitor-page { padding: 24px; }
.process-monitor-page h2 { margin: 0 0 16px; font-size: 20px; }
.process-monitor-page h3 { margin: 0 0 12px; font-size: 16px; }
.stats-row { margin-bottom: 16px; }
</style>
