<template>
  <a-card :title="t('apiConnectors.pageTitle')" class="page-card">
    <template #extra>
      <a-button type="primary" @click="handleCreate">{{ t('common.create') }}</a-button>
    </template>

    <a-table
      :columns="columns"
      :data-source="dataSource"
      :loading="loading"
      row-key="id"
      :pagination="false"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-badge :status="record.enabled ? 'success' : 'default'" :text="record.enabled ? t('apiConnectors.statusEnabled') : t('apiConnectors.statusDisabled')" />
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="handleSync(record.id)">{{ t('apiConnectors.sync') }}</a-button>
            <a-button type="link" size="small" @click="handleHealthCheck(record.id)">{{ t('apiConnectors.healthCheck') }}</a-button>
            <a-button type="link" size="small" @click="handleEdit(record)">{{ t('common.edit') }}</a-button>
            <a-popconfirm :title="t('common.deleteConfirm')" @confirm="handleDelete(record.id)">
              <a-button type="link" size="small" danger>{{ t('common.delete') }}</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <!-- 创建/编辑弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      :title="editingRecord ? t('apiConnectors.editTitle') : t('apiConnectors.createTitle')"
      :confirm-loading="saving"
      @ok="handleSave"
    >
      <a-form :model="formState" layout="vertical">
        <a-form-item :label="t('apiConnectors.colName')" required>
          <a-input v-model:value="formState.name" />
        </a-form-item>
        <a-form-item :label="t('apiConnectors.colBaseUrl')" required>
          <a-input v-model:value="formState.baseUrl" placeholder="https://api.example.com" />
        </a-form-item>
        <a-form-item :label="t('apiConnectors.colAuthType')">
          <a-select v-model:value="formState.authType" :options="authTypeOptions" />
        </a-form-item>
        <a-form-item :label="t('apiConnectors.colDescription')">
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
import { requestApi } from '@/services/api-core';
import type { ApiResponse } from '@/types/api';

const { t } = useI18n();

interface ConnectorRecord {
  id: number;
  name: string;
  baseUrl: string;
  authType?: string;
  description?: string;
  enabled: boolean;
}

const columns = computed(() => [
  { title: t('apiConnectors.colName'), dataIndex: 'name', key: 'name' },
  { title: t('apiConnectors.colBaseUrl'), dataIndex: 'baseUrl', key: 'baseUrl' },
  { title: t('apiConnectors.colAuthType'), dataIndex: 'authType', key: 'authType', width: 120 },
  { title: t('apiConnectors.colStatus'), key: 'status', width: 120 },
  { title: t('common.actions'), key: 'action', width: 220, fixed: 'right' as const }
]);

const authTypeOptions = [
  { label: 'None', value: 'None' },
  { label: 'ApiKey', value: 'ApiKey' },
  { label: 'Bearer', value: 'Bearer' },
  { label: 'Basic', value: 'Basic' },
  { label: 'OAuth2', value: 'OAuth2' }
];

const dataSource = ref<ConnectorRecord[]>([]);
const loading = ref(false);
const modalVisible = ref(false);
const saving = ref(false);
const editingRecord = ref<ConnectorRecord | null>(null);
const formState = reactive({ name: '', baseUrl: '', authType: 'None', description: '' });

const fetchData = async () => {
  loading.value = true;
  try {
    const resp = await requestApi<ApiResponse<ConnectorRecord[]>>('/connectors');
    dataSource.value = resp.data ?? [];
  } catch (e) {
    message.error((e as Error).message);
  } finally {
    loading.value = false;
  }
};

const handleCreate = () => {
  editingRecord.value = null;
  Object.assign(formState, { name: '', baseUrl: '', authType: 'None', description: '' });
  modalVisible.value = true;
};

const handleEdit = (record: ConnectorRecord) => {
  editingRecord.value = record;
  Object.assign(formState, { name: record.name, baseUrl: record.baseUrl, authType: record.authType ?? 'None', description: record.description ?? '' });
  modalVisible.value = true;
};

const handleSave = async () => {
  if (!formState.name || !formState.baseUrl) { message.warning(t('common.requiredFields')); return; }
  saving.value = true;
  try {
    if (editingRecord.value) {
      await requestApi<ApiResponse<object>>(`/connectors/${editingRecord.value.id}`, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(formState)
      });
    } else {
      await requestApi<ApiResponse<object>>('/connectors', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(formState)
      });
    }
    message.success(t('common.saveSuccess'));
    modalVisible.value = false;
    await fetchData();
  } catch (e) {
    message.error((e as Error).message);
  } finally {
    saving.value = false;
  }
};

const handleDelete = async (id: number) => {
  try {
    await requestApi<ApiResponse<object>>(`/connectors/${id}`, { method: 'DELETE' });
    message.success(t('common.deleteSuccess'));
    await fetchData();
  } catch (e) {
    message.error((e as Error).message);
  }
};

const handleSync = async (id: number) => {
  try {
    await requestApi<ApiResponse<object>>(`/connectors/${id}/sync`, { method: 'POST' });
    message.success(t('apiConnectors.syncSuccess'));
  } catch (e) {
    message.error((e as Error).message);
  }
};

const handleHealthCheck = async (id: number) => {
  try {
    const resp = await requestApi<ApiResponse<{ healthy: boolean; message?: string }>>(`/connectors/${id}/health`);
    if (resp.data?.healthy) {
      message.success(t('apiConnectors.healthOk'));
    } else {
      message.warning(resp.data?.message ?? t('apiConnectors.healthFail'));
    }
  } catch (e) {
    message.error((e as Error).message);
  }
};

onMounted(fetchData);
</script>
