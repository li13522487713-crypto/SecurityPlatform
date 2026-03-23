<template>
  <a-card :title="t('dynamicMigrations.pageTitle')" class="page-card">
    <div class="crud-toolbar">
      <a-space wrap>
        <a-input
          v-model:value="tableKeyFilter"
          :placeholder="t('dynamicMigrations.filterByTable')"
          allow-clear
          style="width: 200px"
          @press-enter="handleSearch"
        />
        <a-button @click="handleSearch">{{ t('common.search') }}</a-button>
        <a-button @click="handleReset">{{ t('common.reset') }}</a-button>
      </a-space>
    </div>

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
          <a-tag :color="getStatusColor(record.status)">{{ record.status }}</a-tag>
        </template>
        <template v-else-if="column.key === 'createdAt'">
          {{ formatDateTime(record.createdAt) }}
        </template>
        <template v-else-if="column.key === 'action'">
          <a-button type="link" size="small" @click="viewDetail(record)">{{ t('common.detail') }}</a-button>
        </template>
      </template>
    </a-table>

    <a-drawer
      v-model:open="detailVisible"
      :title="t('dynamicMigrations.detailTitle')"
      placement="right"
      :width="560"
    >
      <a-descriptions v-if="detailRecord" bordered :column="1" size="small">
        <a-descriptions-item :label="t('dynamicMigrations.colId')">{{ detailRecord.id }}</a-descriptions-item>
        <a-descriptions-item :label="t('dynamicMigrations.colTableKey')">{{ detailRecord.tableKey }}</a-descriptions-item>
        <a-descriptions-item :label="t('dynamicMigrations.colVersion')">{{ detailRecord.version }}</a-descriptions-item>
        <a-descriptions-item :label="t('dynamicMigrations.colStatus')">{{ detailRecord.status }}</a-descriptions-item>
        <a-descriptions-item :label="t('dynamicMigrations.colScript')">
          <pre style="max-height:300px;overflow:auto;font-size:12px">{{ detailRecord.scriptContent }}</pre>
        </a-descriptions-item>
        <a-descriptions-item :label="t('dynamicMigrations.colCreatedAt')">{{ formatDateTime(detailRecord.createdAt) }}</a-descriptions-item>
      </a-descriptions>
    </a-drawer>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { message } from 'ant-design-vue';
import type { TablePaginationConfig } from 'ant-design-vue';
import { requestApi } from '@/services/api-core';
import { formatDateTime } from '@/utils/common';
import type { ApiResponse, PagedResult } from '@/types/api';

const { t } = useI18n();

interface MigrationRecord {
  id: string;
  tableKey: string;
  version: string;
  status: string;
  scriptContent?: string;
  createdAt: string;
}

const columns = [
  { title: t('dynamicMigrations.colTableKey'), dataIndex: 'tableKey', key: 'tableKey' },
  { title: t('dynamicMigrations.colVersion'), dataIndex: 'version', key: 'version', width: 120 },
  { title: t('dynamicMigrations.colStatus'), key: 'status', width: 100 },
  { title: t('dynamicMigrations.colCreatedAt'), key: 'createdAt', width: 180 },
  { title: t('common.actions'), key: 'action', width: 100, fixed: 'right' as const }
];

const tableKeyFilter = ref('');
const dataSource = ref<MigrationRecord[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({ current: 1, pageSize: 20, total: 0, showTotal: (total) => t('crud.totalItems', { total }) });
const detailVisible = ref(false);
const detailRecord = ref<MigrationRecord | null>(null);

const getStatusColor = (status: string) => {
  if (status === 'Completed') return 'green';
  if (status === 'Failed') return 'red';
  if (status === 'Pending') return 'orange';
  return 'blue';
};

const fetchData = async () => {
  loading.value = true;
  try {
    const params = new URLSearchParams({
      pageIndex: String(pagination.current ?? 1),
      pageSize: String(pagination.pageSize ?? 20)
    });
    if (tableKeyFilter.value) params.set('tableKey', tableKeyFilter.value);
    const resp = await requestApi<ApiResponse<PagedResult<MigrationRecord>>>(`/dynamic-migrations?${params.toString()}`);
    dataSource.value = resp.data?.items ?? [];
    pagination.total = resp.data?.total ?? 0;
  } catch (e) {
    message.error((e as Error).message);
  } finally {
    loading.value = false;
  }
};

const handleSearch = () => { pagination.current = 1; fetchData(); };
const handleReset = () => { tableKeyFilter.value = ''; handleSearch(); };
const onTableChange = (pager: TablePaginationConfig) => { pagination.current = pager.current; pagination.pageSize = pager.pageSize; fetchData(); };
const viewDetail = (record: MigrationRecord) => { detailRecord.value = record; detailVisible.value = true; };

onMounted(fetchData);
</script>

<style scoped>
.crud-toolbar { margin-bottom: 12px; }
</style>
