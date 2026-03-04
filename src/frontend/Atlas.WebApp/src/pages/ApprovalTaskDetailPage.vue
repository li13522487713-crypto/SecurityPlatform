<template>
  <div class="task-detail-page">
    <div class="page-header">
      <a-page-header
        :title="`审批任务: ${task?.title || ''}`"
        @back="$router.back()"
      >
        <template #tags>
          <a-tag :color="getStatusColor(task?.status)">{{ getStatusText(task?.status) }}</a-tag>
        </template>
        <template #extra>
          <template v-if="task?.status === ApprovalTaskStatus.Pending">
            <a-button type="primary" @click="showApproveModal">同意</a-button>
            <a-button danger @click="showRejectModal">驳回</a-button>
            <a-dropdown>
              <template #overlay>
                <a-menu @click="handleMenuClick">
                  <a-menu-item key="transfer">转办</a-menu-item>
                  <a-menu-item key="delegate">委派</a-menu-item>
                  <a-menu-item key="jump">跳转</a-menu-item>
                  <a-menu-item key="communicate">沟通</a-menu-item>
                </a-menu>
              </template>
              <a-button>更多操作 <DownOutlined /></a-button>
            </a-dropdown>
          </template>
        </template>
      </a-page-header>
    </div>

    <a-spin :spinning="loading">
      <div class="content-layout">
        <div class="main-content">
          <!-- 业务表单区域 -->
          <a-card title="业务表单" class="mb-4">
            <AmisRenderer
              v-if="amisSchema"
              :schema="amisSchema"
              :data="formDataForAmis"
            />
            <LfFormRenderer
              v-else
              :form-json="formJson ?? undefined"
              :form-data="formData"
              :read-only="true"
            />
          </a-card>

          <a-card title="审批轨迹">
            <a-timeline v-if="historyItems.length > 0">
              <a-timeline-item v-for="item in historyItems" :key="item.id">
                <div class="timeline-title">{{ item.eventType }}</div>
                <div class="timeline-meta">
                  {{ formatDateTime(item.occurredAt) }}
                  <span v-if="item.actorUserId"> · 操作人: {{ item.actorUserId }}</span>
                  <span v-if="item.fromNode || item.toNode">
                    · {{ item.fromNode || '-' }} -> {{ item.toNode || '-' }}
                  </span>
                </div>
              </a-timeline-item>
            </a-timeline>
            <a-empty v-else description="暂无历史轨迹" />
          </a-card>
        </div>

        <div class="side-content">
          <a-card title="附件上传" class="mb-4">
            <FileUploadPanel
              v-model="attachmentFiles"
              :disabled="task?.status !== ApprovalTaskStatus.Pending"
              button-text="上传审批附件"
            />
          </a-card>

          <!-- 沟通面板 -->
          <a-card title="沟通记录" class="mb-4">
            <CommunicationPanel
              v-if="task"
              :task-id="task.id"
              :current-user-id="currentUserId"
              style="height: 400px"
            />
          </a-card>
        </div>
      </div>
    </a-spin>

    <!-- 同意弹窗 -->
    <a-modal
      v-model:open="approveVisible"
      title="审批同意"
      @ok="handleApprove"
      :confirm-loading="submitting"
    >
      <a-textarea v-model:value="comment" placeholder="请输入审批意见（选填）" :rows="4" />
    </a-modal>

    <!-- 驳回弹窗 -->
    <a-modal
      v-model:open="rejectVisible"
      title="审批驳回"
      @ok="handleReject"
      :confirm-loading="submitting"
    >
      <a-form-item label="驳回原因" required>
        <a-textarea v-model:value="comment" placeholder="请输入驳回原因" :rows="4" />
      </a-form-item>
    </a-modal>

    <!-- 转办弹窗 -->
    <a-modal
      v-model:open="transferVisible"
      title="转办"
      @ok="handleTransfer"
      :confirm-loading="submitting"
      @cancel="resetTransferForm"
    >
      <a-form layout="vertical">
        <a-form-item label="转办给" required>
          <UserRolePicker
            mode="user"
            v-model:value="transferTargetIds"
            placeholder="请选择转办人"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="transferComment" placeholder="请输入转办说明（选填）" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 委派弹窗 -->
    <a-modal
      v-model:open="delegateVisible"
      title="委派"
      @ok="handleDelegate"
      :confirm-loading="submitting"
      @cancel="resetDelegateForm"
    >
      <a-form layout="vertical">
        <a-form-item label="委派给" required>
          <UserRolePicker
            mode="user"
            v-model:value="delegateTargetIds"
            placeholder="请选择委派人"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="delegateComment" placeholder="请输入委派说明（选填）" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 跳转选择器 -->
    <JumpNodeSelector
      v-model:visible="jumpVisible"
      :flow-definition="flowDefinitionNodes"
      @select="handleJump"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { message } from 'ant-design-vue';
import {
  getApprovalTaskById,
  getApprovalInstanceById,
  getApprovalInstanceHistory,
  decideApprovalTask,
  delegateTask,
  transferTask,
  jumpTask,
  getApprovalFlowById,
  getCurrentUser
} from '@/services/api';
import { DownOutlined } from '@ant-design/icons-vue';
import CommunicationPanel from '@/components/approval/runtime/CommunicationPanel.vue';
import JumpNodeSelector from '@/components/approval/runtime/JumpNodeSelector.vue';
import LfFormRenderer from '@/components/approval/runtime/LfFormRenderer.vue';
import AmisRenderer from '@/components/amis/amis-renderer.vue';
import UserRolePicker from '@/components/common/UserRolePicker.vue';
import FileUploadPanel from '@/components/common/file-upload-panel.vue';
import type {
  ApprovalTaskResponse,
  ApprovalInstanceResponse,
  ApprovalFlowDefinitionResponse,
  ApprovalHistoryEventResponse,
  FileUploadResult
} from '@/types/api';
import type { JsonValue } from '@/types/api';
import type { ApprovalDefinitionJson, FormJson } from '@/types/approval-definition';
import { ApprovalTaskStatus } from '@/types/api';

const route = useRoute();
const router = useRouter();
const taskId = route.params.id as string;

const loading = ref(false);
const submitting = ref(false);
const task = ref<ApprovalTaskResponse | null>(null);
const instance = ref<ApprovalInstanceResponse | null>(null);
const flowDefinition = ref<ApprovalFlowDefinitionResponse | null>(null);
const parsedDefinition = ref<ApprovalDefinitionJson | null>(null);
const currentUserId = ref('');
const historyItems = ref<ApprovalHistoryEventResponse[]>([]);

const approveVisible = ref(false);
const rejectVisible = ref(false);
const transferVisible = ref(false);
const delegateVisible = ref(false);
const jumpVisible = ref(false);
const attachmentFiles = ref<FileUploadResult[]>([]);

const comment = ref('');
const transferTargetIds = ref<string[]>([]);
const transferComment = ref('');
const delegateTargetIds = ref<string[]>([]);
const delegateComment = ref('');

const formJson = computed<FormJson | null>(() => {
  return parsedDefinition.value?.lfForm?.formJson ?? null;
});

const amisSchema = computed<Record<string, JsonValue> | null>(() => {
  const schema = parsedDefinition.value?.amisForm?.schema;
  if (!schema || typeof schema !== 'object') {
    return null;
  }
  return schema as Record<string, JsonValue>;
});

const formData = computed<Record<string, unknown>>(() => {
  if (!instance.value?.dataJson) return {};
  try {
    return JSON.parse(instance.value.dataJson) as Record<string, unknown>;
  } catch {
    return {};
  }
});

const formDataForAmis = computed<Record<string, JsonValue>>(() => {
  return formData.value as Record<string, JsonValue>;
});

const flowDefinitionNodes = computed(() => {
  return parsedDefinition.value?.nodes ?? null;
});

const fetchDetail = async () => {
  loading.value = true;
  try {
    const [userResult, taskResult] = await Promise.all([
      getCurrentUser(),
      getApprovalTaskById(taskId)
    ]);

    currentUserId.value = userResult.id;
    task.value = taskResult;

    const [instanceResult, historyResult] = await Promise.all([
      getApprovalInstanceById(taskResult.instanceId),
      getApprovalInstanceHistory(taskResult.instanceId, { pageIndex: 1, pageSize: 50 })
    ]);
    instance.value = instanceResult;
    historyItems.value = historyResult.items;

    if (instanceResult.definitionId) {
      const def = await getApprovalFlowById(String(instanceResult.definitionId));
      flowDefinition.value = def;
      try {
        parsedDefinition.value = JSON.parse(def.definitionJson) as ApprovalDefinitionJson;
      } catch {
        parsedDefinition.value = null;
      }
    }
  } catch {
    message.error('获取任务详情失败');
  } finally {
    loading.value = false;
  }
};

const showApproveModal = () => {
  comment.value = '同意';
  approveVisible.value = true;
};

const showRejectModal = () => {
  comment.value = '';
  rejectVisible.value = true;
};

const handleApprove = async () => {
  submitting.value = true;
  try {
    await decideApprovalTask({ taskId, approved: true, comment: comment.value });
    message.success('已同意');
    approveVisible.value = false;
    router.back();
  } catch {
    message.error('操作失败');
  } finally {
    submitting.value = false;
  }
};

const handleReject = async () => {
  if (!comment.value.trim()) {
    message.warning('请填写驳回原因');
    return;
  }
  submitting.value = true;
  try {
    await decideApprovalTask({ taskId, approved: false, comment: comment.value });
    message.success('已驳回');
    rejectVisible.value = false;
    router.back();
  } catch {
    message.error('操作失败');
  } finally {
    submitting.value = false;
  }
};

const handleTransfer = async () => {
  if (!transferTargetIds.value.length) {
    message.warning('请选择转办人');
    return;
  }
  if (!instance.value) return;
  submitting.value = true;
  try {
    await transferTask(String(instance.value.id), taskId, transferTargetIds.value[0], transferComment.value || undefined);
    message.success('转办成功');
    transferVisible.value = false;
    resetTransferForm();
    await fetchDetail();
  } catch {
    message.error('转办失败');
  } finally {
    submitting.value = false;
  }
};

const handleDelegate = async () => {
  if (!delegateTargetIds.value.length) {
    message.warning('请选择委派人');
    return;
  }
  submitting.value = true;
  try {
    await delegateTask(taskId, delegateTargetIds.value[0], delegateComment.value || undefined);
    message.success('委派成功');
    delegateVisible.value = false;
    resetDelegateForm();
    await fetchDetail();
  } catch {
    message.error('委派失败');
  } finally {
    submitting.value = false;
  }
};

const handleMenuClick = ({ key }: { key: string }) => {
  if (key === 'jump') {
    jumpVisible.value = true;
  } else if (key === 'delegate') {
    delegateVisible.value = true;
  } else if (key === 'transfer') {
    transferVisible.value = true;
  }
};

const handleJump = async (targetNodeId: string) => {
  if (!instance.value) return;
  try {
    await jumpTask(String(instance.value.id), targetNodeId, taskId);
    message.success('跳转成功');
    router.back();
  } catch {
    message.error('跳转失败');
  }
};

const resetTransferForm = () => {
  transferTargetIds.value = [];
  transferComment.value = '';
};

const resetDelegateForm = () => {
  delegateTargetIds.value = [];
  delegateComment.value = '';
};

const getStatusColor = (status: number | undefined) => {
  if (status === undefined) return 'default';
  const map: Record<number, string> = {
    [ApprovalTaskStatus.Pending]: 'blue',
    [ApprovalTaskStatus.Approved]: 'green',
    [ApprovalTaskStatus.Rejected]: 'red',
    [ApprovalTaskStatus.Canceled]: 'default'
  };
  return map[status] ?? 'default';
};

const getStatusText = (status: number | undefined) => {
  if (status === undefined) return '';
  const map: Record<number, string> = {
    [ApprovalTaskStatus.Pending]: '待审批',
    [ApprovalTaskStatus.Approved]: '已同意',
    [ApprovalTaskStatus.Rejected]: '已驳回',
    [ApprovalTaskStatus.Canceled]: '已取消'
  };
  return map[status] ?? '未知';
};

const formatDateTime = (value: string) => new Date(value).toLocaleString();

onMounted(() => {
  void fetchDetail();
});
</script>

<style scoped>
.task-detail-page {
  padding: 24px;
  background: #f0f2f5;
  min-height: 100vh;
}
.page-header {
  background: #fff;
  padding: 16px 24px;
  margin-bottom: 24px;
}
.content-layout {
  display: flex;
  gap: 24px;
}
.main-content {
  flex: 1;
}
.side-content {
  width: 350px;
}
.mb-4 {
  margin-bottom: 24px;
}
</style>
