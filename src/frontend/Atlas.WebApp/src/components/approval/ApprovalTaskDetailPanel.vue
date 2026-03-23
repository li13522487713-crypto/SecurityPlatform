<template>
  <div class="task-detail-panel">
    <div class="panel-header">
      <div class="header-title">
        <span class="title-text">{{ task?.title || t('approvalTaskPanel.defaultTitle') }}</span>
        <a-tag :color="getStatusColor(task?.status)">{{ getStatusText(task?.status) }}</a-tag>
      </div>
      <div class="header-actions">
        <template v-if="task?.status === ApprovalTaskStatus.Pending">
          <a-button type="primary" size="small" @click="showApproveModal">{{ t('approvalRuntime.actionsAgree') }}</a-button>
          <a-button danger size="small" @click="showRejectModal">{{ t('approvalRuntime.actionsReject') }}</a-button>
          <a-dropdown>
            <template #overlay>
              <a-menu @click="handleMenuClick">
                <a-menu-item key="transfer">{{ t('approvalRuntime.menuTransfer') }}</a-menu-item>
                <a-menu-item key="delegate">{{ t('approvalRuntime.menuDelegate') }}</a-menu-item>
                <a-menu-item key="jump">{{ t('approvalRuntime.menuJump') }}</a-menu-item>
              </a-menu>
            </template>
            <a-button size="small">{{ t('approvalTaskPanel.more') }} <DownOutlined /></a-button>
          </a-dropdown>
        </template>
        <a-button size="small" type="text" @click="$emit('close')">{{ t('approvalTaskPanel.close') }}</a-button>
      </div>
    </div>

    <div class="panel-body">
      <a-spin :spinning="loading">
        <a-tabs v-model:active-key="activeTab" class="detail-tabs">
          <a-tab-pane key="form" :tab="t('approvalTaskPanel.tabForm')">
            <div class="form-container">
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
            </div>
          </a-tab-pane>

          <a-tab-pane key="history" :tab="t('approvalTaskPanel.tabHistory')">
            <div class="history-container">
              <a-timeline v-if="historyItems.length > 0">
                <a-timeline-item
                  v-for="item in historyItems"
                  :key="item.id"
                  :color="item.eventType === 'TaskApproved' || item.eventType === 'InstanceCompleted' ? 'green' : (item.eventType === 'TaskRejected' || item.eventType === 'InstanceRejected' ? 'red' : 'blue')"
                >
                  <div class="timeline-title">{{ getEventLabel(item.eventType) }}</div>
                  <div class="timeline-meta">
                    {{ formatDateTime(item.occurredAt) }}
                    <span v-if="item.actorUserId"> · {{ t('approvalRuntime.actorPrefix') }} {{ item.actorUserId }}</span>
                  </div>
                  <div v-if="item.fromNode || item.toNode" class="timeline-nodes">
                    <span v-if="item.fromNode">{{ t('approvalTaskPanel.nodeFrom') }} {{ item.fromNode }}</span>
                    <span v-if="item.fromNode && item.toNode"> → </span>
                    <span v-if="item.toNode">{{ t('approvalTaskPanel.nodeTo') }} {{ item.toNode }}</span>
                  </div>
                </a-timeline-item>
              </a-timeline>
              <a-empty v-else :description="t('approvalTaskPanel.emptyFlowHistory')" />
            </div>
          </a-tab-pane>

          <a-tab-pane key="attachments" :tab="t('approvalTaskPanel.tabAttachments')">
            <div class="attachments-container">
              <FileUploadPanel
                v-model="attachmentFiles"
                :disabled="task?.status !== ApprovalTaskStatus.Pending"
                :button-text="t('approvalRuntime.attachmentButtonText')"
              />
              <a-divider />
              <h4>{{ t('approvalTaskPanel.commHeading') }}</h4>
              <CommunicationPanel
                v-if="task"
                :task-id="task.id"
                :current-user-id="currentUserId"
                style="height: 300px"
              />
            </div>
          </a-tab-pane>
        </a-tabs>
      </a-spin>
    </div>

    <a-modal v-model:open="approveVisible" :title="t('approvalRuntime.approveModalTitle')" :confirm-loading="submitting" @ok="handleApprove">
      <a-textarea v-model:value="comment" :placeholder="t('approvalRuntime.approveCommentPlaceholder')" :rows="4" />
    </a-modal>
    <a-modal v-model:open="rejectVisible" :title="t('approvalRuntime.rejectModalTitle')" :confirm-loading="submitting" @ok="handleReject">
      <a-form-item :label="t('approvalRuntime.rejectReasonLabel')" required>
        <a-textarea v-model:value="comment" :placeholder="t('approvalRuntime.rejectPlaceholder')" :rows="4" />
      </a-form-item>
    </a-modal>
    <a-modal v-model:open="transferVisible" :title="t('approvalRuntime.transferModalTitle')" :confirm-loading="submitting" @ok="handleTransfer">
      <a-form layout="vertical">
        <a-form-item :label="t('approvalRuntime.transferToLabel')" required>
          <UserRolePicker v-model:value="transferTargetIds" mode="user" :placeholder="t('approvalRuntime.transferUserPlaceholder')" style="width: 100%" />
        </a-form-item>
        <a-form-item :label="t('approvalRuntime.remarkLabel')">
          <a-textarea v-model:value="transferComment" :placeholder="t('approvalTaskPanel.transferRemarkPlaceholder')" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal v-model:open="delegateVisible" :title="t('approvalRuntime.delegateModalTitle')" :confirm-loading="submitting" @ok="handleDelegate">
      <a-form layout="vertical">
        <a-form-item :label="t('approvalRuntime.delegateToLabel')" required>
          <UserRolePicker v-model:value="delegateTargetIds" mode="user" :placeholder="t('approvalTaskPanel.delegatePlaceholderTarget')" style="width: 100%" />
        </a-form-item>
        <a-form-item :label="t('approvalRuntime.remarkLabel')">
          <a-textarea v-model:value="delegateComment" :placeholder="t('approvalRuntime.delegateRemarkPlaceholder')" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>

    <JumpNodeSelector
      :visible="jumpVisible"
      :flow-definition="parsedDefinition"
      @update:visible="jumpVisible = $event"
      @select="handleJump"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue';
import { useI18n } from 'vue-i18n';

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { message } from 'ant-design-vue';
import { DownOutlined } from '@ant-design/icons-vue';
import {
  getApprovalTaskById, getApprovalInstanceById, getApprovalInstanceHistory,
  decideApprovalTask, transferTask, getApprovalFlowById, getCurrentUser
} from '@/services/api';
import CommunicationPanel from '@/components/approval/runtime/CommunicationPanel.vue';
import JumpNodeSelector from '@/components/approval/runtime/JumpNodeSelector.vue';
import LfFormRenderer from '@/components/approval/runtime/LfFormRenderer.vue';
import AmisRenderer from '@/components/amis/amis-renderer.vue';
import UserRolePicker from '@/components/common/UserRolePicker.vue';
import FileUploadPanel from '@/components/common/file-upload-panel.vue';
import type {
  ApprovalTaskResponse, ApprovalInstanceResponse, ApprovalFlowDefinitionResponse,
  ApprovalHistoryEventResponse, FileUploadResult, JsonValue
} from '@/types/api';
import type { ApprovalDefinitionJson, FormJson } from '@/types/approval-definition';
import { ApprovalTaskStatus } from '@/types/api';

const { t } = useI18n();

const props = defineProps<{ taskId: string }>();
const emit = defineEmits(['close', 'refresh']);

const loading = ref(false);
const submitting = ref(false);
const activeTab = ref('form');
const task = ref<ApprovalTaskResponse | null>(null);
const instance = ref<ApprovalInstanceResponse | null>(null);
const parsedDefinition = ref<ApprovalDefinitionJson | null>(null);
const currentUserId = ref('');
const historyItems = ref<ApprovalHistoryEventResponse[]>([]);

const approveVisible = ref(false);
const rejectVisible = ref(false);
const transferVisible = ref(false);
const attachmentFiles = ref<FileUploadResult[]>([]);
const comment = ref('');
const transferTargetIds = ref<string[]>([]);
const transferComment = ref('');

const formJson = computed<FormJson | null>(() => parsedDefinition.value?.lfForm?.formJson ?? null);
const amisSchema = computed<Record<string, JsonValue> | null>(() => {
  const schema = parsedDefinition.value?.amisForm?.schema;
  return (schema && typeof schema === 'object') ? schema as Record<string, JsonValue> : null;
});
const formData = computed<Record<string, unknown>>(() => {
  if (!instance.value?.dataJson) return {};
  try { return JSON.parse(instance.value.dataJson); } catch { return {}; }
});
const formDataForAmis = computed<Record<string, JsonValue>>(() => formData.value as Record<string, JsonValue>);

const fetchDetail = async () => {
  if (!props.taskId) return;
  loading.value = true;
  try {
    const [userResult, taskResult]  = await Promise.all([
      getCurrentUser(), getApprovalTaskById(props.taskId)
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
      try { parsedDefinition.value = JSON.parse(def.definitionJson); } catch { parsedDefinition.value = null; }
    }
  } catch {
    message.error(t('approvalRuntime.loadTaskFailed'));
  } finally {
    loading.value = false;
  }
};

watch(() => props.taskId, fetchDetail, { immediate: true });

const showApproveModal = () => { comment.value = t('approvalRuntime.defaultAgreeComment'); approveVisible.value = true; };
const showRejectModal = () => { comment.value = ''; rejectVisible.value = true; };

const handleApprove = async () => {
  submitting.value = true;
  try {
    await decideApprovalTask({ taskId: props.taskId, approved: true, comment: comment.value });

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.approveSuccess'));
    approveVisible.value = false;
    emit('refresh');
  } catch {
    message.error(t('approvalTaskPanel.approveFailed'));
  } finally {
    submitting.value = false;
  }
};

const handleReject = async () => {
  if (!comment.value.trim()) { message.warning(t('approvalRuntime.rejectWarnReason')); return; }
  submitting.value = true;
  try {
    await decideApprovalTask({ taskId: props.taskId, approved: false, comment: comment.value });

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.rejectSuccess'));
    rejectVisible.value = false;
    emit('refresh');
  } catch {
    message.error(t('approvalTaskPanel.rejectFailed'));
  } finally {
    submitting.value = false;
  }
};

const delegateVisible = ref(false);
const delegateTargetIds = ref<string[]>([]);
const delegateComment = ref('');
const jumpVisible = ref(false);

const handleMenuClick = ({ key }: { key: string }) => {
  if (key === 'transfer') transferVisible.value = true;
  else if (key === 'delegate') delegateVisible.value = true;
  else if (key === 'jump') jumpVisible.value = true;
};

const handleDelegate = async () => {
  if (!delegateTargetIds.value.length || !task.value) return;
  submitting.value = true;
  try {
    const { delegateTask }  = await import('@/services/api-approval');

    if (!isMounted.value) return;
    await delegateTask(props.taskId, delegateTargetIds.value[0], delegateComment.value || undefined);

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.delegateSuccess'));
    delegateVisible.value = false;
    emit('refresh');
  } catch {
    message.error(t('approvalRuntime.delegateFailed'));
  } finally {
    submitting.value = false;
  }
};

const handleJump = async (targetNodeId: string) => {
  if (!instance.value || !task.value) return;
  submitting.value = true;
  try {
    const { jumpTask }  = await import('@/services/api-approval');

    if (!isMounted.value) return;
    await jumpTask(String(instance.value.id), targetNodeId, props.taskId);

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.jumpSuccess'));
    jumpVisible.value = false;
    emit('refresh');
  } catch {
    message.error(t('approvalRuntime.jumpFailed'));
  } finally {
    submitting.value = false;
  }
};

const handleTransfer = async () => {
  if (!transferTargetIds.value.length || !instance.value) return;
  submitting.value = true;
  try {
    await transferTask(String(instance.value.id), props.taskId, transferTargetIds.value[0], transferComment.value || undefined);

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.transferSuccess'));
    transferVisible.value = false;
    emit('refresh');
  } catch {} finally { submitting.value = false; }
};

const getStatusColor = (status?: number) => status === ApprovalTaskStatus.Pending ? 'blue' : (status === ApprovalTaskStatus.Approved ? 'green' : 'default');
const getStatusText = (status?: number) => {
  if (status === ApprovalTaskStatus.Pending) return t('approvalTaskPanel.statusPending');
  if (status === ApprovalTaskStatus.Approved) return t('approvalTaskPanel.statusApproved');
  return t('approvalTaskPanel.statusProcessed');
};
const formatDateTime = (value: string) => new Date(value).toLocaleString();

const getEventLabel = (eventType: string): string => {
  const map: Record<string, string> = {
    InstanceStarted: t('approvalTaskPanel.historyEventInstanceStarted'),
    InstanceCompleted: t('approvalTaskPanel.historyEventInstanceCompleted'),
    InstanceRejected: t('approvalTaskPanel.historyEventInstanceRejected'),
    InstanceCanceled: t('approvalTaskPanel.historyEventInstanceCanceled'),
    InstanceSuspended: t('approvalTaskPanel.historyEventInstanceSuspended'),
    InstanceActivated: t('approvalTaskPanel.historyEventInstanceActivated'),
    InstanceTerminated: t('approvalTaskPanel.historyEventInstanceTerminated'),
    TaskCreated: t('approvalTaskPanel.historyEventTaskCreated'),
    TaskApproved: t('approvalTaskPanel.historyEventTaskApproved'),
    TaskRejected: t('approvalTaskPanel.historyEventTaskRejected'),
    TaskTransferred: t('approvalTaskPanel.historyEventTaskTransferred'),
    TaskDelegated: t('approvalTaskPanel.historyEventTaskDelegated'),
    TaskClaimed: t('approvalTaskPanel.historyEventTaskClaimed'),
    TaskCanceled: t('approvalTaskPanel.historyEventTaskCanceled'),
    NodeEntered: t('approvalTaskPanel.historyEventNodeEntered'),
    NodeCompleted: t('approvalTaskPanel.historyEventNodeCompleted'),
  };
  return map[eventType] || eventType;
};
</script>

<style scoped>
.task-detail-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--color-bg-container);
  border-left: 1px solid var(--color-border);
}
.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid var(--color-border);
}
.header-title {
  display: flex;
  align-items: center;
  gap: 12px;
}
.title-text {
  font-size: 16px;
  font-weight: 600;
  color: var(--color-text-primary);
}
.header-actions {
  display: flex;
  gap: 8px;
}
.panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 0 20px;
}
.detail-tabs :deep(.ant-tabs-nav) {
  margin-bottom: 16px;
}
.form-container, .history-container, .attachments-container {
  padding-bottom: 24px;
}
.timeline-title {
  font-weight: 500;
  color: var(--color-text-primary);
}
.timeline-meta {
  font-size: 12px;
  color: var(--color-text-tertiary);
  margin-top: 4px;
}
.timeline-nodes {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-top: 2px;
}
</style>
