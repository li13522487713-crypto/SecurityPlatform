<template>
  <a-card title="运行态中心" class="page-card" :loading="loading">
    <template #extra>
      <a-space>
        <a-select v-model:value="statusFilter" style="120px" placeholder="状态" allow-clear>
          <a-select-option value="Running">运行中</a-select-option>
          <a-select-option value="Blocked">阻塞</a-select-option>
          <a-select-option value="Completed">完成</a-select-option>
        </a-select>
        <a-button @click="loadData">查询</a-button>
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
      :title="detail?.flowName || '实例详情'"
      width="480"
      destroy-on-close
    >
      <a-descriptions size="small" :column="1" bordered>
        <a-descriptions-item label="实例ID">{{ detail?.id }}</a-descriptions-item>
        <a-descriptions-item label="状态">{{ detail?.status }}</a-descriptions-item>
        <a-descriptions-item label="当前节点">{{ detail?.currentNode }}</a-descriptions-item>
        <a-descriptions-item label="启动时间">{{ detail?.startedAt }}</a-descriptions-item>
        <a-descriptions-item label="结束时间">{{ detail?.finishedAt || "-" }}</a-descriptions-item>
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
            {{ trace.startedAt }} - {{ trace.endedAt || "..." }} | {{ trace.durationMinutes }} 分钟
          </div>
        </a-timeline-item>
      </a-timeline>

      <a-alert
        v-for="(risk, idx) in detail?.riskHints || []"
        :key="idx"
        type="warning"
        show-icon
        :message="risk"
        style="margin-top: 8px"
      />
    </a-drawer>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { getVisualizationInstances, getVisualizationInstanceDetail } from "@/services/api";
import type { VisualizationInstanceSummary, VisualizationInstanceDetail } from "@/types/api";
import { message } from "ant-design-vue";

const instances = ref<VisualizationInstanceSummary[]>([]);
const loading = ref(false);
const pagination = ref({ current: 1, pageSize: 10, total: 0 });
const statusFilter = ref<string>();
const detailVisible = ref(false);
const detail = ref<VisualizationInstanceDetail>();

const columns = [
  { title: "实例ID", dataIndex: "id", key: "id" },
  { title: "流程", dataIndex: "flowName", key: "flowName" },
  { title: "状态", dataIndex: "status", key: "status" },
  { title: "当前节点", dataIndex: "currentNode", key: "currentNode" },
  { title: "启动时间", dataIndex: "startedAt", key: "startedAt" },
  { title: "耗时(分钟)", dataIndex: "durationMinutes", key: "durationMinutes" }
];

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
  color: #595959;
  font-size: 12px;
}
</style>
