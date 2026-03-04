<template>
  <a-card title="部门负责人配置" class="page-card">
    <a-alert
      message="功能说明"
      description="配置各部门的负责人，用于审批流中按「部门负责人」分配审批任务。每个部门只能有一位负责人。"
      type="info"
      show-icon
      class="mb-4"
    />

    <!-- 查询表单 -->
    <a-form layout="inline" class="mb-4">
      <a-form-item label="部门ID">
        <a-input
          v-model:value="queryDeptId"
          placeholder="输入部门ID"
          style="width: 200px"
          @pressEnter="handleQuery"
        />
      </a-form-item>
      <a-form-item>
        <a-button type="primary" :loading="querying" @click="handleQuery">查询</a-button>
      </a-form-item>
    </a-form>

    <!-- 查询结果 -->
    <a-card v-if="queryResult !== undefined" size="small" class="mb-4">
      <a-descriptions :column="1">
        <a-descriptions-item label="部门ID">{{ queryDeptId }}</a-descriptions-item>
        <a-descriptions-item label="当前负责人用户ID">
          <span v-if="queryResult">{{ queryResult }}</span>
          <a-tag v-else color="default">未配置</a-tag>
        </a-descriptions-item>
      </a-descriptions>
      <div style="margin-top: 16px">
        <a-space>
          <a-button type="primary" @click="handleEditOpen">
            {{ queryResult ? '修改负责人' : '设置负责人' }}
          </a-button>
          <a-popconfirm
            v-if="queryResult"
            title="确认移除该部门的负责人配置？"
            @confirm="handleRemove"
          >
            <a-button danger :loading="removing">移除负责人</a-button>
          </a-popconfirm>
        </a-space>
      </div>
    </a-card>

    <!-- 设置弹窗 -->
    <a-modal
      v-model:open="editModalVisible"
      :title="queryResult ? '修改部门负责人' : '设置部门负责人'"
      :confirm-loading="submitting"
      @ok="handleSubmit"
      @cancel="editModalVisible = false"
    >
      <a-form layout="vertical">
        <a-form-item label="部门ID">
          <a-input :value="queryDeptId" disabled />
        </a-form-item>
        <a-form-item label="负责人用户ID" required>
          <a-input
            v-model:value="newLeaderUserId"
            placeholder="请输入负责人用户ID"
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { message } from 'ant-design-vue';
import {
  getDepartmentLeader,
  setDepartmentLeader,
  removeDepartmentLeader,
} from '@/services/api';

const queryDeptId = ref('');
const querying = ref(false);
const queryResult = ref<string | null | undefined>(undefined);

const editModalVisible = ref(false);
const submitting = ref(false);
const removing = ref(false);
const newLeaderUserId = ref('');

const handleQuery = async () => {
  if (!queryDeptId.value.trim()) {
    message.warning('请输入部门ID');
    return;
  }
  querying.value = true;
  try {
    queryResult.value = await getDepartmentLeader(queryDeptId.value.trim());
  } catch (err) {
    message.error(err instanceof Error ? err.message : '查询失败');
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
    message.warning('请填写负责人用户ID');
    return;
  }
  submitting.value = true;
  try {
    await setDepartmentLeader({
      departmentId: queryDeptId.value.trim(),
      leaderUserId: newLeaderUserId.value.trim(),
    });
    message.success('已设置');
    editModalVisible.value = false;
    await handleQuery();
  } catch (err) {
    message.error(err instanceof Error ? err.message : '设置失败');
  } finally {
    submitting.value = false;
  }
};

const handleRemove = async () => {
  removing.value = true;
  try {
    await removeDepartmentLeader(queryDeptId.value.trim());
    message.success('已移除');
    await handleQuery();
  } catch (err) {
    message.error(err instanceof Error ? err.message : '移除失败');
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
