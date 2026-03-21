<template>
  <a-card :title="t('visualization.runtimeTitle')" class="page-card" :loading="loading">
    <template #extra>
      <a-space>
        <a-select
          v-model:value="statusFilter"
          style="120px"
          :placeholder="t('visualization.placeholderStatus')"
          allow-clear
        >
          <a-select-option value="Running">{{ t("visualization.statusRunning") }}</a-select-option>
          <a-select-option value="Blocked">{{ t("visualization.statusBlocked") }}</a-select-option>
          <a-select-option value="Completed">{{ t("visualization.statusCompleted") }}</a-select-option>
        </a-select>
        <a-button @click="loadData">{{ t("visualization.search") }}</a-button>
      </a-space>
    </template>

    <a-table
      :data-source="instances"
      :columns="columns"
      :pagination="pagination"
      row-key="id"
      size="middle"
      @change="handleTableChange"
      @row-click="handleRowClick"
    />

    <a-drawer
      v-model:open="detailVisible"
      :title="detail?.flowName || t('visualization.detailTitle')"
      width="480"
      destroy-on-close
    >
      <a-descriptions size="small" :column="1" bordered>
        <a-descriptions-item :label="t('visualization.labelInstanceId')">{{ detail?.id }}</a-descriptions-item>
        <a-descriptions-item :label="t('visualization.labelStatus')">{{ detail?.status }}</a-descriptions-item>
        <a-descriptions-item :label="t('visualization.labelCurrentNode')">{{ detail?.currentNode }}</a-descriptions-item>
        <a-descriptions-item :label="t('visualization.labelStartedAt')">{{ detail?.startedAt }}</a-descriptions-item>
        <a-descriptions-item :label="t('visualization.labelFinishedAt')">{{ detail?.finishedAt || "-" }}</a-descriptions-item>
      </a-descriptions>

      <a-divider />

      <a-timeline>
        <a-timeline-item
          v-for="trace in detail?.trace || []"
          :key="trace.nodeId"
          :color="trace.status === 'Completed' ? 'green' : trace.status === 'Running' ? 'blue' : 'red'"
        >
          <div class="trace-title">{{ trace.name }}（{{ trace.status }}）</div>
          <div class="trace-meta">
            {{ trace.startedAt }} - {{ trace.endedAt || "..." }} |
            {{ t("visualization.durationMinutes", { n: trace.durationMinutes }) }}
          </div>
        </a-timeline-item>
      </a-timeline>

      <a-alert
        v-for="risk in detail?.riskHints || []"
        :key="risk"
        type="warning"
        show-icon
        :message="risk"
        style="margin-top: 8px"
      />
    </a-drawer>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => {
  isMounted.value = true;
});
onUnmounted(() => {
  isMounted.value = false;
});

import { getVisualizationInstances, getVisualizationInstanceDetail } from "@/services/api";
import type { VisualizationInstanceSummary, VisualizationInstanceDetail } from "@/types/api";
import { message } from "ant-design-vue";

const { t } = useI18n();

const instances = ref<VisualizationInstanceSummary[]>([]);
const loading = ref(false);
const pagination = ref({ current: 1, pageSize: 10, total: 0 });
const statusFilter = ref<string>();
const detailVisible = ref(false);
const detail = ref<VisualizationInstanceDetail>();

const columns = computed(() => [
  { title: t("visualization.colInstanceId"), dataIndex: "id", key: "id" },
  { title: t("visualization.colFlow"), dataIndex: "flowName", key: "flowName" },
  { title: t("visualization.labelStatus"), dataIndex: "status", key: "status" },
  { title: t("visualization.labelCurrentNode"), dataIndex: "currentNode", key: "currentNode" },
  { title: t("visualization.labelStartedAt"), dataIndex: "startedAt", key: "startedAt" },
  { title: t("visualization.colDurationMin"), dataIndex: "durationMinutes", key: "durationMinutes" }
]);

interface TablePagination {
  current?: number;
  pageSize?: number;
}

const loadData = async () => {
  try {
    loading.value = true;
    const result = await getVisualizationInstances(
      {
        pageIndex: pagination.value.current,
        pageSize: pagination.value.pageSize
      },
      {
        status: statusFilter.value
      }
    );

    if (!isMounted.value) return;
    instances.value = result.items;
    pagination.value.total = result.total;
  } catch (err) {
    message.error((err as Error).message);
  } finally {
    loading.value = false;
  }
};

const handleTableChange = (pager: TablePagination) => {
  pagination.value = {
    ...pagination.value,
    current: pager.current ?? pagination.value.current,
    pageSize: pager.pageSize ?? pagination.value.pageSize
  };
  loadData();
};

const handleRowClick = async (record: VisualizationInstanceSummary) => {
  try {
    detailVisible.value = true;
    detail.value = await getVisualizationInstanceDetail(record.id);

    if (!isMounted.value) return;
  } catch (err) {
    message.error((err as Error).message);
  }
};

onMounted(loadData);
</script>

<style scoped>
.trace-title {
  font-weight: 600;
}
.trace-meta {
  color: var(--color-text-secondary);
  font-size: 12px;
}
</style>
