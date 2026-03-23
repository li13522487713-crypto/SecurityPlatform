<template>
  <a-card :title="t('approvalDepartmentLeader.pageTitle')" class="page-card">
    <a-alert
      :message="t('approvalDepartmentLeader.featureAlertTitle')"
      :description="t('approvalDepartmentLeader.featureAlertDesc')"
      type="info"
      show-icon
      class="mb-4"
    />

    <!-- 查询表单 -->
    <a-form layout="inline" class="mb-4">
      <a-form-item :label="t('approvalDepartmentLeader.labelDeptId')">
        <a-input
          v-model:value="queryDeptId"
          :placeholder="t('approvalDepartmentLeader.placeholderDeptId')"
          style="width: 200px"
          @press-enter="handleQuery"
        />
      </a-form-item>
      <a-form-item>
        <a-button type="primary" :loading="querying" @click="handleQuery">{{ t('approvalDepartmentLeader.query') }}</a-button>
      </a-form-item>
    </a-form>

    <!-- 查询结果 -->
    <a-card v-if="queryResult !== undefined" size="small" class="mb-4">
      <a-descriptions :column="1">
        <a-descriptions-item :label="t('approvalDepartmentLeader.descDeptId')">{{ queryDeptId }}</a-descriptions-item>
        <a-descriptions-item :label="t('approvalDepartmentLeader.descLeaderUserId')">
          <span v-if="queryResult">{{ queryResult }}</span>
          <a-tag v-else color="default">{{ t('approvalDepartmentLeader.notConfigured') }}</a-tag>
        </a-descriptions-item>
      </a-descriptions>
      <div style="margin-top: 16px">
        <a-space>
          <a-button type="primary" @click="handleEditOpen">
            {{ queryResult ? t('approvalDepartmentLeader.editLeader') : t('approvalDepartmentLeader.setLeader') }}
          </a-button>
          <a-popconfirm
            v-if="queryResult"
            :title="t('approvalDepartmentLeader.removeConfirm')"
            @confirm="handleRemove"
          >
            <a-button danger :loading="removing">{{ t('approvalDepartmentLeader.removeLeader') }}</a-button>
          </a-popconfirm>
        </a-space>
      </div>
    </a-card>

    <!-- 设置弹窗 -->
    <a-modal
      v-model:open="editModalVisible"
      :title="queryResult ? t('approvalDepartmentLeader.modalEditTitle') : t('approvalDepartmentLeader.modalSetTitle')"
      :confirm-loading="submitting"
      @ok="handleSubmit"
      @cancel="editModalVisible = false"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('approvalDepartmentLeader.labelDeptId')">
          <a-input :value="queryDeptId" disabled />
        </a-form-item>
        <a-form-item :label="t('approvalDepartmentLeader.labelLeaderUserId')" required>
          <a-input
            v-model:value="newLeaderUserId"
            :placeholder="t('approvalDepartmentLeader.placeholderLeaderUserId')"
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { message } from 'ant-design-vue';
import {
  getDepartmentLeader,
  setDepartmentLeader,
  removeDepartmentLeader,
} from '@/services/api';

const { t } = useI18n();

const queryDeptId = ref('');
const querying = ref(false);
const queryResult = ref<string | null | undefined>(undefined);

const editModalVisible = ref(false);
const submitting = ref(false);
const removing = ref(false);
const newLeaderUserId = ref('');

const handleQuery = async () => {
  if (!queryDeptId.value.trim()) {
    message.warning(t('approvalDepartmentLeader.warnDeptId'));
    return;
  }
  querying.value = true;
  try {
    queryResult.value = await getDepartmentLeader(queryDeptId.value.trim());
  } catch (err) {
    message.error(err instanceof Error ? err.message : t('approvalDepartmentLeader.queryFailed'));
  } finally {
    querying.value = false;
  }
};

const handleEditOpen = () => {
  newLeaderUserId.value = queryResult.value ?? '';
  editModalVisible.value = true;
};

const handleSubmit = async () => {
  if (!newLeaderUserId.value.trim()) {
    message.warning(t('approvalDepartmentLeader.warnLeaderId'));
    return;
  }
  submitting.value = true;
  try {
    await setDepartmentLeader({
      departmentId: queryDeptId.value.trim(),
      leaderUserId: newLeaderUserId.value.trim(),
    });
    message.success(t('approvalDepartmentLeader.setSuccess'));
    editModalVisible.value = false;
    await handleQuery();
  } catch (err) {
    message.error(err instanceof Error ? err.message : t('approvalDepartmentLeader.setFailed'));
  } finally {
    submitting.value = false;
  }
};

const handleRemove = async () => {
  removing.value = true;
  try {
    await removeDepartmentLeader(queryDeptId.value.trim());
    message.success(t('approvalDepartmentLeader.removeSuccess'));
    await handleQuery();
  } catch (err) {
    message.error(err instanceof Error ? err.message : t('approvalDepartmentLeader.removeFailed'));
  } finally {
    removing.value = false;
  }
};
</script>

<style scoped>
.mb-4 {
  margin-bottom: 16px;
}
</style>
