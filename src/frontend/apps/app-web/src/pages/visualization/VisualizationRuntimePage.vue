<template>
  <a-card :title="t('visualization.runtimeTitle')" class="page-card">
    <template #extra>
      <a-space>
        <a-select
          v-model:value="statusFilter"
          style="width: 140px"
          :placeholder="t('visualization.placeholderStatus')"
          allow-clear
        >
          <a-select-option value="Running">{{ t("visualization.statusRunning") }}</a-select-option>
          <a-select-option value="Blocked">{{ t("visualization.statusBlocked") }}</a-select-option>
          <a-select-option value="Completed">{{ t("visualization.statusCompleted") }}</a-select-option>
        </a-select>
        <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
        <a-button type="primary" @click="handleSearch">{{ t("common.search") }}</a-button>
      </a-space>
    </template>

    <a-table
      :columns="columns"
      :data-source="instances"
      :pagination="pagination"
      :loading="loading"
      row-key="id"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="statusColor(record.status)">{{ statusText(record.status) }}</a-tag>
        </template>
        <template v-else-if="column.key === 'startedAt'">{{ formatTime(record.startedAt) }}</template>
        <template v-else-if="column.key === 'durationMinutes'">
          {{ record.durationMinutes ?? "-" }}
        </template>
        <template v-else-if="column.key === 'action'">
          <a-button type="link" size="small" @click="openDetail(record.id)">
            {{ t("visualization.openDetail") }}
          </a-button>
        </template>
      </template>
    </a-table>

    <a-drawer
      v-model:open="detailVisible"
      :title="detail?.flowName || t('visualization.detailTitle')"
      width="520"
      destroy-on-close
    >
      <a-descriptions size="small" :column="1" bordered>
        <a-descriptions-item :label="t('visualization.labelInstanceId')">{{ detail?.id }}</a-descriptions-item>
        <a-descriptions-item :label="t('visualization.labelStatus')">
          <a-tag :color="statusColor(detail?.status)">{{ statusText(detail?.status) }}</a-tag>
        </a-descriptions-item>
        <a-descriptions-item :label="t('visualization.labelCurrentNode')">{{ detail?.currentNode || "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('visualization.labelStartedAt')">{{ formatTime(detail?.startedAt) }}</a-descriptions-item>
        <a-descriptions-item :label="t('visualization.labelFinishedAt')">{{ formatTime(detail?.finishedAt) }}</a-descriptions-item>
      </a-descriptions>

      <a-divider />

      <a-timeline>
        <a-timeline-item
          v-for="trace in detail?.trace || []"
          :key="trace.nodeId"
          :color="statusColor(trace.status)"
        >
          <div class="trace-title">{{ trace.name }} ({{ statusText(trace.status) }})</div>
          <div class="trace-meta">
            {{ formatTime(trace.startedAt) }} - {{ formatTime(trace.endedAt) }}
            · {{ t("visualization.durationMinutes", { n: trace.durationMinutes ?? 0 }) }}
          </div>
        </a-timeline-item>
      </a-timeline>

      <a-alert
        v-for="hint in detail?.riskHints || []"
        :key="hint"
        type="warning"
        :message="hint"
        show-icon
        style="margin-top: 8px"
      />
    </a-drawer>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useRoute } from "vue-router";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import {
  getVisualizationInstanceDetail,
  getVisualizationInstances,
  type VisualizationInstanceDetail,
  type VisualizationInstanceSummary,
} from "@/services/api-visualization";

const { t, locale } = useI18n();
const route = useRoute();
const appKey = computed(() => String(route.params.appKey ?? ""));

const columns = computed(() => [
  { title: t("visualization.colInstanceId"), dataIndex: "id", key: "id" },
  { title: t("visualization.colFlow"), dataIndex: "flowName", key: "flowName" },
  { title: t("visualization.labelStatus"), key: "status", width: 120 },
  { title: t("visualization.labelCurrentNode"), dataIndex: "currentNode", key: "currentNode" },
  { title: t("visualization.labelStartedAt"), key: "startedAt", width: 180 },
  { title: t("visualization.colDurationMin"), key: "durationMinutes", width: 120 },
  { title: t("common.actions"), key: "action", width: 110 },
]);

const instances = ref<VisualizationInstanceSummary[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total: number) => String(total),
});
const statusFilter = ref<string | undefined>(undefined);
const detailVisible = ref(false);
const detail = ref<VisualizationInstanceDetail | null>(null);

const formatTime = (iso?: string | null) => {
  if (!iso) {
    return "-";
  }
  const value = new Date(iso);
  if (Number.isNaN(value.getTime())) {
    return iso;
  }
  const language = locale.value === "en-US" ? "en-US" : "zh-CN";
  return value.toLocaleString(language, { hour12: false });
};

const statusText = (status?: string | null) => {
  if (!status) {
    return t("visualization.statusUnknown");
  }
  if (status === "Running") {
    return t("visualization.statusRunning");
  }
  if (status === "Blocked") {
    return t("visualization.statusBlocked");
  }
  if (status === "Completed") {
    return t("visualization.statusCompleted");
  }
  return status;
};

const statusColor = (status?: string | null) => {
  if (status === "Completed") {
    return "green";
  }
  if (status === "Running") {
    return "blue";
  }
  if (status === "Blocked") {
    return "red";
  }
  return "default";
};

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getVisualizationInstances(
      appKey.value,
      {
        pageIndex: pagination.current ?? 1,
        pageSize: pagination.pageSize ?? 10,
      },
      { status: statusFilter.value }
    );
    instances.value = result.items;
    pagination.total = result.total;
  } catch (error) {
    message.error((error as Error).message || t("visualization.loadFailed"));
  } finally {
    loading.value = false;
  }
};

const handleSearch = () => {
  pagination.current = 1;
  void fetchData();
};

const handleReset = () => {
  statusFilter.value = undefined;
  handleSearch();
};

const handleTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  void fetchData();
};

const openDetail = async (id: string) => {
  detailVisible.value = true;
  detail.value = null;
  try {
    detail.value = await getVisualizationInstanceDetail(appKey.value, id);
  } catch (error) {
    message.error((error as Error).message || t("visualization.loadDetailFailed"));
  }
};

onMounted(() => {
  void fetchData();
});
</script>

<style scoped>
.trace-title {
  font-weight: 600;
}

.trace-meta {
  color: rgba(0, 0, 0, 0.45);
  font-size: 12px;
}
</style>
