<template>
  <a-card :title="t('dashboards.pageTitle')" class="page-card">
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
        <template v-if="column.key === 'isDefault'">
          <a-tag :color="record.isDefault ? 'blue' : 'default'">{{ record.isDefault ? t('dashboards.defaultDashboard') : '' }}</a-tag>
        </template>
        <template v-else-if="column.key === 'createdAt'">
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
      :title="editingId ? t('dashboards.editTitle') : t('dashboards.createTitle')"
      :confirm-loading="saving"
      @ok="handleSave"
    >
      <a-form :model="formState" layout="vertical">
        <a-form-item :label="t('dashboards.colName')" required>
          <a-input v-model:value="formState.name" />
        </a-form-item>
        <a-form-item :label="t('dashboards.colDescription')">
          <a-textarea v-model:value="formState.description" :rows="3" />
        </a-form-item>
        <a-form-item>
          <a-checkbox v-model:checked="formState.isDefault">{{ t('dashboards.setDefault') }}</a-checkbox>
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

interface DashboardItem { id: number; name: string; description?: string; isDefault: boolean; createdAt: string; }

const columns = computed(() => [
  { title: t('dashboards.colName'), dataIndex: 'name', key: 'name' },
  { title: t('dashboards.colDescription'), dataIndex: 'description', key: 'description' },
  { title: t('dashboards.colDefault'), key: 'isDefault', width: 120 },
  { title: t('dashboards.colCreatedAt'), key: 'createdAt', width: 180 },
  { title: t('common.actions'), key: 'action', width: 160, fixed: 'right' as const }
]);

const keyword = ref('');
const dataSource = ref<DashboardItem[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({ current: 1, pageSize: 20, total: 0, showTotal: (total) => t('crud.totalItems', { total }) });
const modalVisible = ref(false);
const saving = ref(false);
const editingId = ref<number | null>(null);
const formState = reactive({ name: '', description: '', isDefault: false });

const fetchData = async () => {
  loading.value = true;
  try {
    const params = new URLSearchParams({ pageIndex: String(pagination.current ?? 1), pageSize: String(pagination.pageSize ?? 20) });
    if (keyword.value) params.set('keyword', keyword.value);
    const resp = await requestApi<ApiResponse<PagedResult<DashboardItem>>>(`/dashboards?${params}`);
    dataSource.value = resp.data?.items ?? [];
    pagination.total = resp.data?.total ?? 0;
  } catch (e) { message.error((e as Error).message); } finally { loading.value = false; }
};

const handleSearch = () => { pagination.current = 1; fetchData(); };
const handleReset = () => { keyword.value = ''; handleSearch(); };
const onTableChange = (pager: TablePaginationConfig) => { pagination.current = pager.current; pagination.pageSize = pager.pageSize; fetchData(); };
const handleCreate = () => { editingId.value = null; Object.assign(formState, { name: '', description: '', isDefault: false }); modalVisible.value = true; };
const handleEdit = (r: DashboardItem) => { editingId.value = r.id; Object.assign(formState, { name: r.name, description: r.description ?? '', isDefault: r.isDefault }); modalVisible.value = true; };

const handleSave = async () => {
  if (!formState.name) { message.warning(t('common.requiredFields')); return; }
  saving.value = true;
  try {
    if (editingId.value) {
      await requestApi<ApiResponse<object>>(`/dashboards/${editingId.value}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(formState) });
    } else {
      await requestApi<ApiResponse<object>>('/dashboards', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(formState) });
    }
    message.success(t('common.saveSuccess'));
    modalVisible.value = false;
    await fetchData();
  } catch (e) { message.error((e as Error).message); } finally { saving.value = false; }
};

const handleDelete = async (id: number) => {
  try {
    await requestApi<ApiResponse<object>>(`/dashboards/${id}`, { method: 'DELETE' });
    message.success(t('common.deleteSuccess'));
    await fetchData();
  } catch (e) { message.error((e as Error).message); }
};

onMounted(fetchData);
</script>

<style scoped>
.crud-toolbar { margin-bottom: 12px; display: flex; align-items: center; }
</style>
