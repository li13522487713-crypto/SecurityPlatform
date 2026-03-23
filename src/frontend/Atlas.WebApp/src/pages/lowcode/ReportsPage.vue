<template>
  <a-card :title="t('reports.pageTitle')" class="page-card">
    <template #extra>
      <a-button type="primary" @click="handleCreate">{{ t('common.create') }}</a-button>
    </template>

    <div class="crud-toolbar">
      <a-input
        v-model:value="keyword"
        :placeholder="t('common.searchPlaceholder')"
        allow-clear
        style="width: 240px"
        @press-enter="handleSearch"
      />
      <a-button style="margin-left:8px" @click="handleSearch">{{ t('common.search') }}</a-button>
      <a-button style="margin-left:8px" @click="handleReset">{{ t('common.reset') }}</a-button>
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
        <template v-if="column.key === 'createdAt'">
          {{ formatDateTime(record.createdAt) }}
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="handleEdit(record)">{{ t('common.edit') }}</a-button>
            <a-popconfirm :title="t('common.deleteConfirm')" @confirm="handleDelete(record.id)">
              <a-button type="link" size="small" danger>{{ t('common.delete') }}</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="modalVisible"
      :title="editingId ? t('reports.editTitle') : t('reports.createTitle')"
      @ok="handleSave"
      :confirm-loading="saving"
    >
      <a-form :model="formState" layout="vertical">
        <a-form-item :label="t('reports.colName')" required>
          <a-input v-model:value="formState.name" />
        </a-form-item>
        <a-form-item :label="t('reports.colDescription')">
          <a-textarea v-model:value="formState.description" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>
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

interface ReportItem { id: number; name: string; description?: string; createdAt: string; }

const columns = computed(() => [
  { title: t('reports.colName'), dataIndex: 'name', key: 'name' },
  { title: t('reports.colDescription'), dataIndex: 'description', key: 'description' },
  { title: t('reports.colCreatedAt'), key: 'createdAt', width: 180 },
  { title: t('common.actions'), key: 'action', width: 160, fixed: 'right' as const }
]);

const keyword = ref('');
const dataSource = ref<ReportItem[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({ current: 1, pageSize: 20, total: 0, showTotal: (total) => t('crud.totalItems', { total }) });
const modalVisible = ref(false);
const saving = ref(false);
const editingId = ref<number | null>(null);
const formState = reactive({ name: '', description: '' });

const fetchData = async () => {
  loading.value = true;
  try {
    const params = new URLSearchParams({ pageIndex: String(pagination.current ?? 1), pageSize: String(pagination.pageSize ?? 20) });
    if (keyword.value) params.set('keyword', keyword.value);
    const resp = await requestApi<ApiResponse<PagedResult<ReportItem>>>(`/reports?${params}`);
    dataSource.value = resp.data?.items ?? [];
    pagination.total = resp.data?.total ?? 0;
  } catch (e) { message.error((e as Error).message); } finally { loading.value = false; }
};

const handleSearch = () => { pagination.current = 1; fetchData(); };
const handleReset = () => { keyword.value = ''; handleSearch(); };
const onTableChange = (pager: TablePaginationConfig) => { pagination.current = pager.current; pagination.pageSize = pager.pageSize; fetchData(); };
const handleCreate = () => { editingId.value = null; Object.assign(formState, { name: '', description: '' }); modalVisible.value = true; };
const handleEdit = (r: ReportItem) => { editingId.value = r.id; Object.assign(formState, { name: r.name, description: r.description ?? '' }); modalVisible.value = true; };

const handleSave = async () => {
  if (!formState.name) { message.warning(t('common.requiredFields')); return; }
  saving.value = true;
  try {
    if (editingId.value) {
      await requestApi<ApiResponse<object>>(`/reports/${editingId.value}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(formState) });
    } else {
      await requestApi<ApiResponse<object>>('/reports', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(formState) });
    }
    message.success(t('common.saveSuccess'));
    modalVisible.value = false;
    await fetchData();
  } catch (e) { message.error((e as Error).message); } finally { saving.value = false; }
};

const handleDelete = async (id: number) => {
  try {
    await requestApi<ApiResponse<object>>(`/reports/${id}`, { method: 'DELETE' });
    message.success(t('common.deleteSuccess'));
    await fetchData();
  } catch (e) { message.error((e as Error).message); }
};

onMounted(fetchData);
</script>

<style scoped>
.crud-toolbar { margin-bottom: 12px; display: flex; align-items: center; }
</style>
