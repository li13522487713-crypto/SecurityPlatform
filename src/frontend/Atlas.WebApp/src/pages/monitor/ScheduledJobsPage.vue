<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-title">定时任务</h2>
      <a-button @click="load" :loading="loading">刷新</a-button>
    </div>

    <a-table
      :columns="columns"
      :data-source="items"
      :loading="loading"
      :pagination="{ total, current: pageIndex, pageSize, showQuickJumper: true, onChange: onPageChange }"
      row-key="id"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="record.isEnabled ? 'green' : 'default'">
            {{ record.isEnabled ? "启用" : "已禁用" }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'lastRunStatus'">
          <a-tag :color="statusColor(record.lastRunStatus)">
            {{ record.lastRunStatus ?? '-' }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'lastRunAt'">
          {{ record.lastRunAt ? formatTime(record.lastRunAt) : '-' }}
        </template>
        <template v-else-if="column.key === 'nextRunAt'">
          {{ record.nextRunAt ? formatTime(record.nextRunAt) : '-' }}
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-popconfirm
              title="确认立即触发该任务？"
              ok-text="触发"
              cancel-text="取消"
              @confirm="handleTrigger(record.id)"
            >
              <a-button type="link" size="small">立即执行</a-button>
            </a-popconfirm>
            <a-button type="link" size="small" @click="openExecutionHistory(record)">执行历史</a-button>
            <a-popconfirm
              v-if="record.isEnabled"
              title="确认禁用该任务？"
              ok-text="禁用"
              cancel-text="取消"
              @confirm="handleDisable(record.id)"
            >
              <a-button type="link" size="small" danger>禁用</a-button>
            </a-popconfirm>
            <a-popconfirm
              v-else
              title="确认启用该任务？"
              ok-text="启用"
              cancel-text="取消"
              @confirm="handleEnable(record.id)"
            >
              <a-button type="link" size="small">启用</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-drawer
      v-model:open="historyDrawerVisible"
      :title="historyDrawerTitle"
      placement="right"
      width="760"
      :destroy-on-close="true"
    >
      <a-table
        :columns="historyColumns"
        :data-source="executionHistoryItems"
        :loading="historyLoading"
        row-key="jobId"
        :pagination="{
          total: historyTotal,
          current: historyPageIndex,
          pageSize: historyPageSize,
          showQuickJumper: true,
          onChange: onHistoryPageChange
        }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'state'">
            <a-tag :color="statusColor(record.state)">{{ record.state ?? '-' }}</a-tag>
          </template>
          <template v-else-if="column.key === 'createdAt'">
            {{ record.createdAt ? formatTime(record.createdAt) : '-' }}
          </template>
          <template v-else-if="column.key === 'startedAt'">
            {{ record.startedAt ? formatTime(record.startedAt) : '-' }}
          </template>
          <template v-else-if="column.key === 'finishedAt'">
            {{ record.finishedAt ? formatTime(record.finishedAt) : '-' }}
          </template>
          <template v-else-if="column.key === 'durationMilliseconds'">
            {{ typeof record.durationMilliseconds === 'number' ? `${record.durationMilliseconds} ms` : '-' }}
          </template>
          <template v-else-if="column.key === 'errorMessage'">
            <span class="error-message">{{ record.errorMessage || '-' }}</span>
          </template>
        </template>
      </a-table>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { message } from "ant-design-vue";
import { requestApi } from "@/services/api-core";
import type { ApiResponse, PagedResult } from "@/types/api";

interface ScheduledJobDto {
  id: string;
  name: string;
  cronExpression: string;
  queue: string;
  isEnabled: boolean;
  lastRunAt?: string;
  lastRunStatus?: string;
  nextRunAt?: string;
}

interface ScheduledJobExecutionDto {
  jobId: string;
  createdAt?: string;
  startedAt?: string;
  finishedAt?: string;
  durationMilliseconds?: number;
  state?: string;
  errorMessage?: string;
}

const loading = ref(false);
const items = ref<ScheduledJobDto[]>([]);
const total = ref(0);
const pageIndex = ref(1);
const pageSize = ref(20);
const historyDrawerVisible = ref(false);
const historyDrawerTitle = ref("执行历史");
const selectedHistoryJobId = ref<string>("");
const historyLoading = ref(false);
const executionHistoryItems = ref<ScheduledJobExecutionDto[]>([]);
const historyTotal = ref(0);
const historyPageIndex = ref(1);
const historyPageSize = ref(10);

const columns = [
  { title: "任务 ID", dataIndex: "id", key: "id" },
  { title: "Cron 表达式", dataIndex: "cronExpression", key: "cronExpression" },
  { title: "队列", dataIndex: "queue", key: "queue" },
  { title: "状态", key: "status" },
  { title: "上次执行", key: "lastRunAt" },
  { title: "上次状态", key: "lastRunStatus" },
  { title: "下次执行", key: "nextRunAt" },
  { title: "操作", key: "actions" }
];

const historyColumns = [
  { title: "执行实例ID", dataIndex: "jobId", key: "jobId", width: 180 },
  { title: "状态", key: "state", width: 120 },
  { title: "创建时间", key: "createdAt", width: 170 },
  { title: "开始时间", key: "startedAt", width: 170 },
  { title: "结束时间", key: "finishedAt", width: 170 },
  { title: "耗时", key: "durationMilliseconds", width: 120 },
  { title: "错误信息", key: "errorMessage" }
];

const load = async () => {
  loading.value = true;
  try {
    const query = `?pageIndex=${pageIndex.value}&pageSize=${pageSize.value}`;
    const response = await requestApi<ApiResponse<PagedResult<ScheduledJobDto>>>(`/scheduled-jobs${query}`);
    items.value = response.data?.items ?? [];
    total.value = response.data?.total ?? 0;
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : "加载失败");
  } finally {
    loading.value = false;
  }
};

const onPageChange = (page: number) => {
  pageIndex.value = page;
  load();
};

const handleTrigger = async (jobId: string) => {
  try {
    await requestApi<ApiResponse<object>>(`/scheduled-jobs/${jobId}/trigger`, { method: "POST" });
    message.success("触发成功");
    load();
  } catch {
    message.error("触发失败");
  }
};

const handleDisable = async (jobId: string) => {
  try {
    await requestApi<ApiResponse<object>>(`/scheduled-jobs/${jobId}/disable`, { method: "PUT" });
    message.success("已禁用");
    load();
  } catch {
    message.error("操作失败");
  }
};

const handleEnable = async (jobId: string) => {
  try {
    await requestApi<ApiResponse<object>>(`/scheduled-jobs/${jobId}/enable`, { method: "PUT" });
    message.success("已启用");
    load();
  } catch {
    message.error("操作失败");
  }
};

const loadExecutionHistory = async () => {
  if (!selectedHistoryJobId.value) return;
  historyLoading.value = true;
  try {
    const query = `?pageIndex=${historyPageIndex.value}&pageSize=${historyPageSize.value}`;
    const response = await requestApi<ApiResponse<PagedResult<ScheduledJobExecutionDto>>>(
      `/scheduled-jobs/${selectedHistoryJobId.value}/executions${query}`
    );
    executionHistoryItems.value = response.data?.items ?? [];
    historyTotal.value = response.data?.total ?? 0;
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : "加载执行历史失败");
  } finally {
    historyLoading.value = false;
  }
};

const openExecutionHistory = (job: ScheduledJobDto) => {
  selectedHistoryJobId.value = job.id;
  historyDrawerTitle.value = `执行历史 - ${job.id}`;
  historyPageIndex.value = 1;
  historyDrawerVisible.value = true;
  loadExecutionHistory();
};

const onHistoryPageChange = (page: number) => {
  historyPageIndex.value = page;
  loadExecutionHistory();
};

const statusColor = (status?: string) => {
  const map: Record<string, string> = {
    Succeeded: "green",
    Failed: "red",
    Processing: "blue",
    Enqueued: "orange"
  };
  return status ? (map[status] ?? "default") : "default";
};

const formatTime = (iso: string) => {
  try { return new Date(iso).toLocaleString("zh-CN"); } catch { return iso; }
};

onMounted(load);
</script>

<style scoped>
.page-container { padding: 24px; }
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}
.page-title { margin: 0; font-size: 18px; font-weight: 600; }

.error-message {
  color: #cf1322;
}
</style>
