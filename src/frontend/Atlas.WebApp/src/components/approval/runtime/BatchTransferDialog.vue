<template>
  <a-modal
    :visible="visible"
    title="批量转办"
    @ok="handleOk"
    @cancel="handleCancel"
    :confirm-loading="loading"
  >
    <a-form layout="vertical">
      <a-form-item label="原处理人">
        <UserRolePicker mode="user" v-model:value="fromUsers" :max-count="1" placeholder="选择原处理人" />
      </a-form-item>
      <a-form-item label="新处理人">
        <UserRolePicker mode="user" v-model:value="toUsers" :max-count="1" placeholder="选择新处理人" />
      </a-form-item>
      <a-alert message="将原处理人名下所有待办任务转交给新处理人" type="warning" show-icon />
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { message } from 'ant-design-vue';
import { batchTransferTasks } from '@/services/api';
import UserRolePicker from '@/components/common/UserRolePicker.vue';

const props = defineProps<{
  visible: boolean;
}>();

const emit = defineEmits<{
  'update:visible': [value: boolean];
  success: [];
}>();

const fromUsers = ref<string[]>([]);
const toUsers = ref<string[]>([]);
const loading = ref(false);

const handleOk = async () => {
  if (fromUsers.value.length === 0 || toUsers.value.length === 0) {
    message.warning('请选择处理人');
    return;
  }

  loading.value = true;
  try {
    await batchTransferTasks(fromUsers.value[0], toUsers.value[0]);
    message.success('转办成功');
    emit('success');
    emit('update:visible', false);
  } catch (error) {
    message.error('转办失败');
  } finally {
    loading.value = false;
  }
};

const handleCancel = () => {
  emit('update:visible', false);
};
</script>
