<template>
  <a-modal
    :visible="visible"
    :title="t('approvalDesigner.batchTransferTitle')"
    :confirm-loading="loading"
    @ok="handleOk"
    @cancel="handleCancel"
  >
    <a-form layout="vertical">
      <a-form-item :label="t('approvalDesigner.batchFromLabel')">
        <UserRolePicker v-model:value="fromUsers" mode="user" :max-count="1" :placeholder="t('approvalDesigner.batchPhFrom')" />
      </a-form-item>
      <a-form-item :label="t('approvalDesigner.batchToLabel')">
        <UserRolePicker v-model:value="toUsers" mode="user" :max-count="1" :placeholder="t('approvalDesigner.batchPhTo')" />
      </a-form-item>
      <a-alert :message="t('approvalDesigner.batchAlertMsg')" type="warning" show-icon />
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { message } from 'ant-design-vue';
import { batchTransferTasks } from '@/services/api';
import UserRolePicker from '@/components/common/UserRolePicker.vue';

const { t } = useI18n();

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
    message.warning(t('approvalDesigner.batchWarnPickUsers'));
    return;
  }

  loading.value = true;
  try {
    await batchTransferTasks(fromUsers.value[0], toUsers.value[0]);
    message.success(t('approvalDesigner.batchMsgOk'));
    emit('success');
    emit('update:visible', false);
  } catch (error) {
    message.error(t('approvalDesigner.batchMsgFail'));
  } finally {
    loading.value = false;
  }
};

const handleCancel = () => {
  emit('update:visible', false);
};
</script>
