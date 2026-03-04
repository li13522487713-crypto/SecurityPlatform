<template>
  <div class="instance-detail-page">
    <div class="page-header">
      <a-page-header
        :title="`流程详情: ${instance?.flowName || ''}`"
        @back="$router.back()"
      >
        <template #tags>
          <a-tag :color="getStatusColor(instance?.status)">{{ getStatusText(instance?.status) }}</a-tag>
        </template>
        <template #extra>
          <a-button v-if="canCancel" danger @click="handleCancel">撤销流程</a-button>
          <a-button v-if="canSuspend" @click="handleSuspend">挂起</a-button>
          <a-button v-if="canActivate" type="primary" @click="handleActivate">激活</a-button>
        </template>
      </a-page-header>
    </div>

    <a-spin :spinning="loading">
      <div class="content-layout">
        <div class="main-content">
          <!-- 表单详情 (只读) -->
          <a-card title="表单详情" class="mb-4">
            <div v-if="instance?.dataJson">
              <!-- 这里应该使用表单渲染器，暂时用 JSON 展示 -->
              <pre>{{ JSON.stringify(JSON.parse(instance.dataJson), null, 2) }}</pre>
            </div>
          </a-card>

          <!-- 流程图状态 -->
          <a-card title="流程状态" class="mb-4">
            <div class="flow-chart-container" ref="flowChartRef"></div>
          </a-card>
        </div>

        <div class="side-content">
          <!-- 审批记录时间线 -->
          <a-card title="审批记录">
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
                    <span class="timeline-user">{{ event.actorUserId ? `用户 ${event.actorUserId}` : '系统' }}</span>
                    <span class="timeline-action">{{ getEventActionText(event.eventType) }}</span>
                  </div>
                  <div class="timeline-comment" v-if="event.payloadJson">{{ event.payloadJson }}</div>
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
import { ref, onMounted, computed } from 'vue';
import { useRoute } from 'vue-router';
import { message } from 'ant-design-vue';
import type { Component } from 'vue';
import { 
  getApprovalInstanceById, 
  getApprovalInstanceHistory, 
  cancelApprovalInstance,
  suspendInstance,
  activateInstance
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
import dayjs from 'dayjs';

const route = useRoute();
const instanceId = route.params.id as string;

const loading = ref(false);
const instance = ref<ApprovalInstanceDetailDto | null>(null);
const historyEvents = ref<ApprovalHistoryEventDto[]>([]);
const flowChartRef = ref<HTMLElement | null>(null);

const canCancel = computed(() => instance.value?.status === ApprovalInstanceStatus.Running);
const canSuspend = computed(() => instance.value?.status === ApprovalInstanceStatus.Running);
const canActivate = computed(() => instance.value?.status === ApprovalInstanceStatus.Suspended);

const fetchDetail = async () => {
  loading.value = true;
  try {
    const res = await getApprovalInstanceById(instanceId);
    instance.value = res;
    
    const historyRes = await getApprovalInstanceHistory(instanceId, { pageIndex: 1, pageSize: 100 });
    historyEvents.value = historyRes.items;
    
    // TODO: Render Flow Chart with Status
  } catch (error) {
    message.error('获取详情失败');
  } finally {
    loading.value = false;
  }
};

const handleCancel = async () => {
  try {
    await cancelApprovalInstance(instanceId);
    message.success('已撤销');
    fetchDetail();
  } catch (error) {
    message.error('撤销失败');
  }
};

const handleSuspend = async () => {
  try {
    await suspendInstance(instanceId);
    message.success('已挂起');
    fetchDetail();
  } catch (error) {
    message.error('挂起失败');
  }
};

const handleActivate = async () => {
  try {
    await activateInstance(instanceId);
    message.success('已激活');
    fetchDetail();
  } catch (error) {
    message.error('激活失败');
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
    return '未知';
  }

  const map: Record<ApprovalInstanceDetailDto['status'], string> = {
    [ApprovalInstanceStatus.Destroy]: '已作废',
    [ApprovalInstanceStatus.Suspended]: '已挂起',
    [ApprovalInstanceStatus.Draft]: '草稿',
    [ApprovalInstanceStatus.Running]: '运行中',
    [ApprovalInstanceStatus.Completed]: '已完成',
    [ApprovalInstanceStatus.Rejected]: '已驳回',
    [ApprovalInstanceStatus.Canceled]: '已取消',
    [ApprovalInstanceStatus.TimedOut]: '已超时结束',
    [ApprovalInstanceStatus.Terminated]: '已强制终止',
    [ApprovalInstanceStatus.AutoApproved]: '自动通过',
    [ApprovalInstanceStatus.AutoRejected]: '自动拒绝',
    [ApprovalInstanceStatus.AiProcessing]: 'AI 处理中',
    [ApprovalInstanceStatus.AiManualReview]: 'AI 转人工'
  };
  return map[status] || '未知';
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
    [ApprovalHistoryEventType.InstanceStarted]: '发起流程',
    [ApprovalHistoryEventType.TaskCreated]: '创建任务',
    [ApprovalHistoryEventType.TaskApproved]: '审批通过',
    [ApprovalHistoryEventType.TaskRejected]: '审批驳回',
    [ApprovalHistoryEventType.NodeAdvanced]: '流转到下一节点',
    [ApprovalHistoryEventType.InstanceCompleted]: '流程完成',
    [ApprovalHistoryEventType.InstanceRejected]: '流程驳回',
    [ApprovalHistoryEventType.InstanceCanceled]: '流程取消',
    [ApprovalHistoryEventType.InstanceSuspended]: '流程挂起',
    [ApprovalHistoryEventType.InstanceActivated]: '流程激活',
    [ApprovalHistoryEventType.InstanceTerminated]: '流程终止'
  };
  return map[type] ?? '流程操作';
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
