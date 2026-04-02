<template>
  <div class="batch-dead-letters">
    <a-page-header :title="t('batchProcess.deadLetter.title')" :sub-title="t('batchProcess.deadLetter.subtitle')">
      <template #extra>
        <a-space>
          <a-button :disabled="selectedRowKeys.length === 0" @click="handleBatchRetry">{{ t('batchProcess.deadLetter.batchRetry') }}</a-button>
          <a-button :disabled="selectedRowKeys.length === 0" danger @click="handleBatchAbandon">{{ t('batchProcess.deadLetter.batchAbandon') }}</a-button>
        </a-space>
      </template>
    </a-page-header>

    <a-card :bordered="false">
      <div style="margin-bottom: 16px; display: flex; gap: 12px;">
        <a-select
          v-model:value="statusFilter"
          :placeholder="t('batchProcess.common.allStatuses')"
          style="width: 160px"
          allow-clear
          @change="loadData"
        >
          <a-select-option :value="0">{{ t('batchProcess.dlStatus.pending') }}</a-select-option>
          <a-select-option :value="1">{{ t('batchProcess.dlStatus.retrying') }}</a-select-option>
          <a-select-option :value="2">{{ t('batchProcess.dlStatus.resolved') }}</a-select-option>
          <a-select-option :value="3">{{ t('batchProcess.dlStatus.abandoned') }}</a-select-option>
        </a-select>
      </div>

      <a-table
        :columns="columns"
        :data-source="records"
        :loading="loading"
        :pagination="pagination"
        :row-selection="{ selectedRowKeys, onChange: onSelectChange }"
        row-key="id"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="dlStatusColor(record.status)">{{ dlStatusLabel(record.status) }}</a-tag>
          </template>
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button v-if="record.status === 0" size="small" type="link" @click="handleRetry(record.id)">{{ t('batchProcess.deadLetter.retry') }}</a-button>
              <a-button v-if="record.status <= 1" size="small" type="link" danger @click="handleAbandon(record.id)">{{ t('batchProcess.deadLetter.abandon') }}</a-button>
              <a-button size="small" type="link" @click="showDetail(record)">{{ t('batchProcess.common.detail') }}</a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-drawer v-model:open="detailVisible" :title="t('batchProcess.deadLetter.detailTitle')" :width="560">
      <template v-if="detailRecord">
        <a-descriptions :column="1" bordered size="small">
          <a-descriptions-item :label="'ID'">{{ detailRecord.id }}</a-descriptions-item>
          <a-descriptions-item :label="t('batchProcess.deadLetter.recordKey')">{{ detailRecord.recordKey }}</a-descriptions-item>
          <a-descriptions-item :label="t('batchProcess.deadLetter.errorType')">{{ detailRecord.errorType }}</a-descriptions-item>
          <a-descriptions-item :label="t('batchProcess.deadLetter.errorMessage')">{{ detailRecord.errorMessage }}</a-descriptions-item>
          <a-descriptions-item :label="t('batchProcess.deadLetter.retryCount')">{{ detailRecord.retryCount }} / {{ detailRecord.maxRetries }}</a-descriptions-item>
          <a-descriptions-item :label="t('batchProcess.common.status')">
            <a-tag :color="dlStatusColor(detailRecord.status)">{{ dlStatusLabel(detailRecord.status) }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item :label="t('batchProcess.common.createdAt')">{{ detailRecord.createdAt }}</a-descriptions-item>
        </a-descriptions>
        <div v-if="detailRecord.recordPayload" style="margin-top: 16px;">
          <h4>{{ t('batchProcess.deadLetter.payload') }}</h4>
          <a-textarea :value="detailRecord.recordPayload" :rows="6" readonly />
        </div>
        <div v-if="detailRecord.errorStackTrace" style="margin-top: 16px;">
          <h4>{{ t('batchProcess.deadLetter.stackTrace') }}</h4>
          <a-textarea :value="detailRecord.errorStackTrace" :rows="8" readonly />
        </div>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { message } from 'ant-design-vue'
import {
  getDeadLettersPaged,
  getDeadLetterById,
  retryDeadLetter,
  retryDeadLettersBatch,
  abandonDeadLetter,
  abandonDeadLettersBatch,
  type BatchDeadLetterListItem,
  type BatchDeadLetterResponse,
} from '@/services/api-batch-process'

const { t } = useI18n()
const loading = ref(false)
const records = ref<BatchDeadLetterListItem[]>([])
const statusFilter = ref<number | undefined>(undefined)
const selectedRowKeys = ref<string[]>([])
const total = ref(0)
const pageIndex = ref(1)
const pageSize = ref(10)
const detailVisible = ref(false)
const detailRecord = ref<BatchDeadLetterResponse | null>(null)

const columns = computed(() => [
  { title: t('batchProcess.deadLetter.recordKey'), dataIndex: 'recordKey', key: 'recordKey', ellipsis: true },
  { title: t('batchProcess.deadLetter.errorType'), dataIndex: 'errorType', key: 'errorType', width: 140 },
  { title: t('batchProcess.deadLetter.errorMessage'), dataIndex: 'errorMessage', key: 'errorMessage', ellipsis: true },
  { title: t('batchProcess.deadLetter.retryCount'), dataIndex: 'retryCount', key: 'retryCount', width: 80 },
  { title: t('batchProcess.common.status'), key: 'status', width: 100 },
  { title: t('batchProcess.common.createdAt'), dataIndex: 'createdAt', key: 'createdAt', width: 180 },
  { title: t('batchProcess.common.action'), key: 'action', width: 200 },
])

const pagination = computed(() => ({
  current: pageIndex.value,
  pageSize: pageSize.value,
  total: total.value,
  showSizeChanger: true,
}))

function dlStatusColor(s: number) {
  return ['warning', 'processing', 'success', 'default'][s] ?? 'default'
}
function dlStatusLabel(s: number) {
  return [t('batchProcess.dlStatus.pending'), t('batchProcess.dlStatus.retrying'), t('batchProcess.dlStatus.resolved'), t('batchProcess.dlStatus.abandoned')][s] ?? ''
}

function onSelectChange(keys: string[]) {
  selectedRowKeys.value = keys
}

async function loadData() {
  loading.value = true
  try {
    const res = await getDeadLettersPaged({ pageIndex: pageIndex.value, pageSize: pageSize.value, status: statusFilter.value as number | undefined })
    if (res?.data) {
      records.value = res.data.items
      total.value = res.data.total
    }
  } finally {
    loading.value = false
  }
}

function handleTableChange(pag: { current?: number; pageSize?: number }) {
  pageIndex.value = pag.current ?? 1
  pageSize.value = pag.pageSize ?? 10
  loadData()
}

async function handleRetry(id: string) {
  await retryDeadLetter(id)
  message.success(t('batchProcess.msg.retrySuccess'))
  loadData()
}

async function handleAbandon(id: string) {
  await abandonDeadLetter(id)
  message.success(t('batchProcess.msg.abandonSuccess'))
  loadData()
}

async function handleBatchRetry() {
  await retryDeadLettersBatch(selectedRowKeys.value)
  selectedRowKeys.value = []
  message.success(t('batchProcess.msg.retrySuccess'))
  loadData()
}

async function handleBatchAbandon() {
  await abandonDeadLettersBatch(selectedRowKeys.value)
  selectedRowKeys.value = []
  message.success(t('batchProcess.msg.abandonSuccess'))
  loadData()
}

async function showDetail(record: BatchDeadLetterListItem) {
  const res = await getDeadLetterById(record.id)
  if (res?.data) {
    detailRecord.value = res.data
    detailVisible.value = true
  }
}

onMounted(loadData)
</script>
