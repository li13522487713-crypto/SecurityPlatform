<template>
  <CrudPageLayout :title="t('workflow.instancesTitle')">
    <template #toolbar-actions>
      <a-space>
        <a-button @click="loadInstances">
          <template #icon><ReloadOutlined /></template>
          {{ t('workflow.refresh') }}
        </a-button>
      </a-space>
    </template>

    <template #table>
      <a-table
        :columns="columns"
        :data-source="instances"
        :loading="loading"
        :pagination="tablePagination"
        :row-key="(record: WorkflowInstanceListItem) => record.id"
        @change="handleTableChange"
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
                {{ t('workflow.viewDetail') }}
              </a-button>
              <a-button
                v-if="record.status === 'Runnable' || record.status === 'Running'"
                type="link"
                size="small"
                danger
                @click="handleSuspend(record.id)"
              >
                {{ t('workflow.suspend') }}
              </a-button>
              <a-button
                v-if="record.status === 'Suspended'"
                type="link"
                size="small"
                @click="handleResume(record.id)"
              >
                {{ t('workflow.resume') }}
              </a-button>
              <a-button
                v-if="record.status !== 'Complete' && record.status !== 'Terminated'"
                type="link"
                size="small"
                danger
                @click="handleTerminate(record.id)"
              >
                {{ t('workflow.terminate') }}
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <template #extra-drawers>
      <a-drawer
        v-model:open="detailDrawerVisible"
        :title="t('workflow.drawerTitle')"
        placement="right"
        width="800"
        @close="handleCloseDetail"
      >
        <div v-if="selectedInstance">
          <a-descriptions bordered :column="1">
            <a-descriptions-item :label="t('workflow.labelInstanceId')">{{ selectedInstance.id }}</a-descriptions-item>
            <a-descriptions-item :label="t('workflow.labelWorkflowId')">{{ selectedInstance.workflowDefinitionId }}</a-descriptions-item>
            <a-descriptions-item :label="t('workflow.labelVersion')">{{ selectedInstance.version }}</a-descriptions-item>
            <a-descriptions-item :label="t('workflow.labelStatus')">
              <a-tag :color="getStatusColor(selectedInstance.status)">
                {{ getStatusText(selectedInstance.status) }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item :label="t('workflow.labelCreatedAt')">{{ formatDateTime(selectedInstance.createTime) }}</a-descriptions-item>
            <a-descriptions-item :label="t('workflow.labelCompletedAt')">
              {{ selectedInstance.completeTime ? formatDateTime(selectedInstance.completeTime) : "-" }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('workflow.labelReference')">{{ selectedInstance.reference || "-" }}</a-descriptions-item>
          </a-descriptions>

          <a-divider orientation="left">{{ t('workflow.dividerPointers') }}</a-divider>
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
                <div v-if="pointer.startTime">{{ t('workflow.pointerStart') }} {{ formatDateTime(pointer.startTime) }}</div>
                <div v-if="pointer.endTime">{{ t('workflow.pointerEnd') }} {{ formatDateTime(pointer.endTime) }}</div>
                <div v-if="pointer.sleepUntil">{{ t('workflow.pointerSleep') }} {{ formatDateTime(pointer.sleepUntil) }}</div>
                <div v-if="pointer.eventName">{{ t('workflow.pointerEvent') }} {{ pointer.eventName }}</div>
                <div v-if="pointer.retryCount > 0">{{ t('workflow.pointerRetry') }} {{ pointer.retryCount }}</div>
                <div v-if="pointer.errorMessage" style="color: red">{{ t('workflow.pointerError') }} {{ pointer.errorMessage }}</div>
              </div>
            </a-timeline-item>
          </a-timeline>

          <a-space style="margin-top: 16px">
            <a-button :loading="pointersLoading" @click="handleRefreshPointers">
              <template #icon><ReloadOutlined /></template>
              {{ t('workflow.refreshPointers') }}
            </a-button>
            <a-switch v-model:checked="autoRefresh" /> {{ t('workflow.autoRefresh') }}
          </a-space>
        </div>
      </a-drawer>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch, onUnmounted, computed } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute } from "vue-router";
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
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

const { t } = useI18n();
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

const columns = computed(() => [
  { title: t('workflow.colInstanceId'), dataIndex: "id", key: "id", ellipsis: true, width: 200 },
  { title: t('workflow.colWorkflowId'), dataIndex: "workflowDefinitionId", key: "workflowDefinitionId" },
  { title: t('workflow.colVersion'), dataIndex: "version", key: "version", width: 80 },
  { title: t('workflow.colStatus'), dataIndex: "status", key: "status", width: 120 },
  { title: t('workflow.colCreatedAt'), dataIndex: "createTime", key: "createTime", width: 180 },
  { title: t('workflow.colCompletedAt'), dataIndex: "completeTime", key: "completeTime", width: 180 },
  { title: t('workflow.colActions'), key: "actions", width: 280 }
]);

const tablePagination = computed(() => ({
  current: pageIndex.value,
  pageSize: pageSize.value,
  total: total.value,
  showSizeChanger: true,
  showQuickJumper: true,
  showTotal: (n: number) => t('crud.totalItems', { total: n })
}));

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
    message.error(err instanceof Error ? err.message : t('workflow.loadFailed'));
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
    message.error(err instanceof Error ? err.message : t('workflow.loadDetailFailed'));
  }
};

const loadExecutionPointers = async (instanceId: string) => {
  pointersLoading.value = true;
  try {
    executionPointers.value = await getExecutionPointers(instanceId);

    if (!isMounted.value) return;
  } catch (err) {
    message.error(err instanceof Error ? err.message : t('workflow.loadPointersFailed'));
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
    message.success(t('workflow.suspendedOk'));
    loadInstances();
  } catch (err) {
    message.error(err instanceof Error ? err.message : t('workflow.suspendFailed'));
  }
};

const handleResume = async (instanceId: string) => {
  try {
    await resumeWorkflow(instanceId);

    if (!isMounted.value) return;
    message.success(t('workflow.resumedOk'));
    loadInstances();
  } catch (err) {
    message.error(err instanceof Error ? err.message : t('workflow.resumeFailed'));
  }
};

const handleTerminate = async (instanceId: string) => {
  try {
    await terminateWorkflow(instanceId);

    if (!isMounted.value) return;
    message.success(t('workflow.terminatedOk'));
    loadInstances();
  } catch (err) {
    message.error(err instanceof Error ? err.message : t('workflow.terminateFailed'));
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
    Runnable: t('workflow.wfRunnable'),
    Running: t('workflow.wfRunning'),
    Complete: t('workflow.wfComplete'),
    Suspended: t('workflow.wfSuspended'),
    Terminated: t('workflow.wfTerminated')
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
    Running: t('workflow.ptrRunning'),
    Complete: t('workflow.ptrComplete'),
    Sleeping: t('workflow.ptrSleeping'),
    WaitingForEvent: t('workflow.ptrWaitingEvent'),
    Failed: t('workflow.ptrFailed'),
    Pending: t('workflow.ptrPending')
  };
  return textMap[status] || status;
};

const formatDateTime = (dateTime: string): string => {
  return new Date(dateTime).toLocaleString();
};

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
