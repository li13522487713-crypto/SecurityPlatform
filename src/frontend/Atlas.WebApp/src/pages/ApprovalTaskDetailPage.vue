<template>
  <div class="task-detail-page">
    <div class="page-header">
      <a-page-header
        :title="t('approvalRuntime.pageTaskTitle', { title: task?.title || '' })"
        @back="$router.back()"
      >
        <template #tags>
          <a-tag :color="getStatusColor(task?.status)">{{ getStatusText(task?.status) }}</a-tag>
        </template>
        <template #extra>
          <template v-if="task?.status === ApprovalTaskStatus.Pending">
            <a-button type="primary" @click="showApproveModal">{{ t('approvalRuntime.actionsAgree') }}</a-button>
            <a-button danger @click="showRejectModal">{{ t('approvalRuntime.actionsReject') }}</a-button>
            <a-dropdown>
              <template #overlay>
                <a-menu @click="handleMenuClick">
                  <a-menu-item key="transfer">{{ t('approvalRuntime.menuTransfer') }}</a-menu-item>
                  <a-menu-item key="delegate">{{ t('approvalRuntime.menuDelegate') }}</a-menu-item>
                  <a-menu-item key="jump">{{ t('approvalRuntime.menuJump') }}</a-menu-item>
                  <a-menu-item key="communicate">{{ t('approvalRuntime.menuCommunicate') }}</a-menu-item>
                </a-menu>
              </template>
              <a-button>{{ t('approvalRuntime.moreActions') }} <DownOutlined /></a-button>
            </a-dropdown>
          </template>
        </template>
      </a-page-header>
    </div>

    <a-spin :spinning="loading">
      <div class="content-layout">
        <div class="main-content">
          <a-card :title="t('approvalRuntime.cardBusinessForm')" class="mb-4">
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

          <a-card :title="t('approvalRuntime.cardApprovalTrack')">
            <a-timeline v-if="historyItems.length > 0">
              <a-timeline-item v-for="item in historyItems" :key="item.id">
                <div class="timeline-title">{{ item.eventType }}</div>
                <div class="timeline-meta">
                  {{ formatDateTime(item.occurredAt) }}
                  <span v-if="item.actorUserId"> · {{ t('approvalRuntime.actorPrefix') }} {{ item.actorUserId }}</span>
                  <span v-if="item.fromNode || item.toNode">
                    · {{ item.fromNode || '-' }} -> {{ item.toNode || '-' }}
                  </span>
                </div>
              </a-timeline-item>
            </a-timeline>
            <a-empty v-else :description="t('approvalRuntime.emptyHistory')" />
          </a-card>
        </div>

        <div class="side-content">
          <a-card :title="t('approvalRuntime.cardAttachments')" class="mb-4">
            <FileUploadPanel
              v-model="attachmentFiles"
              :disabled="task?.status !== ApprovalTaskStatus.Pending"
              :button-text="t('approvalRuntime.attachmentButtonText')"
            />
          </a-card>

          <a-card :title="t('approvalRuntime.cardCommunication')" class="mb-4">
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

    <a-modal
      v-model:open="approveVisible"
      :title="t('approvalRuntime.approveModalTitle')"
      :confirm-loading="submitting"
      @ok="handleApprove"
    >
      <a-textarea v-model:value="comment" :placeholder="t('approvalRuntime.approveCommentPlaceholder')" :rows="4" />
    </a-modal>

    <a-modal
      v-model:open="rejectVisible"
      :title="t('approvalRuntime.rejectModalTitle')"
      :confirm-loading="submitting"
      @ok="handleReject"
    >
      <a-form-item :label="t('approvalRuntime.rejectReasonLabel')" required>
        <a-textarea v-model:value="comment" :placeholder="t('approvalRuntime.rejectPlaceholder')" :rows="4" />
      </a-form-item>
    </a-modal>

    <a-modal
      v-model:open="transferVisible"
      :title="t('approvalRuntime.transferModalTitle')"
      :confirm-loading="submitting"
      @ok="handleTransfer"
      @cancel="resetTransferForm"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('approvalRuntime.transferToLabel')" required>
          <UserRolePicker
            v-model:value="transferTargetIds"
            mode="user"
            :placeholder="t('approvalRuntime.transferUserPlaceholder')"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item :label="t('approvalRuntime.remarkLabel')">
          <a-textarea v-model:value="transferComment" :placeholder="t('approvalRuntime.transferRemarkPlaceholder')" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="delegateVisible"
      :title="t('approvalRuntime.delegateModalTitle')"
      :confirm-loading="submitting"
      @ok="handleDelegate"
      @cancel="resetDelegateForm"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('approvalRuntime.delegateToLabel')" required>
          <UserRolePicker
            v-model:value="delegateTargetIds"
            mode="user"
            :placeholder="t('approvalRuntime.delegateUserPlaceholder')"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item :label="t('approvalRuntime.remarkLabel')">
          <a-textarea v-model:value="delegateComment" :placeholder="t('approvalRuntime.delegateRemarkPlaceholder')" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>

    <JumpNodeSelector
      v-model:visible="jumpVisible"
      :flow-definition="flowDefinitionNodes"
      @select="handleJump"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useI18n } from 'vue-i18n';

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

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

const { t } = useI18n();
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
    const [userResult, taskResult]  = await Promise.all([
      getCurrentUser(),
      getApprovalTaskById(taskId)
    ]);

    if (!isMounted.value) return;

    currentUserId.value = userResult.id;
    task.value = taskResult;

    const [instanceResult, historyResult]  = await Promise.all([
      getApprovalInstanceById(taskResult.instanceId),
      getApprovalInstanceHistory(taskResult.instanceId, { pageIndex: 1, pageSize: 50 })
    ]);


    if (!isMounted.value) return;
    instance.value = instanceResult;
    historyItems.value = historyResult.items;

    if (instanceResult.definitionId) {
      const def  = await getApprovalFlowById(String(instanceResult.definitionId));

      if (!isMounted.value) return;
      flowDefinition.value = def;
      try {
        parsedDefinition.value = JSON.parse(def.definitionJson) as ApprovalDefinitionJson;
      } catch {
        parsedDefinition.value = null;
      }
    }
  } catch {
    message.error(t('approvalRuntime.loadTaskFailed'));
  } finally {
    loading.value = false;
  }
};

const showApproveModal = () => {
  comment.value = t('approvalRuntime.defaultAgreeComment');
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

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.approveSuccess'));
    approveVisible.value = false;
    router.back();
  } catch {
    message.error(t('approvalRuntime.approveFailed'));
  } finally {
    submitting.value = false;
  }
};

const handleReject = async () => {
  if (!comment.value.trim()) {
    message.warning(t('approvalRuntime.rejectWarnReason'));
    return;
  }
  submitting.value = true;
  try {
    await decideApprovalTask({ taskId, approved: false, comment: comment.value });

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.rejectSuccess'));
    rejectVisible.value = false;
    router.back();
  } catch {
    message.error(t('approvalRuntime.rejectFailed'));
  } finally {
    submitting.value = false;
  }
};

const handleTransfer = async () => {
  if (!transferTargetIds.value.length) {
    message.warning(t('approvalRuntime.transferWarnUser'));
    return;
  }
  if (!instance.value) return;
  submitting.value = true;
  try {
    await transferTask(String(instance.value.id), taskId, transferTargetIds.value[0], transferComment.value || undefined);

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.transferSuccess'));
    transferVisible.value = false;
    resetTransferForm();
    await fetchDetail();

    if (!isMounted.value) return;
  } catch {
    message.error(t('approvalRuntime.transferFailed'));
  } finally {
    submitting.value = false;
  }
};

const handleDelegate = async () => {
  if (!delegateTargetIds.value.length) {
    message.warning(t('approvalRuntime.delegateWarnUser'));
    return;
  }
  submitting.value = true;
  try {
    await delegateTask(taskId, delegateTargetIds.value[0], delegateComment.value || undefined);

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.delegateSuccess'));
    delegateVisible.value = false;
    resetDelegateForm();
    await fetchDetail();

    if (!isMounted.value) return;
  } catch {
    message.error(t('approvalRuntime.delegateFailed'));
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

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.jumpSuccess'));
    router.back();
  } catch {
    message.error(t('approvalRuntime.jumpFailed'));
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
    [ApprovalTaskStatus.Pending]: t('approvalRuntime.taskStatusPending'),
    [ApprovalTaskStatus.Approved]: t('approvalRuntime.taskStatusApproved'),
    [ApprovalTaskStatus.Rejected]: t('approvalRuntime.taskStatusRejected'),
    [ApprovalTaskStatus.Canceled]: t('approvalRuntime.taskStatusCanceled')
  };
  return map[status] ?? t('approvalRuntime.taskStatusUnknown');
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
