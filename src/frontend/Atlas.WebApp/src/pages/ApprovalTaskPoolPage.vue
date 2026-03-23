<template>
  <CrudPageLayout :title="t('approvalRuntime.taskPoolTitle')">
    <template #toolbar-actions>
      <a-button type="primary" @click="refresh">{{ t('approvalRuntime.refresh') }}</a-button>
    </template>
    <template #table>
      <a-table
        :columns="columns"
        :data-source="tasks"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'action'">
            <a-button type="link" @click="handleClaim(record)">{{ t('approvalRuntime.claim') }}</a-button>
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag color="blue">{{ t('approvalRuntime.tagPendingClaim') }}</a-tag>
          </template>
        </template>
      </a-table>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue';
import { useI18n } from 'vue-i18n';

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { message } from 'ant-design-vue';
import type { TablePaginationConfig } from 'ant-design-vue';
import { getTaskPool, claimTask } from '@/services/api';
import type { ApprovalTaskResponse } from '@/types/api';
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";

const { t } = useI18n();

const loading = ref(false);
const tasks = ref<ApprovalTaskResponse[]>([]);
const pagination = ref({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showQuickJumper: true
});

const columns = computed(() => [
  { title: t('approvalRuntime.colTaskTitle'), dataIndex: 'title', key: 'title' },
  { title: t('approvalRuntime.colFlowNameShort'), dataIndex: 'flowName', key: 'flowName' },
  { title: t('approvalRuntime.colNodeName'), dataIndex: 'nodeName', key: 'nodeName' },
  { title: t('approvalRuntime.colDeliveredAt'), dataIndex: 'createdAt', key: 'createdAt' },
  { title: t('approvalRuntime.colStatus'), dataIndex: 'status', key: 'status' },
  { title: t('approvalRuntime.colActions'), key: 'action', width: 100 }
]);

const fetchTasks = async () => {
  loading.value = true;
  try {
    const res  = await getTaskPool({
      pageIndex: pagination.value.current,
      pageSize: pagination.value.pageSize
    });

    if (!isMounted.value) return;
    tasks.value = res.items;
    pagination.value.total = res.total;
  } catch {
    message.error(t('approvalRuntime.loadPoolFailed'));
  } finally {
    loading.value = false;
  }
};

const handleTableChange = (pag: TablePaginationConfig) => {
  pagination.value.current = pag.current ?? pagination.value.current;
  pagination.value.pageSize = pag.pageSize ?? pagination.value.pageSize;
  fetchTasks();
};

const handleClaim = async (task: ApprovalTaskResponse) => {
  try {
    await claimTask(task.id);

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.claimSuccess'));
    fetchTasks();
  } catch {
    message.error(t('approvalRuntime.claimFailed'));
  }
};

const refresh = () => {
  pagination.value.current = 1;
  fetchTasks();
};

onMounted(() => {
  fetchTasks();
});
</script>

<style scoped>

</style>
