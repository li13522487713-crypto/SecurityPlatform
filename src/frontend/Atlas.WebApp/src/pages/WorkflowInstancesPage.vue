<template>
  <a-card class="page-card">
    <template #title>工作流实例监控</template>
    <template #extra>
      <a-space>
        <a-button @click="loadInstances">
          <template #icon><ReloadOutlined /></template>
          刷新
        </a-button>
      </a-space>
    </template>

    <a-table
      :columns="columns"
      :data-source="instances"
      :loading="loading"
      :pagination="{
        current: pageIndex,
        pageSize: pageSize,
        total: total,
        showSizeChanger: true,
        showQuickJumper: true,
        showTotal: (total: number) => `共 ${total} 条`
      }"
      @change="handleTableChange"
      :row-key="(record: WorkflowInstanceListItem) => record.id"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="getStatusColor(record.status)">
            {{ getStatusText(record.status) }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'createTime'">
          {{ formatDateTime(record.createTime) }}
        </template>
        <template v-else-if="column.key === 'completeTime'">
          {{ record.completeTime ? formatDateTime(record.completeTime) : "-" }}
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button type="link" size="small" @click="handleViewDetail(record)">
              查看详情
            </a-button>
            <a-button
              type="link"
              size="small"
              @click="handleSuspend(record.id)"
              v-if="record.status === 'Runnable' || record.status === 'Running'"
              danger
            >
              挂起
            </a-button>
            <a-button
              type="link"
              size="small"
              @click="handleResume(record.id)"
              v-if="record.status === 'Suspended'"
            >
              恢复
            </a-button>
            <a-button
              type="link"
              size="small"
              @click="handleTerminate(record.id)"
              v-if="record.status !== 'Complete' && record.status !== 'Terminated'"
              danger
            >
              终止
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="detailDrawerVisible"
      title="工作流实例详情"
      placement="right"
      width="800"
      @close="handleCloseDetail"
    >
      <div v-if="selectedInstance">
        <a-descriptions bordered :column="1">
          <a-descriptions-item label="实例ID">{{ selectedInstance.id }}</a-descriptions-item>
          <a-descriptions-item label="工作流ID">{{ selectedInstance.workflowDefinitionId }}</a-descriptions-item>
          <a-descriptions-item label="版本">{{ selectedInstance.version }}</a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag :color="getStatusColor(selectedInstance.status)">
              {{ getStatusText(selectedInstance.status) }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="创建时间">{{ formatDateTime(selectedInstance.createTime) }}</a-descriptions-item>
          <a-descriptions-item label="完成时间">
            {{ selectedInstance.completeTime ? formatDateTime(selectedInstance.completeTime) : "-" }}
          </a-descriptions-item>
          <a-descriptions-item label="引用标识">{{ selectedInstance.reference || "-" }}</a-descriptions-item>
        </a-descriptions>

        <a-divider orientation="left">执行指针（步骤级监控）</a-divider>
        <a-timeline>
          <a-timeline-item
            v-for="pointer in executionPointers"
            :key="pointer.id"
            :color="getPointerColor(pointer.status)"
          >
            <template #dot>
              <LoadingOutlined v-if="pointer.status === 'Running'" />
              <ClockCircleOutlined v-else-if="pointer.status === 'Sleeping'" />
              <HourglassOutlined v-else-if="pointer.status === 'WaitingForEvent'" />
              <CheckCircleOutlined v-else-if="pointer.status === 'Complete'" />
              <CloseCircleOutlined v-else />
            </template>
            <div>
              <strong>{{ pointer.stepName }}</strong>
              <a-tag :color="getPointerColor(pointer.status)" style="margin-left: 8px">
                {{ getPointerStatusText(pointer.status) }}
              </a-tag>
            </div>
            <div style="font-size: 12px; color: #666; margin-top: 4px">
              <div v-if="pointer.startTime">开始: {{ formatDateTime(pointer.startTime) }}</div>
              <div v-if="pointer.endTime">结束: {{ formatDateTime(pointer.endTime) }}</div>
              <div v-if="pointer.sleepUntil">睡眠到: {{ formatDateTime(pointer.sleepUntil) }}</div>
              <div v-if="pointer.eventName">等待事件: {{ pointer.eventName }}</div>
              <div v-if="pointer.retryCount > 0">重试次数: {{ pointer.retryCount }}</div>
              <div v-if="pointer.errorMessage" style="color: red">错误: {{ pointer.errorMessage }}</div>
            </div>
          </a-timeline-item>
        </a-timeline>

        <a-space style="margin-top: 16px">
          <a-button @click="handleRefreshPointers" :loading="pointersLoading">
            <template #icon><ReloadOutlined /></template>
            刷新执行状态
          </a-button>
          <a-switch v-model:checked="autoRefresh" /> 自动刷新（每3秒）
        </a-space>
      </div>
    </a-drawer>
  </a-card>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute } from "vue-router";
import {
  getWorkflowInstances,
  getWorkflowInstance,
  getExecutionPointers,
  suspendWorkflow,
  resumeWorkflow,
  terminateWorkflow
} from "@/services/api";
import type { WorkflowInstanceListItem, WorkflowInstanceResponse, ExecutionPointerResponse } from "@/types/api";
import { message } from "ant-design-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import {
  ReloadOutlined,
  LoadingOutlined,
  ClockCircleOutlined,
  HourglassOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined
} from "@ant-design/icons-vue";

const route = useRoute();
const loading = ref(false);
const instances = ref<WorkflowInstanceListItem[]>([]);
const pageIndex = ref(1);
const pageSize = ref(10);
const total = ref(0);
const detailDrawerVisible = ref(false);
const selectedInstance = ref<WorkflowInstanceResponse | null>(null);
const executionPointers = ref<ExecutionPointerResponse[]>([]);
const pointersLoading = ref(false);
const autoRefresh = ref(false);
let refreshTimer: number | null = null;

const columns = [
  { title: "实例ID", dataIndex: "id", key: "id", ellipsis: true, width: 200 },
  { title: "工作流ID", dataIndex: "workflowDefinitionId", key: "workflowDefinitionId" },
  { title: "版本", dataIndex: "version", key: "version", width: 80 },
  { title: "状态", dataIndex: "status", key: "status", width: 120 },
  { title: "创建时间", dataIndex: "createTime", key: "createTime", width: 180 },
  { title: "完成时间", dataIndex: "completeTime", key: "completeTime", width: 180 },
  { title: "操作", key: "actions", width: 280 }
];

const loadInstances = async () => {
  loading.value = true;
  try {
    const result  = await getWorkflowInstances({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value
    });

    if (!isMounted.value) return;
    instances.value = result.items;
    total.value = result.total;
  } catch (err) {
    message.error(err instanceof Error ? err.message : "加载失败");
  } finally {
    loading.value = false;
  }
};

const handleTableChange = (pagination: TablePaginationConfig) => {
  pageIndex.value = pagination.current ?? 1;
  pageSize.value = pagination.pageSize ?? 10;
  loadInstances();
};

const handleViewDetail = async (record: WorkflowInstanceListItem) => {
  try {
    selectedInstance.value = await getWorkflowInstance(record.id);

    if (!isMounted.value) return;
    await loadExecutionPointers(record.id);

    if (!isMounted.value) return;
    detailDrawerVisible.value = true;
  } catch (err) {
    message.error(err instanceof Error ? err.message : "加载详情失败");
  }
};

const loadExecutionPointers = async (instanceId: string) => {
  pointersLoading.value = true;
  try {
    executionPointers.value = await getExecutionPointers(instanceId);

    if (!isMounted.value) return;
  } catch (err) {
    message.error(err instanceof Error ? err.message : "加载执行指针失败");
  } finally {
    pointersLoading.value = false;
  }
};

const handleRefreshPointers = async () => {
  if (selectedInstance.value) {
    await loadExecutionPointers(selectedInstance.value.id);

    if (!isMounted.value) return;
  }
};

const handleCloseDetail = () => {
  detailDrawerVisible.value = false;
  selectedInstance.value = null;
  executionPointers.value = [];
  autoRefresh.value = false;
};

const handleSuspend = async (instanceId: string) => {
  try {
    await suspendWorkflow(instanceId);

    if (!isMounted.value) return;
    message.success("工作流已挂起");
    loadInstances();
  } catch (err) {
    message.error(err instanceof Error ? err.message : "挂起失败");
  }
};

const handleResume = async (instanceId: string) => {
  try {
    await resumeWorkflow(instanceId);

    if (!isMounted.value) return;
    message.success("工作流已恢复");
    loadInstances();
  } catch (err) {
    message.error(err instanceof Error ? err.message : "恢复失败");
  }
};

const handleTerminate = async (instanceId: string) => {
  try {
    await terminateWorkflow(instanceId);

    if (!isMounted.value) return;
    message.success("工作流已终止");
    loadInstances();
  } catch (err) {
    message.error(err instanceof Error ? err.message : "终止失败");
  }
};

const getStatusColor = (status: string): string => {
  const colorMap: Record<string, string> = {
    Runnable: "blue",
    Running: "processing",
    Complete: "success",
    Suspended: "warning",
    Terminated: "error"
  };
  return colorMap[status] || "default";
};

const getStatusText = (status: string): string => {
  const textMap: Record<string, string> = {
    Runnable: "可运行",
    Running: "运行中",
    Complete: "已完成",
    Suspended: "已挂起",
    Terminated: "已终止"
  };
  return textMap[status] || status;
};

const getPointerColor = (status: string): string => {
  const colorMap: Record<string, string> = {
    Running: "blue",
    Complete: "green",
    Sleeping: "orange",
    WaitingForEvent: "cyan",
    Failed: "red",
    Pending: "gray"
  };
  return colorMap[status] || "gray";
};

const getPointerStatusText = (status: string): string => {
  const textMap: Record<string, string> = {
    Running: "运行中",
    Complete: "已完成",
    Sleeping: "休眠中",
    WaitingForEvent: "等待事件",
    Failed: "失败",
    Pending: "待执行"
  };
  return textMap[status] || status;
};

const formatDateTime = (dateTime: string): string => {
  return new Date(dateTime).toLocaleString("zh-CN");
};

// 自动刷新逻辑
watch(autoRefresh, (newVal) => {
  if (newVal && selectedInstance.value) {
    refreshTimer = window.setInterval(() => {
      if (selectedInstance.value) {
        loadExecutionPointers(selectedInstance.value.id);
      }
    }, 3000);
  } else {
    if (refreshTimer) {
      clearInterval(refreshTimer);
      refreshTimer = null;
    }
  }
});

onMounted(async () => {
  await loadInstances();

  if (!isMounted.value) return;

  // 如果 URL 中有 instanceId 参数，自动打开详情
  const instanceId = route.query.instanceId as string;
  if (instanceId) {
    const instance = instances.value.find((i) => i.id === instanceId);
    if (instance) {
      handleViewDetail(instance);
    }
  }
});

onBeforeUnmount(() => {
  if (refreshTimer) {
    clearInterval(refreshTimer);
  }
});
</script>

<style scoped>
.page-card {
  margin: 16px;
}
</style>
