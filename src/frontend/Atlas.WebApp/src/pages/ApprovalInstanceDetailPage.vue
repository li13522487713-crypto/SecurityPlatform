<template>
  <div class="instance-detail-page">
    <div class="page-header">
      <a-page-header
        :title="t('approvalRuntime.pageInstanceTitle', { name: instance?.flowName || '' })"
        @back="$router.back()"
      >
        <template #tags>
          <a-tag :color="getStatusColor(instance?.status)">{{ getStatusText(instance?.status) }}</a-tag>
        </template>
        <template #extra>
          <a-button v-if="canCancel" danger @click="handleCancel">{{ t('approvalRuntime.revokeFlow') }}</a-button>
          <a-button v-if="canSuspend" @click="handleSuspend">{{ t('approvalRuntime.suspend') }}</a-button>
          <a-button v-if="canActivate" type="primary" @click="handleActivate">{{ t('approvalRuntime.activate') }}</a-button>
        </template>
      </a-page-header>
    </div>

    <a-spin :spinning="loading">
      <div class="content-layout">
        <div class="main-content">
          <a-card :title="t('approvalRuntime.cardFormDetail')" class="mb-4">
            <div v-if="instance?.dataJson">
              <LfFormRenderer
                v-if="formJson"
                :form-json="formJson"
                :form-data="parsedFormData"
                :read-only="true"
              />
              <pre v-else>{{ JSON.stringify(JSON.parse(instance.dataJson), null, 2) }}</pre>
            </div>
            <a-empty v-else :description="t('approvalRuntime.emptyFormData')" />
          </a-card>

          <a-card :title="t('approvalRuntime.cardFlowStatus')" class="mb-4">
            <div ref="flowChartRef" class="flow-chart-container">
              <a-steps
                v-if="flowSteps.length > 0"
                direction="vertical"
                size="small"
                :current="currentFlowStepIndex"
                :items="flowSteps"
              />
              <a-empty v-else :description="t('approvalRuntime.emptyFlowStatus')" />
            </div>
          </a-card>
        </div>

        <div class="side-content">
          <a-card :title="t('approvalRuntime.cardApprovalRecords')">
            <a-timeline>
              <a-timeline-item
                v-for="event in historyEvents"
                :key="event.id"
                :color="getEventColor(event.eventType)"
              >
                <template #dot>
                  <component :is="getEventIcon(event.eventType)" />
                </template>
                <div class="timeline-content">
                  <div class="timeline-header">
                    <span class="timeline-user">{{ event.actorUserId ? t('approvalRuntime.userLabel', { id: event.actorUserId }) : t('approvalRuntime.systemLabel') }}</span>
                    <span class="timeline-action">{{ getEventActionText(event.eventType) }}</span>
                  </div>
                  <div v-if="event.payloadJson" class="timeline-comment">{{ event.payloadJson }}</div>
                  <div class="timeline-time">{{ formatTime(event.occurredAt) }}</div>
                </div>
              </a-timeline-item>
            </a-timeline>
          </a-card>
        </div>
      </div>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, onUnmounted } from 'vue';
import { useI18n } from 'vue-i18n';

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute } from 'vue-router';
import { message } from 'ant-design-vue';
import type { Component } from 'vue';
import {
  getApprovalInstanceById,
  getApprovalInstanceHistory,
  cancelApprovalInstance,
  suspendInstance,
  activateInstance,
  getApprovalFlowById
} from '@/services/api';
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  ClockCircleOutlined,
  PlayCircleOutlined,
  StopOutlined
} from '@ant-design/icons-vue';
import {
  ApprovalHistoryEventType,
  ApprovalInstanceStatus
} from '@/types/api';
import type {
  ApprovalHistoryEventDto,
  ApprovalInstanceDetailDto
} from '@/types/approval-instance-detail';
import type { FormJson } from '@/types/approval-definition';
import LfFormRenderer from '@/components/approval/runtime/LfFormRenderer.vue';
import dayjs from 'dayjs';

const { t } = useI18n();
const route = useRoute();
const instanceId = route.params.id as string;

const loading = ref(false);
const instance = ref<ApprovalInstanceDetailDto | null>(null);
const historyEvents = ref<ApprovalHistoryEventDto[]>([]);
const flowChartRef = ref<HTMLElement | null>(null);
const formJson = ref<FormJson | undefined>(undefined);

const parsedFormData = computed<Record<string, unknown>>(() => {
  if (!instance.value?.dataJson) return {};
  try {
    const parsed = JSON.parse(instance.value.dataJson);
    return typeof parsed === 'object' && parsed !== null ? (parsed as Record<string, unknown>) : {};
  } catch {
    return {};
  }
});

const canCancel = computed(() => instance.value?.status === ApprovalInstanceStatus.Running);
const canSuspend = computed(() => instance.value?.status === ApprovalInstanceStatus.Running);
const canActivate = computed(() => instance.value?.status === ApprovalInstanceStatus.Suspended);

const flowSteps = computed(() => {
  return historyEvents.value
    .slice()
    .sort((a, b) => dayjs(a.occurredAt).valueOf() - dayjs(b.occurredAt).valueOf())
    .map((event) => ({
      title: getEventActionText(event.eventType),
      description: `${event.actorUserId ? t('approvalRuntime.userLabel', { id: event.actorUserId }) : t('approvalRuntime.systemLabel')} · ${formatTime(event.occurredAt)}`
    }));
});

const currentFlowStepIndex = computed(() => {
  if (flowSteps.value.length === 0) {
    return 0;
  }

  return Math.max(flowSteps.value.length - 1, 0);
});

const fetchDetail = async () => {
  loading.value = true;
  try {
    const res  = await getApprovalInstanceById(instanceId);

    if (!isMounted.value) return;
    instance.value = res;

    const historyRes  = await getApprovalInstanceHistory(instanceId, { pageIndex: 1, pageSize: 100 });


    if (!isMounted.value) return;
    historyEvents.value = historyRes.items;

    if (res.definitionId) {
      try {
        const flowDef  = await getApprovalFlowById(String(res.definitionId));

        if (!isMounted.value) return;
        if (flowDef.definitionJson) {
          const defParsed = JSON.parse(flowDef.definitionJson) as { formJson?: FormJson };
          if (defParsed.formJson) {
            formJson.value = defParsed.formJson;
          }
        }
      } catch {
        // ignore
      }
    }

  } catch {
    message.error(t('approvalRuntime.loadDetailFailed'));
  } finally {
    loading.value = false;
  }
};

const handleCancel = async () => {
  try {
    await cancelApprovalInstance(instanceId);

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.cancelSuccess'));
    fetchDetail();
  } catch {
    message.error(t('approvalRuntime.cancelFailed'));
  }
};

const handleSuspend = async () => {
  try {
    await suspendInstance(instanceId);

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.suspendSuccess'));
    fetchDetail();
  } catch {
    message.error(t('approvalRuntime.suspendFailed'));
  }
};

const handleActivate = async () => {
  try {
    await activateInstance(instanceId);

    if (!isMounted.value) return;
    message.success(t('approvalRuntime.activateSuccess'));
    fetchDetail();
  } catch {
    message.error(t('approvalRuntime.activateFailed'));
  }
};

const getStatusColor = (status?: ApprovalInstanceDetailDto['status']) => {
  if (status === undefined) {
    return 'default';
  }

  const map: Record<ApprovalInstanceDetailDto['status'], string> = {
    [ApprovalInstanceStatus.Destroy]: 'default',
    [ApprovalInstanceStatus.Suspended]: 'orange',
    [ApprovalInstanceStatus.Draft]: 'purple',
    [ApprovalInstanceStatus.Running]: 'blue',
    [ApprovalInstanceStatus.Completed]: 'green',
    [ApprovalInstanceStatus.Rejected]: 'red',
    [ApprovalInstanceStatus.Canceled]: 'default',
    [ApprovalInstanceStatus.TimedOut]: 'volcano',
    [ApprovalInstanceStatus.Terminated]: 'magenta',
    [ApprovalInstanceStatus.AutoApproved]: 'cyan',
    [ApprovalInstanceStatus.AutoRejected]: 'geekblue',
    [ApprovalInstanceStatus.AiProcessing]: 'processing',
    [ApprovalInstanceStatus.AiManualReview]: 'gold'
  };
  return map[status] || 'default';
};

const getStatusText = (status?: ApprovalInstanceDetailDto['status']) => {
  if (status === undefined) {
    return t('approvalRuntime.instStatusUnknown');
  }

  const map: Record<ApprovalInstanceDetailDto['status'], string> = {
    [ApprovalInstanceStatus.Destroy]: t('approvalRuntime.instStatusDestroy'),
    [ApprovalInstanceStatus.Suspended]: t('approvalRuntime.instStatusSuspended'),
    [ApprovalInstanceStatus.Draft]: t('approvalRuntime.instStatusDraft'),
    [ApprovalInstanceStatus.Running]: t('approvalRuntime.instStatusRunning'),
    [ApprovalInstanceStatus.Completed]: t('approvalRuntime.instStatusCompleted'),
    [ApprovalInstanceStatus.Rejected]: t('approvalRuntime.instStatusRejected'),
    [ApprovalInstanceStatus.Canceled]: t('approvalRuntime.instStatusCanceled'),
    [ApprovalInstanceStatus.TimedOut]: t('approvalRuntime.instDetailTimedOutEnd'),
    [ApprovalInstanceStatus.Terminated]: t('approvalRuntime.instDetailTerminated'),
    [ApprovalInstanceStatus.AutoApproved]: t('approvalRuntime.instStatusAutoApproved'),
    [ApprovalInstanceStatus.AutoRejected]: t('approvalRuntime.instDetailAutoReject'),
    [ApprovalInstanceStatus.AiProcessing]: t('approvalRuntime.instStatusAiProcessing'),
    [ApprovalInstanceStatus.AiManualReview]: t('approvalRuntime.instStatusAiManual')
  };
  return map[status] || t('approvalRuntime.instStatusUnknown');
};

const getEventColor = (type: ApprovalHistoryEventDto['eventType']) => {
  const map: Partial<Record<ApprovalHistoryEventDto['eventType'], string>> = {
    [ApprovalHistoryEventType.TaskApproved]: 'green',
    [ApprovalHistoryEventType.InstanceCompleted]: 'green',
    [ApprovalHistoryEventType.TaskRejected]: 'red',
    [ApprovalHistoryEventType.InstanceRejected]: 'red',
    [ApprovalHistoryEventType.InstanceCanceled]: 'orange',
    [ApprovalHistoryEventType.InstanceSuspended]: 'orange',
    [ApprovalHistoryEventType.InstanceActivated]: 'blue'
  };
  return map[type] ?? 'blue';
};

const getEventIcon = (type: ApprovalHistoryEventDto['eventType']): Component => {
  if (type === ApprovalHistoryEventType.TaskApproved || type === ApprovalHistoryEventType.InstanceCompleted) {
    return CheckCircleOutlined;
  }
  if (type === ApprovalHistoryEventType.TaskRejected || type === ApprovalHistoryEventType.InstanceRejected) {
    return CloseCircleOutlined;
  }
  if (type === ApprovalHistoryEventType.InstanceActivated) {
    return PlayCircleOutlined;
  }
  if (type === ApprovalHistoryEventType.InstanceSuspended) {
    return StopOutlined;
  }
  return ClockCircleOutlined;
};

const getEventActionText = (type: ApprovalHistoryEventDto['eventType']) => {
  const map: Partial<Record<ApprovalHistoryEventDto['eventType'], string>> = {
    [ApprovalHistoryEventType.InstanceStarted]: t('approvalRuntime.historyEventInstanceStarted'),
    [ApprovalHistoryEventType.TaskCreated]: t('approvalRuntime.historyEventTaskCreated'),
    [ApprovalHistoryEventType.TaskApproved]: t('approvalRuntime.historyEventTaskApproved'),
    [ApprovalHistoryEventType.TaskRejected]: t('approvalRuntime.historyEventTaskRejected'),
    [ApprovalHistoryEventType.NodeAdvanced]: t('approvalRuntime.historyEventNodeAdvanced'),
    [ApprovalHistoryEventType.InstanceCompleted]: t('approvalRuntime.historyEventInstanceCompleted'),
    [ApprovalHistoryEventType.InstanceRejected]: t('approvalRuntime.historyEventInstanceRejected'),
    [ApprovalHistoryEventType.InstanceCanceled]: t('approvalRuntime.historyEventInstanceCanceled'),
    [ApprovalHistoryEventType.InstanceSuspended]: t('approvalRuntime.historyEventInstanceSuspended'),
    [ApprovalHistoryEventType.InstanceActivated]: t('approvalRuntime.historyEventInstanceActivated'),
    [ApprovalHistoryEventType.InstanceTerminated]: t('approvalRuntime.historyEventInstanceTerminated')
  };
  return map[type] ?? t('approvalRuntime.historyEventFallback');
};

const formatTime = (time: string) => {
  return dayjs(time).format('YYYY-MM-DD HH:mm:ss');
};

onMounted(() => {
  fetchDetail();
});
</script>

<style scoped>
.instance-detail-page {
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
.flow-chart-container {
  height: 400px;
  background: #fafafa;
  border: 1px solid #eee;
}
.timeline-header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 4px;
}
.timeline-user {
  font-weight: 500;
}
.timeline-time {
  color: #999;
  font-size: 12px;
  margin-top: 4px;
}
</style>
