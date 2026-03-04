<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-title">{{ t("scheduledJobs.title") }}</h2>
      <a-button @click="load" :loading="loading">{{ t("scheduledJobs.refresh") }}</a-button>
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
            {{ record.isEnabled ? t("scheduledJobs.statusEnabled") : t("scheduledJobs.statusDisabled") }}
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
              :title="t('scheduledJobs.triggerConfirm')"
              :ok-text="t('scheduledJobs.triggerOk')"
              :cancel-text="t('scheduledJobs.cancel')"
              @confirm="handleTrigger(record.id)"
            >
              <a-button type="link" size="small">{{ t("scheduledJobs.runNow") }}</a-button>
            </a-popconfirm>
            <a-button type="link" size="small" @click="openExecutionHistory(record)">{{ t("scheduledJobs.history") }}</a-button>
            <a-popconfirm
              v-if="record.isEnabled"
              :title="t('scheduledJobs.disableConfirm')"
              :ok-text="t('scheduledJobs.disableOk')"
              :cancel-text="t('scheduledJobs.cancel')"
              @confirm="handleDisable(record.id)"
            >
              <a-button type="link" size="small" danger>{{ t("scheduledJobs.disable") }}</a-button>
            </a-popconfirm>
            <a-popconfirm
              v-else
              :title="t('scheduledJobs.enableConfirm')"
              :ok-text="t('scheduledJobs.enableOk')"
              :cancel-text="t('scheduledJobs.cancel')"
              @confirm="handleEnable(record.id)"
            >
              <a-button type="link" size="small">{{ t("scheduledJobs.enable") }}</a-button>
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
            {{ typeof record.durationMilliseconds === 'number' ? t('scheduledJobs.durationMs', { duration: record.durationMilliseconds }) : '-' }}
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
import { useI18n } from "vue-i18n";
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
const historyDrawerTitle = ref("");
const selectedHistoryJobId = ref<string>("");
const historyLoading = ref(false);
const executionHistoryItems = ref<ScheduledJobExecutionDto[]>([]);
const historyTotal = ref(0);
const historyPageIndex = ref(1);
const historyPageSize = ref(10);
const { t } = useI18n();
historyDrawerTitle.value = t("scheduledJobs.history");

const columns = [
  { title: t("scheduledJobs.colJobId"), dataIndex: "id", key: "id" },
  { title: t("scheduledJobs.colCron"), dataIndex: "cronExpression", key: "cronExpression" },
  { title: t("scheduledJobs.colQueue"), dataIndex: "queue", key: "queue" },
  { title: t("scheduledJobs.colStatus"), key: "status" },
  { title: t("scheduledJobs.colLastRunAt"), key: "lastRunAt" },
  { title: t("scheduledJobs.colLastRunStatus"), key: "lastRunStatus" },
  { title: t("scheduledJobs.colNextRunAt"), key: "nextRunAt" },
  { title: t("scheduledJobs.colActions"), key: "actions" }
];

const historyColumns = [
  { title: t("scheduledJobs.colExecutionId"), dataIndex: "jobId", key: "jobId", width: 180 },
  { title: t("scheduledJobs.colState"), key: "state", width: 120 },
  { title: t("scheduledJobs.colCreatedAt"), key: "createdAt", width: 170 },
  { title: t("scheduledJobs.colStartedAt"), key: "startedAt", width: 170 },
  { title: t("scheduledJobs.colFinishedAt"), key: "finishedAt", width: 170 },
  { title: t("scheduledJobs.colDuration"), key: "durationMilliseconds", width: 120 },
  { title: t("scheduledJobs.colError"), key: "errorMessage" }
];

const load = async () => {
  loading.value = true;
  try {
    const query = `?pageIndex=${pageIndex.value}&pageSize=${pageSize.value}`;
    const response = await requestApi<ApiResponse<PagedResult<ScheduledJobDto>>>(`/scheduled-jobs${query}`);
    items.value = response.data?.items ?? [];
    total.value = response.data?.total ?? 0;
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : t("scheduledJobs.loadFailed"));
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
    message.success(t("scheduledJobs.triggerSuccess"));
    load();
  } catch {
    message.error(t("scheduledJobs.triggerFailed"));
  }
};

const handleDisable = async (jobId: string) => {
  try {
    await requestApi<ApiResponse<object>>(`/scheduled-jobs/${jobId}/disable`, { method: "PUT" });
    message.success(t("scheduledJobs.disableSuccess"));
    load();
  } catch {
    message.error(t("scheduledJobs.operationFailed"));
  }
};

const handleEnable = async (jobId: string) => {
  try {
    await requestApi<ApiResponse<object>>(`/scheduled-jobs/${jobId}/enable`, { method: "PUT" });
    message.success(t("scheduledJobs.enableSuccess"));
    load();
  } catch {
    message.error(t("scheduledJobs.operationFailed"));
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
    message.error(e instanceof Error ? e.message : t("scheduledJobs.loadHistoryFailed"));
  } finally {
    historyLoading.value = false;
  }
};

const openExecutionHistory = (job: ScheduledJobDto) => {
  selectedHistoryJobId.value = job.id;
  historyDrawerTitle.value = t("scheduledJobs.historyTitle", { id: job.id });
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
