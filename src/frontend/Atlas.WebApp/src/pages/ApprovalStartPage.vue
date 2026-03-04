<template>
  <a-card title="发起审批" class="page-card">
    <a-form layout="vertical">
      <a-form-item label="选择流程" required>
        <a-select
          v-model:value="selectedDefinitionId"
          show-search
          :filter-option="false"
          :options="flowOptions"
          :loading="flowLoading"
          placeholder="请选择已发布流程"
          @search="handleSearch"
        />
      </a-form-item>

      <a-form-item label="业务标识（BusinessKey）" required>
        <a-input v-model:value="businessKey" placeholder="例如：PROC-20260303-0001" />
      </a-form-item>

      <a-form-item label="业务数据 JSON">
        <a-textarea
          v-model:value="dataJsonText"
          :rows="8"
          placeholder='{"title":"采购申请","amount":1000}'
        />
      </a-form-item>
    </a-form>

    <a-space>
      <a-button @click="handleSaveDraft" :loading="submitting">保存草稿</a-button>
      <a-button type="primary" @click="handleStart" :loading="submitting">提交发起</a-button>
    </a-space>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { message } from 'ant-design-vue';
import { getApprovalFlowsPaged, saveDraft, startApprovalInstance } from '@/services/api';
import { ApprovalFlowStatus } from '@/types/api';

const selectedDefinitionId = ref<string>();
const businessKey = ref('');
const dataJsonText = ref('');
const flowLoading = ref(false);
const submitting = ref(false);
const flowOptions = ref<Array<{ label: string; value: string }>>([]);

const loadFlows = async (keyword?: string) => {
  flowLoading.value = true;
  try {
    const result = await getApprovalFlowsPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword,
    });
    flowOptions.value = result.items
      .filter((item) => item.status === ApprovalFlowStatus.Published)
      .map((item) => ({
        label: `${item.name} (v${item.version})`,
        value: item.id,
      }));
  } catch (err) {
    message.error(err instanceof Error ? err.message : '加载流程列表失败');
  } finally {
    flowLoading.value = false;
  }
};

const validatePayload = () => {
  if (!selectedDefinitionId.value) {
    message.warning('请选择流程定义');
    return false;
  }
  if (!businessKey.value.trim()) {
    message.warning('请输入业务标识');
    return false;
  }
  if (!dataJsonText.value.trim()) {
    return true;
  }
  try {
    JSON.parse(dataJsonText.value);
    return true;
  } catch {
    message.warning('业务数据 JSON 格式不正确');
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
    message.success('流程发起成功');
    businessKey.value = '';
    dataJsonText.value = '';
  } catch (err) {
    message.error(err instanceof Error ? err.message : '发起失败');
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
    message.success('草稿保存成功');
  } catch (err) {
    message.error(err instanceof Error ? err.message : '保存草稿失败');
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
