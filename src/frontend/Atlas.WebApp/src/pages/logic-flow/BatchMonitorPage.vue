<template>
  <div class="batch-monitor">
    <a-page-header :title="t('batchProcess.monitor.title')" :sub-title="t('batchProcess.monitor.subtitle')" @back="$router.back()">
      <template #extra>
        <a-button @click="loadExecutions">{{ t('batchProcess.common.refresh') }}</a-button>
      </template>
    </a-page-header>

    <a-card :bordered="false" style="margin-bottom: 16px">
      <a-table
        :columns="execColumns"
        :data-source="executions"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="execStatusColor(record.status)">{{ execStatusLabel(record.status) }}</a-tag>
          </template>
          <template v-if="column.key === 'progress'">
            <a-progress :percent="calcPercent(record)" :size="'small'" />
          </template>
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button size="small" type="link" @click="loadShards(record.id)">{{ t('batchProcess.monitor.viewShards') }}</a-button>
              <a-button v-if="record.status <= 1" size="small" type="link" danger @click="handleCancel(record.id)">{{ t('batchProcess.action.cancel') }}</a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-card v-if="selectedShards.length > 0" :title="t('batchProcess.monitor.shardDetail')" :bordered="false">
      <a-table :columns="shardColumns" :data-source="selectedShards" :pagination="false" row-key="id" size="small">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="shardStatusColor(record.status)">{{ shardStatusLabel(record.status) }}</a-tag>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { message } from 'ant-design-vue'
import {
  getBatchJobExecutionsPaged,
  getBatchJobExecutionShards,
  cancelBatchJobExecution,
  type BatchJobExecutionListItem,
  type ShardExecutionResponse,
} from '@/services/api-batch-process'

const { t } = useI18n()
const route = useRoute()
const loading = ref(false)
const executions = ref<BatchJobExecutionListItem[]>([])
const selectedShards = ref<ShardExecutionResponse[]>([])
const total = ref(0)
const pageIndex = ref(1)
const pageSize = ref(10)

const jobId = computed(() => (route.query.jobId as string) ?? '')

const execColumns = computed(() => [
  { title: 'ID', dataIndex: 'id', key: 'id', width: 80 },
  { title: t('batchProcess.common.status'), key: 'status', width: 100 },
  { title: t('batchProcess.monitor.progress'), key: 'progress', width: 180 },
  { title: t('batchProcess.monitor.shards'), dataIndex: 'totalShards', key: 'totalShards', width: 80 },
  { title: t('batchProcess.monitor.completedShards'), dataIndex: 'completedShards', key: 'completedShards', width: 80 },
  { title: t('batchProcess.monitor.failedShards'), dataIndex: 'failedShards', key: 'failedShards', width: 80 },
  { title: t('batchProcess.monitor.triggeredBy'), dataIndex: 'triggeredBy', key: 'triggeredBy', width: 120 },
  { title: t('batchProcess.common.createdAt'), dataIndex: 'createdAt', key: 'createdAt', width: 180 },
  { title: t('batchProcess.common.action'), key: 'action', width: 160 },
])

const shardColumns = computed(() => [
  { title: '#', dataIndex: 'shardIndex', key: 'shardIndex', width: 50 },
  { title: t('batchProcess.monitor.shardKey'), dataIndex: 'shardKey', key: 'shardKey' },
  { title: t('batchProcess.common.status'), key: 'status', width: 100 },
  { title: t('batchProcess.monitor.processedRecords'), dataIndex: 'processedRecords', key: 'processedRecords', width: 120 },
  { title: t('batchProcess.monitor.failedRecords'), dataIndex: 'failedRecords', key: 'failedRecords', width: 100 },
  { title: t('batchProcess.monitor.retryCount'), dataIndex: 'retryCount', key: 'retryCount', width: 80 },
  { title: t('batchProcess.monitor.error'), dataIndex: 'errorMessage', key: 'errorMessage', ellipsis: true },
])

const pagination = computed(() => ({
  current: pageIndex.value,
  pageSize: pageSize.value,
  total: total.value,
  showSizeChanger: true,
}))

function calcPercent(record: BatchJobExecutionListItem) {
  if (record.totalShards === 0) return 0
  return Math.round((record.completedShards / record.totalShards) * 100)
}
function execStatusColor(s: number) {
  return ['default', 'processing', 'success', 'error', 'default'][s] ?? 'default'
}
function execStatusLabel(s: number) {
  return [t('batchProcess.execStatus.pending'), t('batchProcess.execStatus.running'), t('batchProcess.execStatus.completed'), t('batchProcess.execStatus.failed'), t('batchProcess.execStatus.cancelled')][s] ?? ''
}
function shardStatusColor(s: number) {
  return ['default', 'processing', 'success', 'error', 'warning'][s] ?? 'default'
}
function shardStatusLabel(s: number) {
  return [t('batchProcess.execStatus.pending'), t('batchProcess.execStatus.running'), t('batchProcess.execStatus.completed'), t('batchProcess.execStatus.failed'), t('batchProcess.shardStatus.retrying')][s] ?? ''
}

async function loadExecutions() {
  if (!jobId.value) return
  loading.value = true
  try {
    const res = await getBatchJobExecutionsPaged(jobId.value, { pageIndex: pageIndex.value, pageSize: pageSize.value })
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

async function loadShards(executionId: string) {
  const res = await getBatchJobExecutionShards(executionId)
  if (res?.data) {
    selectedShards.value = res.data
  }
}

async function handleCancel(executionId: string) {
  await cancelBatchJobExecution(executionId)
  message.success(t('batchProcess.msg.cancelled'))
  loadExecutions()
}

onMounted(loadExecutions)
</script>
