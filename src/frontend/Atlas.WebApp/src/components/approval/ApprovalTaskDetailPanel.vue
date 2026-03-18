<template>
  <div class="task-detail-panel">
    <div class="panel-header">
      <div class="header-title">
        <span class="title-text">{{ task?.title || '任务详情' }}</span>
        <a-tag :color="getStatusColor(task?.status)">{{ getStatusText(task?.status) }}</a-tag>
      </div>
      <div class="header-actions">
        <template v-if="task?.status === ApprovalTaskStatus.Pending">
          <a-button type="primary" size="small" @click="showApproveModal">同意</a-button>
          <a-button danger size="small" @click="showRejectModal">驳回</a-button>
          <a-dropdown>
            <template #overlay>
              <a-menu @click="handleMenuClick">
                <a-menu-item key="transfer">转办</a-menu-item>
                <a-menu-item key="delegate">委派</a-menu-item>
                <a-menu-item key="jump">跳转</a-menu-item>
              </a-menu>
            </template>
            <a-button size="small">更多 <DownOutlined /></a-button>
          </a-dropdown>
        </template>
        <a-button size="small" type="text" @click="$emit('close')">关闭</a-button>
      </div>
    </div>

    <div class="panel-body">
      <a-spin :spinning="loading">
        <a-tabs v-model:activeKey="activeTab" class="detail-tabs">
          <a-tab-pane key="form" tab="表单信息">
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
          
          <a-tab-pane key="history" tab="流转记录">
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
                    <span v-if="item.actorUserId"> · 操作人: {{ item.actorUserId }}</span>
                  </div>
                  <div v-if="item.fromNode || item.toNode" class="timeline-nodes">
                    <span v-if="item.fromNode">从: {{ item.fromNode }}</span>
                    <span v-if="item.fromNode && item.toNode"> → </span>
                    <span v-if="item.toNode">到: {{ item.toNode }}</span>
                  </div>
                </a-timeline-item>
              </a-timeline>
              <a-empty v-else description="暂无流转记录" />
            </div>
          </a-tab-pane>

          <a-tab-pane key="attachments" tab="附件与沟通">
            <div class="attachments-container">
              <FileUploadPanel
                v-model="attachmentFiles"
                :disabled="task?.status !== ApprovalTaskStatus.Pending"
                button-text="上传审批附件"
              />
              <a-divider />
              <h4>沟通记录</h4>
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

    <!-- 弹窗部分 (与原详情页一致) -->
    <a-modal v-model:open="approveVisible" title="审批同意" @ok="handleApprove" :confirm-loading="submitting">
      <a-textarea v-model:value="comment" placeholder="请输入审批意见（选填）" :rows="4" />
    </a-modal>
    <a-modal v-model:open="rejectVisible" title="审批驳回" @ok="handleReject" :confirm-loading="submitting">
      <a-form-item label="驳回原因" required>
        <a-textarea v-model:value="comment" placeholder="请输入驳回原因" :rows="4" />
      </a-form-item>
    </a-modal>
    <!-- 其他弹窗简化省略或保留... -->
    <a-modal v-model:open="transferVisible" title="转办" @ok="handleTransfer" :confirm-loading="submitting">
      <a-form layout="vertical">
        <a-form-item label="转办给" required>
          <UserRolePicker mode="user" v-model:value="transferTargetIds" placeholder="请选择转办人" style="width: 100%" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="transferComment" placeholder="请输入转办说明" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 委派弹窗 -->
    <a-modal v-model:open="delegateVisible" title="委派" @ok="handleDelegate" :confirm-loading="submitting">
      <a-form layout="vertical">
        <a-form-item label="委派给" required>
          <UserRolePicker mode="user" v-model:value="delegateTargetIds" placeholder="请选择被委派人" style="width: 100%" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="delegateComment" placeholder="请输入委派说明" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import { message } from 'ant-design-vue';
import { DownOutlined, CloseOutlined } from '@ant-design/icons-vue';
import {
  getApprovalTaskById, getApprovalInstanceById, getApprovalInstanceHistory,
  decideApprovalTask, transferTask, getApprovalFlowById, getCurrentUser
} from '@/services/api';
import CommunicationPanel from '@/components/approval/runtime/CommunicationPanel.vue';
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
    const [userResult, taskResult] = await Promise.all([
      getCurrentUser(), getApprovalTaskById(props.taskId)
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
      try { parsedDefinition.value = JSON.parse(def.definitionJson); } catch { parsedDefinition.value = null; }
    }
  } catch {
    message.error('获取任务详情失败');
  } finally {
    loading.value = false;
  }
};

watch(() => props.taskId, fetchDetail, { immediate: true });

const showApproveModal = () => { comment.value = '同意'; approveVisible.value = true; };
const showRejectModal = () => { comment.value = ''; rejectVisible.value = true; };

const handleApprove = async () => {
  submitting.value = true;
  try {
    await decideApprovalTask({ taskId: props.taskId, approved: true, comment: comment.value });
    message.success('已同意');
    approveVisible.value = false;
    emit('refresh');
  } catch {
    message.error('审批失败');
  } finally {
    submitting.value = false;
  }
};

const handleReject = async () => {
  if (!comment.value.trim()) { message.warning('请填写驳回原因'); return; }
  submitting.value = true;
  try {
    await decideApprovalTask({ taskId: props.taskId, approved: false, comment: comment.value });
    message.success('已驳回');
    rejectVisible.value = false;
    emit('refresh');
  } catch {
    message.error('驳回失败');
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
    const { delegateTask } = await import('@/services/api-approval');
    await delegateTask(props.taskId, delegateTargetIds.value[0], delegateComment.value || undefined);
    message.success('委派成功');
    delegateVisible.value = false;
    emit('refresh');
  } catch {
    message.error('委派失败');
  } finally {
    submitting.value = false;
  }
};

const handleTransfer = async () => {
  if (!transferTargetIds.value.length || !instance.value) return;
  submitting.value = true;
  try {
    await transferTask(String(instance.value.id), props.taskId, transferTargetIds.value[0], transferComment.value || undefined);
    message.success('转办成功');
    transferVisible.value = false;
    emit('refresh');
  } catch {} finally { submitting.value = false; }
};

const getStatusColor = (status?: number) => status === ApprovalTaskStatus.Pending ? 'blue' : (status === ApprovalTaskStatus.Approved ? 'green' : 'default');
const getStatusText = (status?: number) => status === ApprovalTaskStatus.Pending ? '待审批' : (status === ApprovalTaskStatus.Approved ? '已同意' : '已处理');
const formatDateTime = (value: string) => new Date(value).toLocaleString();

/** 历史事件类型的中文标签映射 */
const EVENT_TYPE_LABELS: Record<string, string> = {
  InstanceStarted: '流程发起',
  InstanceCompleted: '流程完成',
  InstanceRejected: '流程驳回',
  InstanceCanceled: '流程取消',
  InstanceSuspended: '流程挂起',
  InstanceActivated: '流程激活',
  InstanceTerminated: '流程终止',
  TaskCreated: '任务创建',
  TaskApproved: '审批通过',
  TaskRejected: '审批驳回',
  TaskTransferred: '任务转办',
  TaskDelegated: '任务委派',
  TaskClaimed: '任务认领',
  TaskCanceled: '任务取消',
  NodeEntered: '进入节点',
  NodeCompleted: '节点完成',
};

/** 获取历史事件的显示文本 */
const getEventLabel = (eventType: string): string => {
  return EVENT_TYPE_LABELS[eventType] || eventType;
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
