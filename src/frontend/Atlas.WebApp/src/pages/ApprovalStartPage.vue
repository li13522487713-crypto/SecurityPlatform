<template>
  <a-card :title="t('approvalRuntime.startPageTitle')" class="page-card">
    <a-form layout="vertical">
      <a-form-item :label="t('approvalRuntime.labelSelectFlow')" required>
        <a-select
          v-model:value="selectedDefinitionId"
          show-search
          :filter-option="false"
          :options="flowOptions"
          :loading="flowLoading"
          :placeholder="t('approvalRuntime.placeholderPublishedFlow')"
          @search="handleSearch"
        />
      </a-form-item>

      <a-form-item :label="t('approvalRuntime.labelBusinessKey')" required>
        <a-input v-model:value="businessKey" :placeholder="t('approvalRuntime.placeholderBusinessKeyExample')" />
      </a-form-item>

      <a-form-item :label="t('approvalRuntime.labelBusinessJson')">
        <a-textarea
          v-model:value="dataJsonText"
          :rows="8"
          :placeholder="t('approvalRuntime.placeholderBusinessJson')"
        />
      </a-form-item>
    </a-form>

    <a-space>
      <a-button :loading="submitting" @click="handleSaveDraft">{{ t('approvalRuntime.saveDraft') }}</a-button>
      <a-button type="primary" :loading="submitting" @click="handleStart">{{ t('approvalRuntime.submitStart') }}</a-button>
    </a-space>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, ref, onUnmounted } from 'vue';
import { useI18n } from 'vue-i18n';

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { message } from 'ant-design-vue';
import { getApprovalFlowsPaged, saveDraft, startApprovalInstance } from '@/services/api';
import { ApprovalFlowStatus } from '@/types/api';

const { t } = useI18n();

const selectedDefinitionId = ref<string>();
const businessKey = ref('');
const dataJsonText = ref('');
const flowLoading = ref(false);
const submitting = ref(false);
const flowOptions = ref<Array<{ label: string; value: string }>>([]);

const loadFlows = async (keyword?: string) => {
  flowLoading.value = true;
  try {
    const result  = await getApprovalFlowsPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword,
    });

    if (!isMounted.value) return;
    flowOptions.value = result.items
      .filter((item) => item.status === ApprovalFlowStatus.Published)
      .map((item) => ({
        label: `${item.name} (v${item.version})`,
        value: item.id,
      }));
  } catch (err) {
    message.error(err instanceof Error ? err.message : t('approvalRuntime.loadFlowsFailed'));
  } finally {
    flowLoading.value = false;
  }
};

const validatePayload = () => {
  if (!selectedDefinitionId.value) {
    message.warning(t('approvalRuntime.warnSelectDefinition'));
    return false;
  }
  if (!businessKey.value.trim()) {
    message.warning(t('approvalRuntime.warnBusinessKey'));
    return false;
  }
  if (!dataJsonText.value.trim()) {
    return true;
  }
  try {
    JSON.parse(dataJsonText.value);
    return true;
  } catch {
    message.warning(t('approvalRuntime.warnInvalidDataJson'));
    return false;
  }
};

const handleStart = async () => {
  if (!validatePayload()) {
    return;
  }

  submitting.value = true;
  try {
    await startApprovalInstance({
      definitionId: selectedDefinitionId.value!,
      businessKey: businessKey.value.trim(),
      dataJson: dataJsonText.value.trim() || undefined,
    });

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.startSuccess'));
    businessKey.value = '';
    dataJsonText.value = '';
  } catch (err) {
    message.error(err instanceof Error ? err.message : t('approvalRuntime.startFailed'));
  } finally {
    submitting.value = false;
  }
};

const handleSaveDraft = async () => {
  if (!validatePayload()) {
    return;
  }

  submitting.value = true;
  try {
    await saveDraft({
      definitionId: selectedDefinitionId.value!,
      businessKey: businessKey.value.trim(),
      dataJson: dataJsonText.value.trim() || undefined,
    });

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.draftSaved'));
  } catch (err) {
    message.error(err instanceof Error ? err.message : t('approvalRuntime.draftSaveFailed'));
  } finally {
    submitting.value = false;
  }
};

const handleSearch = (value: string) => {
  void loadFlows(value);
};

onMounted(() => {
  void loadFlows();
});
</script>
