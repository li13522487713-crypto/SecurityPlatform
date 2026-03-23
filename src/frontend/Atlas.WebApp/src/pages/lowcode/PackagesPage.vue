<template>
  <a-card :title="t('packages.pageTitle')" class="page-card">
    <a-tabs v-model:activeKey="activeTab">
      <!-- 导出 -->
      <a-tab-pane key="export" :tab="t('packages.tabExport')">
        <a-form layout="vertical" :model="exportForm" style="max-width: 480px">
          <a-form-item :label="t('packages.exportResources')">
            <a-select
              v-model:value="exportForm.resourceTypes"
              mode="multiple"
              :options="resourceTypeOptions"
              :placeholder="t('packages.selectResources')"
            />
          </a-form-item>
          <a-form-item :label="t('packages.exportNote')">
            <a-textarea v-model:value="exportForm.note" :rows="3" />
          </a-form-item>
          <a-form-item>
            <a-button type="primary" :loading="exportLoading" @click="handleExport">{{ t('packages.doExport') }}</a-button>
          </a-form-item>
        </a-form>
        <a-alert v-if="exportResult" type="success" :message="exportResult.message" show-icon style="margin-top:12px" />
      </a-tab-pane>

      <!-- 导入 -->
      <a-tab-pane key="import" :tab="t('packages.tabImport')">
        <a-form layout="vertical" :model="importForm" style="max-width: 480px">
          <a-form-item :label="t('packages.importPackageId')">
            <a-input v-model:value="importForm.packageId" :placeholder="t('packages.packageIdPlaceholder')" />
          </a-form-item>
          <a-form-item>
            <a-button type="primary" :loading="importLoading" @click="handleImport">{{ t('packages.doImport') }}</a-button>
          </a-form-item>
        </a-form>
        <a-alert v-if="importResult" type="success" :message="importResult.message" show-icon style="margin-top:12px" />
      </a-tab-pane>

      <!-- 分析 -->
      <a-tab-pane key="analyze" :tab="t('packages.tabAnalyze')">
        <a-form layout="vertical" style="max-width: 480px">
          <a-form-item :label="t('packages.analyzePackageId')">
            <a-input v-model:value="analyzePackageId" :placeholder="t('packages.packageIdPlaceholder')" />
          </a-form-item>
          <a-form-item>
            <a-button type="primary" :loading="analyzeLoading" @click="handleAnalyze">{{ t('packages.doAnalyze') }}</a-button>
          </a-form-item>
        </a-form>
        <pre v-if="analyzeResult" style="background:#f5f5f5;padding:12px;border-radius:4px;overflow:auto;max-height:400px;font-size:12px">{{ JSON.stringify(analyzeResult, null, 2) }}</pre>
      </a-tab-pane>
    </a-tabs>
  </a-card>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { message } from 'ant-design-vue';
import { requestApi } from '@/services/api-core';
import type { ApiResponse } from '@/types/api';

const { t } = useI18n();

const activeTab = ref('export');

const resourceTypeOptions = [
  { label: 'LowCodeApp', value: 'LowCodeApp' },
  { label: 'DynamicTable', value: 'DynamicTable' },
  { label: 'FormDefinition', value: 'FormDefinition' },
  { label: 'ApprovalFlow', value: 'ApprovalFlow' },
  { label: 'Workflow', value: 'Workflow' }
];

const exportForm = reactive({ resourceTypes: [] as string[], note: '' });
const exportLoading = ref(false);
const exportResult = ref<{ message: string } | null>(null);

const importForm = reactive({ packageId: '' });
const importLoading = ref(false);
const importResult = ref<{ message: string } | null>(null);

const analyzePackageId = ref('');
const analyzeLoading = ref(false);
const analyzeResult = ref<unknown>(null);

const handleExport = async () => {
  exportLoading.value = true;
  exportResult.value = null;
  try {
    const resp = await requestApi<ApiResponse<{ packageId: string; message: string }>>('/packages/export', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ resourceTypes: exportForm.resourceTypes, note: exportForm.note })
    });
    exportResult.value = { message: resp.data?.message ?? t('packages.exportSuccess') };
    message.success(t('packages.exportSuccess'));
  } catch (e) {
    message.error((e as Error).message);
  } finally {
    exportLoading.value = false;
  }
};

const handleImport = async () => {
  if (!importForm.packageId) { message.warning(t('packages.packageIdRequired')); return; }
  importLoading.value = true;
  importResult.value = null;
  try {
    const resp = await requestApi<ApiResponse<{ message: string }>>('/packages/import', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ packageId: importForm.packageId })
    });
    importResult.value = { message: resp.data?.message ?? t('packages.importSuccess') };
    message.success(t('packages.importSuccess'));
  } catch (e) {
    message.error((e as Error).message);
  } finally {
    importLoading.value = false;
  }
};

const handleAnalyze = async () => {
  if (!analyzePackageId.value) { message.warning(t('packages.packageIdRequired')); return; }
  analyzeLoading.value = true;
  analyzeResult.value = null;
  try {
    const resp = await requestApi<ApiResponse<unknown>>('/packages/analyze', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ packageId: analyzePackageId.value })
    });
    analyzeResult.value = resp.data;
  } catch (e) {
    message.error((e as Error).message);
  } finally {
    analyzeLoading.value = false;
  }
};
</script>
