<template>
  <a-card title="我的代理设置" class="page-card">
    <div class="toolbar">
      <a-button type="primary" @click="handleCreate">添加代理</a-button>
    </div>
    <a-table
      :columns="columns"
      :data-source="dataSource"
      :loading="loading"
      row-key="id"
      :pagination="false"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'timeRange'">
          {{ formatTime(record.startTime) }} ~ {{ formatTime(record.endTime) }}
        </template>
        <template v-else-if="column.key === 'isEnabled'">
          <a-tag :color="record.isEnabled ? 'green' : 'default'">
            {{ record.isEnabled ? '生效中' : '已失效' }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-popconfirm title="确认删除此代理设置？" @confirm="handleDelete(record.id)">
            <a-button type="link" danger size="small">删除</a-button>
          </a-popconfirm>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="modalVisible"
      title="添加代理设置"
      :confirm-loading="submitting"
      @ok="handleSubmit"
      @cancel="resetForm"
    >
      <a-form :model="form" layout="vertical">
        <a-form-item label="代理人用户ID" required>
          <a-input
            v-model:value="form.agentUserId"
            placeholder="请输入代理人的用户ID"
          />
        </a-form-item>
        <a-form-item label="代理有效期" required>
          <a-range-picker
            v-model:value="form.dateRange"
            show-time
            style="width: 100%"
            format="YYYY-MM-DD HH:mm"
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue';
import { message } from 'ant-design-vue';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import {
  getMyAgentConfigs,
  createAgentConfig,
  deleteAgentConfig,
  type ApprovalAgentConfigResponse,
} from '@/services/api';

const columns = [
  { title: '代理人用户ID', dataIndex: 'agentUserId', key: 'agentUserId' },
  { title: '委托人用户ID', dataIndex: 'principalUserId', key: 'principalUserId' },
  { title: '有效期', key: 'timeRange' },
  { title: '状态', key: 'isEnabled', width: 100 },
  { title: '操作', key: 'action', width: 100 },
];

const dataSource = ref<ApprovalAgentConfigResponse[]>([]);
const loading = ref(false);
const modalVisible = ref(false);
const submitting = ref(false);

const form = reactive<{
  agentUserId: string;
  dateRange: [Dayjs, Dayjs] | null;
}>({
  agentUserId: '',
  dateRange: null,
});

const fetchData = async () => {
  loading.value = true;
  try {
    dataSource.value = await getMyAgentConfigs();
  } catch (err) {
    message.error(err instanceof Error ? err.message : '加载失败');
  } finally {
    loading.value = false;
  }
};

const handleCreate = () => {
  modalVisible.value = true;
};

const resetForm = () => {
  form.agentUserId = '';
  form.dateRange = null;
  modalVisible.value = false;
};

const handleSubmit = async () => {
  if (!form.agentUserId.trim()) {
    message.warning('请填写代理人用户ID');
    return;
  }
  if (!form.dateRange) {
    message.warning('请选择代理有效期');
    return;
  }

  submitting.value = true;
  try {
    await createAgentConfig({
      agentUserId: form.agentUserId.trim(),
      startTime: form.dateRange[0].toISOString(),
      endTime: form.dateRange[1].toISOString(),
    });
    message.success('已添加代理设置');
    resetForm();
    await fetchData();
  } catch (err) {
    message.error(err instanceof Error ? err.message : '添加失败');
  } finally {
    submitting.value = false;
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteAgentConfig(id);
    message.success('已删除');
    await fetchData();
  } catch (err) {
    message.error(err instanceof Error ? err.message : '删除失败');
  }
};

const formatTime = (time: string) => dayjs(time).format('YYYY-MM-DD HH:mm');

onMounted(() => {
  void fetchData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}
</style>
