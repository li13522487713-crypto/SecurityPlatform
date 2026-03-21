<template>
  <div class="process-monitor-page">
    <h2>{{ t("lowcode.processMonitor.title") }}</h2>
    <a-row :gutter="16" class="stats-row">
      <a-col :span="6">
        <a-card>
          <a-statistic :title="t('lowcode.processMonitor.statActive')" :value="stats.activeInstances" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card>
          <a-statistic :title="t('lowcode.processMonitor.statCompleted')" :value="stats.completedToday" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card>
          <a-statistic :title="t('lowcode.processMonitor.statPending')" :value="stats.pendingTasks" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card>
          <a-statistic :title="t('lowcode.processMonitor.statOverdue')" :value="stats.overdueTasks" value-style="color: #cf1322" />
        </a-card>
      </a-col>
    </a-row>

    <a-divider />
    <h3>{{ t("lowcode.processMonitor.listTitle") }}</h3>
    <a-table :columns="columns" :data-source="instances" :pagination="pagination" :loading="loading" row-key="id" @change="onTableChange">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="statusColor(record.status)">{{ statusLabel(record.status) }}</a-tag>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-button type="link" @click="viewTrace(record.id)">{{ t("lowcode.processMonitor.viewTrace") }}</a-button>
        </template>
      </template>
    </a-table>

    <a-modal v-model:open="traceVisible" :title="t('lowcode.processMonitor.modalTrace')" width="700px" :footer="null">
      <a-timeline v-if="traceData.length">
        <a-timeline-item
          v-for="node in traceData"
          :key="node.nodeId"
          :color="node.status === 'Completed' ? 'green' : node.status === 'Active' ? 'blue' : 'gray'"
        >
          <p><strong>{{ node.nodeName }}</strong> ({{ node.nodeType }})</p>
          <p v-if="node.assigneeName">{{ t("lowcode.processMonitor.assignee") }}: {{ node.assigneeName }}</p>
          <p>{{ node.startedAt || "-" }} → {{ node.endedAt || t("lowcode.processMonitor.inProgress") }}</p>
          <p v-if="node.comment" style="color: #999">{{ node.comment }}</p>
        </a-timeline-item>
      </a-timeline>
      <a-empty v-else :description="t('lowcode.processMonitor.emptyTrace')" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { getAdminInstancesPaged } from "@/services/api";
import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@/types/api";

const { t } = useI18n();

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

const columns = computed(() => [
  { title: t("lowcode.processMonitor.colFlow"), dataIndex: "flowName", key: "flowName" },
  { title: t("lowcode.processMonitor.colInitiator"), dataIndex: "initiatorUserId", key: "initiatorUserId", width: 120 },
  { title: t("lowcode.processMonitor.colNode"), dataIndex: "currentNodeName", key: "currentNodeName", width: 140 },
  { title: t("lowcode.processMonitor.colStatus"), key: "status", width: 100 },
  { title: t("lowcode.processMonitor.colStarted"), dataIndex: "startedAt", key: "startedAt", width: 180 },
  { title: t("lowcode.processMonitor.colActions"), key: "actions", width: 120 }
]);

const statusLabel = (status: number) => {
  if (status === 0) return t("lowcode.processMonitor.stRunning");
  if (status === 1) return t("lowcode.processMonitor.stDone");
  if (status === 2) return t("lowcode.processMonitor.stRejected");
  if (status === 3) return t("lowcode.processMonitor.stCancelled");
  return t("lowcode.processMonitor.stUnknown", { status });
};

const statusColor = (status: number) => {
  if (status === 0) return "blue";
  if (status === 1) return "green";
  if (status === 2 || status === 3) return "red";
  return "default";
};

const fetchStats = async () => {
  try {
    const resp = await requestApi<ApiResponse<DashboardStats>>("/process-monitor/dashboard");

    if (!isMounted.value) return;
    if (resp.data) {
      Object.assign(stats, resp.data);
    }
  } catch {
    // Stats load failure is non-fatal; list remains usable.
  }
};

const fetchInstances = async () => {
  loading.value = true;
  try {
    const result = await getAdminInstancesPaged({
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
    const resp = await requestApi<ApiResponse<ProcessInstanceTrace>>(`/process-monitor/instances/${instanceId}/trace`);

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
