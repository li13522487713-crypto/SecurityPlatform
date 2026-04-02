<template>
  <div class="flow-exec-monitor">
    <a-page-header :title="t('logicFlow.execution')" @back="$router.back()">
      <template #extra>
        <a-button @click="loadExecutions">{{ t('batchProcess.common.refresh') }}</a-button>
      </template>
    </a-page-header>

    <a-card :bordered="false">
      <a-space style="margin-bottom: 12px" wrap>
        <a-input-number
          v-model:value="flowDefFilter"
          :min="1"
          :placeholder="t('logicFlow.flowDefinitionIdFilter')"
          style="width: 200px"
          allow-clear
          @change="onFilterChange"
        />
        <a-select
          v-model:value="statusFilter"
          allow-clear
          style="width: 180px"
          :placeholder="t('logicFlow.execStatus')"
          @change="onFilterChange"
        >
          <a-select-option :value="FlowExecutionStatus.Pending">{{ t('logicFlow.executionStatus.pending') }}</a-select-option>
          <a-select-option :value="FlowExecutionStatus.Running">{{ t('logicFlow.executionStatus.running') }}</a-select-option>
          <a-select-option :value="FlowExecutionStatus.Completed">{{ t('logicFlow.executionStatus.completed') }}</a-select-option>
          <a-select-option :value="FlowExecutionStatus.Failed">{{ t('logicFlow.executionStatus.failed') }}</a-select-option>
          <a-select-option :value="FlowExecutionStatus.Cancelled">{{ t('logicFlow.executionStatus.cancelled') }}</a-select-option>
          <a-select-option :value="FlowExecutionStatus.TimedOut">{{ t('logicFlow.executionStatus.timedOut') }}</a-select-option>
          <a-select-option :value="FlowExecutionStatus.Paused">{{ t('logicFlow.executionStatus.paused') }}</a-select-option>
          <a-select-option :value="FlowExecutionStatus.Compensating">{{ t('logicFlow.executionStatus.compensating') }}</a-select-option>
          <a-select-option :value="FlowExecutionStatus.Compensated">{{ t('logicFlow.executionStatus.compensated') }}</a-select-option>
        </a-select>
      </a-space>

      <a-table
        :columns="columns"
        :data-source="executions"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="execColor(record.status)">{{ execLabel(record.status) }}</a-tag>
          </template>
          <template v-if="column.key === 'triggerType'">
            {{ triggerLabel(record.triggerType) }}
          </template>
          <template v-if="column.key === 'duration'">
            {{ formatDuration(record.durationMs) }}
          </template>
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" @click="openDetail(record)">{{ t('batchProcess.common.detail') }}</a-button>
              <a-button
                v-if="record.status === FlowExecutionStatus.Running || record.status === FlowExecutionStatus.Pending"
                type="link"
                size="small"
                danger
                @click="onCancel(record.id)"
              >
                {{ t('logicFlow.cancel') }}
              </a-button>
              <a-button
                v-if="record.status === FlowExecutionStatus.Running"
                type="link"
                size="small"
                @click="onPause(record.id)"
              >
                {{ t('logicFlow.pause') }}
              </a-button>
              <a-button
                v-if="record.status === FlowExecutionStatus.Paused"
                type="link"
                size="small"
                @click="onResume(record.id)"
              >
                {{ t('logicFlow.resume') }}
              </a-button>
              <a-button type="link" size="small" @click="onRetry(record.id)">{{ t('logicFlow.retry') }}</a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-drawer v-model:open="drawerOpen" width="520" :title="t('logicFlow.executionDetail')" @close="drawerOpen = false">
      <template v-if="detail">
        <a-descriptions :column="1" size="small" bordered>
          <a-descriptions-item :label="t('logicFlow.executionId')">{{ detail.id }}</a-descriptions-item>
          <a-descriptions-item :label="t('logicFlow.flowDefinitionIdLabel')">{{ detail.flowDefinitionId }}</a-descriptions-item>
          <a-descriptions-item :label="t('logicFlow.flowStatus')">
            <a-tag :color="execColor(detail.status)">{{ execLabel(detail.status) }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item :label="t('logicFlow.triggerType')">{{ triggerLabel(detail.triggerType) }}</a-descriptions-item>
          <a-descriptions-item :label="t('logicFlow.startedAt')">{{ detail.startedAt ?? '—' }}</a-descriptions-item>
          <a-descriptions-item :label="t('logicFlow.completedAt')">{{ detail.completedAt ?? '—' }}</a-descriptions-item>
          <a-descriptions-item :label="t('logicFlow.duration')">{{ formatDuration(detail.durationMs) }}</a-descriptions-item>
        </a-descriptions>
        <a-divider>{{ t('logicFlow.nodeRuns') }}</a-divider>
        <a-spin :spinning="runsLoading">
          <a-timeline v-if="nodeRuns.length > 0">
            <a-timeline-item v-for="r in nodeRuns" :key="r.id" :color="nodeRunColor(r.status)">
              <div><strong>{{ r.nodeKey }}</strong> ({{ r.nodeTypeKey }})</div>
              <div>{{ nodeRunLabel(r.status) }} · {{ formatDuration(r.durationMs) }}</div>
              <div v-if="r.errorMessage" class="err">{{ r.errorMessage }}</div>
            </a-timeline-item>
          </a-timeline>
          <a-empty v-else :description="t('logicFlow.noNodeRuns')" />
        </a-spin>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { message } from 'ant-design-vue'
import {
  FlowExecutionStatus,
  getFlowExecutionsPaged,
  getFlowExecutionById,
  getNodeRuns,
  cancelExecution,
  pauseExecution,
  resumeExecution,
  retryExecution,
  LogicFlowNodeRunStatus,
  type FlowExecutionListItem,
  type FlowExecutionResponse,
  type NodeRunResponse,
} from '@/services/api-logic-flow'

const { t } = useI18n()
const route = useRoute()

const loading = ref(false)
const runsLoading = ref(false)
const executions = ref<FlowExecutionListItem[]>([])
const total = ref(0)
const pageIndex = ref(1)
const pageSize = ref(10)
const flowDefFilter = ref<number | null>(null)
const statusFilter = ref<FlowExecutionStatus | undefined>(undefined)

const drawerOpen = ref(false)
const detail = ref<FlowExecutionResponse | null>(null)
const nodeRuns = ref<NodeRunResponse[]>([])

const columns = computed(() => [
  { title: t('logicFlow.executionId'), dataIndex: 'id', key: 'id', width: 96, ellipsis: true },
  { title: t('logicFlow.flowDefinitionIdLabel'), dataIndex: 'flowDefinitionId', key: 'flowDefinitionId', width: 100 },
  { title: t('logicFlow.flowStatus'), key: 'status', width: 110 },
  { title: t('logicFlow.triggerType'), key: 'triggerType', width: 120 },
  { title: t('logicFlow.duration'), key: 'duration', width: 100 },
  { title: t('logicFlow.startedAt'), dataIndex: 'startedAt', key: 'startedAt', width: 180 },
  { title: t('batchProcess.common.action'), key: 'action', width: 280 },
])

const pagination = computed(() => ({
  current: pageIndex.value,
  pageSize: pageSize.value,
  total: total.value,
  showSizeChanger: true,
}))

function formatDuration(ms: number | null | undefined) {
  if (ms == null) return '—'
  if (ms < 1000) return `${ms} ms`
  return `${(ms / 1000).toFixed(2)} s`
}

function triggerLabel(triggerType: number) {
  const map: Record<number, string> = {
    0: t('logicFlow.triggerManual'),
    1: t('logicFlow.triggerScheduled'),
    2: t('logicFlow.triggerEvent'),
    3: t('logicFlow.triggerApi'),
    4: t('logicFlow.triggerDataChange'),
  }
  return map[triggerType] ?? String(triggerType)
}

function execColor(s: FlowExecutionStatus) {
  const map: Partial<Record<FlowExecutionStatus, string>> = {
    [FlowExecutionStatus.Running]: 'blue',
    [FlowExecutionStatus.Completed]: 'success',
    [FlowExecutionStatus.Failed]: 'error',
    [FlowExecutionStatus.Cancelled]: 'default',
    [FlowExecutionStatus.TimedOut]: 'warning',
    [FlowExecutionStatus.Paused]: 'orange',
    [FlowExecutionStatus.Pending]: 'default',
    [FlowExecutionStatus.Compensating]: 'purple',
    [FlowExecutionStatus.Compensated]: 'cyan',
  }
  return map[s] ?? 'default'
}

function execLabel(s: FlowExecutionStatus) {
  const keys: Record<number, string> = {
    [FlowExecutionStatus.Pending]: 'logicFlow.executionStatus.pending',
    [FlowExecutionStatus.Running]: 'logicFlow.executionStatus.running',
    [FlowExecutionStatus.Completed]: 'logicFlow.executionStatus.completed',
    [FlowExecutionStatus.Failed]: 'logicFlow.executionStatus.failed',
    [FlowExecutionStatus.Cancelled]: 'logicFlow.executionStatus.cancelled',
    [FlowExecutionStatus.TimedOut]: 'logicFlow.executionStatus.timedOut',
    [FlowExecutionStatus.Paused]: 'logicFlow.executionStatus.paused',
    [FlowExecutionStatus.Compensating]: 'logicFlow.executionStatus.compensating',
    [FlowExecutionStatus.Compensated]: 'logicFlow.executionStatus.compensated',
  }
  const k = keys[s]
  return k ? t(k) : String(s)
}

function nodeRunColor(s: LogicFlowNodeRunStatus) {
  const map: Partial<Record<LogicFlowNodeRunStatus, string>> = {
    [LogicFlowNodeRunStatus.Running]: 'blue',
    [LogicFlowNodeRunStatus.Completed]: 'green',
    [LogicFlowNodeRunStatus.Failed]: 'red',
    [LogicFlowNodeRunStatus.TimedOut]: 'orange',
  }
  return map[s] ?? 'gray'
}

function nodeRunLabel(s: LogicFlowNodeRunStatus) {
  const keys: Record<number, string> = {
    [LogicFlowNodeRunStatus.Pending]: 'logicFlow.nodeRunStatus.pending',
    [LogicFlowNodeRunStatus.Running]: 'logicFlow.nodeRunStatus.running',
    [LogicFlowNodeRunStatus.Completed]: 'logicFlow.nodeRunStatus.completed',
    [LogicFlowNodeRunStatus.Failed]: 'logicFlow.nodeRunStatus.failed',
    [LogicFlowNodeRunStatus.Skipped]: 'logicFlow.nodeRunStatus.skipped',
    [LogicFlowNodeRunStatus.TimedOut]: 'logicFlow.nodeRunStatus.timedOut',
    [LogicFlowNodeRunStatus.Compensating]: 'logicFlow.nodeRunStatus.compensating',
    [LogicFlowNodeRunStatus.Compensated]: 'logicFlow.nodeRunStatus.compensated',
    [LogicFlowNodeRunStatus.WaitingForRetry]: 'logicFlow.nodeRunStatus.waitingForRetry',
  }
  const k = keys[s]
  return k ? t(k) : String(s)
}

async function loadExecutions() {
  loading.value = true
  try {
    const res = await getFlowExecutionsPaged({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      flowDefinitionId: flowDefFilter.value ?? undefined,
      status: statusFilter.value,
    })
    if (res?.data) {
      executions.value = res.data.items
      total.value = res.data.total
    }
  } finally {
    loading.value = false
  }
}

function handleTableChange(pag: { current?: number; pageSize?: number }) {
  pageIndex.value = pag.current ?? 1
  pageSize.value = pag.pageSize ?? 10
  loadExecutions()
}

function onFilterChange() {
  pageIndex.value = 1
  loadExecutions()
}

async function openDetail(record: FlowExecutionListItem) {
  drawerOpen.value = true
  detail.value = null
  nodeRuns.value = []
  runsLoading.value = true
  try {
    const [execRes, runsRes] = await Promise.all([
      getFlowExecutionById(record.id),
      getNodeRuns(record.id),
    ])
    if (execRes?.data) detail.value = execRes.data
    if (runsRes?.data) nodeRuns.value = runsRes.data
  } finally {
    runsLoading.value = false
  }
}

async function onCancel(id: string) {
  await cancelExecution(id)
  message.success(t('batchProcess.msg.cancelled'))
  loadExecutions()
}

async function onPause(id: string) {
  await pauseExecution(id)
  message.success(t('logicFlow.pauseOk'))
  loadExecutions()
}

async function onResume(id: string) {
  await resumeExecution(id)
  message.success(t('logicFlow.resumeOk'))
  loadExecutions()
}

async function onRetry(id: string) {
  const res = await retryExecution(id)
  if (res?.data?.executionId) {
    message.success(`${t('logicFlow.retry')}: ${res.data.executionId}`)
  }
  loadExecutions()
}

onMounted(() => {
  const q = route.query.flowDefinitionId
  if (typeof q === 'string' && q) {
    const n = Number(q)
    if (!Number.isNaN(n)) flowDefFilter.value = n
  }
  loadExecutions()
})
</script>

<style scoped>
.err {
  color: var(--ant-color-error, #cf1322);
  font-size: 12px;
}
</style>
