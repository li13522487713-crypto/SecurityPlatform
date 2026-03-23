<template>
  <a-card :title="t('outbox.pageTitle')" class="page-card">
    <template #extra>
      <a-space>
        <a-button :loading="loading" @click="fetchData">{{ t('common.refresh') }}</a-button>
        <a-popconfirm :title="t('outbox.retryAllConfirm')" @confirm="handleRetryAll">
          <a-button type="primary">{{ t('outbox.retryAll') }}</a-button>
        </a-popconfirm>
      </a-space>
    </template>

    <a-table
      :columns="columns"
      :data-source="dataSource"
      :pagination="pagination"
      :loading="loading"
      row-key="id"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="record.status === 'Failed' ? 'red' : record.status === 'Pending' ? 'orange' : 'green'">{{ record.status }}</a-tag>
        </template>
        <template v-else-if="column.key === 'createdAt'">
          {{ formatDateTime(record.createdAt) }}
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="handleRetry(record.id)">{{ t('outbox.retry') }}</a-button>
            <a-popconfirm :title="t('common.deleteConfirm')" @confirm="handleDelete(record.id)">
              <a-button type="link" size="small" danger>{{ t('common.delete') }}</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { message } from 'ant-design-vue';
import type { TablePaginationConfig } from 'ant-design-vue';
import { requestApi } from '@/services/api-core';
import { formatDateTime } from '@/utils/common';
import type { ApiResponse, PagedResult } from '@/types/api';

const { t } = useI18n();

interface OutboxItem { id: string; messageType: string; status: string; retryCount: number; error?: string; createdAt: string; }

const columns = computed(() => [
  { title: t('outbox.colMessageType'), dataIndex: 'messageType', key: 'messageType' },
  { title: t('outbox.colStatus'), key: 'status', width: 100 },
  { title: t('outbox.colRetryCount'), dataIndex: 'retryCount', key: 'retryCount', width: 100 },
  { title: t('outbox.colError'), dataIndex: 'error', key: 'error', ellipsis: true },
  { title: t('outbox.colCreatedAt'), key: 'createdAt', width: 180 },
  { title: t('common.actions'), key: 'action', width: 160, fixed: 'right' as const }
]);

const dataSource = ref<OutboxItem[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({ current: 1, pageSize: 20, total: 0, showTotal: (total) => t('crud.totalItems', { total }) });

const fetchData = async () => {
  loading.value = true;
  try {
    const params = new URLSearchParams({ pageIndex: String(pagination.current ?? 1), pageSize: String(pagination.pageSize ?? 20) });
    const resp = await requestApi<ApiResponse<PagedResult<OutboxItem>>>(`/admin/outbox?${params}`);
    dataSource.value = resp.data?.items ?? [];
    pagination.total = resp.data?.total ?? 0;
  } catch (e) { message.error((e as Error).message); } finally { loading.value = false; }
};

const onTableChange = (pager: TablePaginationConfig) => { pagination.current = pager.current; pagination.pageSize = pager.pageSize; fetchData(); };

const handleRetry = async (id: string) => {
  try {
    await requestApi<ApiResponse<object>>(`/admin/outbox/${id}/retry`, { method: 'POST' });
    message.success(t('outbox.retrySuccess'));
    await fetchData();
  } catch (e) { message.error((e as Error).message); }
};

const handleRetryAll = async () => {
  try {
    await requestApi<ApiResponse<object>>('/admin/outbox/retry-all', { method: 'POST' });
    message.success(t('outbox.retryAllSuccess'));
    await fetchData();
  } catch (e) { message.error((e as Error).message); }
};

const handleDelete = async (id: string) => {
  try {
    await requestApi<ApiResponse<object>>(`/admin/outbox/${id}`, { method: 'DELETE' });
    message.success(t('common.deleteSuccess'));
    await fetchData();
  } catch (e) { message.error((e as Error).message); }
};

onMounted(fetchData);
</script>

<style scoped>
.crud-toolbar { margin-bottom: 12px; }
</style>
