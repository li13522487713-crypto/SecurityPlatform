<template>
  <div class="batch-job-designer">
    <a-page-header :title="t('batchProcess.designer.title')" :sub-title="t('batchProcess.designer.subtitle')">
      <template #extra>
        <a-button type="primary" @click="showCreateModal = true">{{ t('batchProcess.common.create') }}</a-button>
      </template>
    </a-page-header>

    <a-card :bordered="false">
      <div style="margin-bottom: 16px; display: flex; gap: 12px; align-items: center;">
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('batchProcess.designer.searchPlaceholder')"
          style="width: 280px"
          allow-clear
          @search="loadJobs"
        />
        <a-select
          v-model:value="statusFilter"
          :placeholder="t('batchProcess.common.allStatuses')"
          style="width: 160px"
          allow-clear
          @change="loadJobs"
        >
          <a-select-option :value="0">{{ t('batchProcess.status.draft') }}</a-select-option>
          <a-select-option :value="1">{{ t('batchProcess.status.active') }}</a-select-option>
          <a-select-option :value="2">{{ t('batchProcess.status.paused') }}</a-select-option>
          <a-select-option :value="3">{{ t('batchProcess.status.archived') }}</a-select-option>
        </a-select>
      </div>

      <a-table
        :columns="columns"
        :data-source="jobs"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="statusColor(record.status)">{{ statusLabel(record.status) }}</a-tag>
          </template>
          <template v-if="column.key === 'shardStrategyType'">
            {{ record.shardStrategyType === 0 ? t('batchProcess.shard.pkRange') : t('batchProcess.shard.timeWindow') }}
          </template>
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button v-if="record.status === 0" size="small" type="link" @click="handleActivate(record)">{{ t('batchProcess.action.activate') }}</a-button>
              <a-button v-if="record.status === 1" size="small" type="link" @click="handleTrigger(record)">{{ t('batchProcess.action.trigger') }}</a-button>
              <a-button v-if="record.status === 1" size="small" type="link" @click="handlePause(record)">{{ t('batchProcess.action.pause') }}</a-button>
              <a-button size="small" type="link" @click="navigateToMonitor(record)">{{ t('batchProcess.action.monitor') }}</a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal v-model:open="showCreateModal" :title="t('batchProcess.designer.createTitle')" :width="640" @ok="handleCreate">
      <a-form layout="vertical">
        <a-form-item :label="t('batchProcess.field.name')">
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item :label="t('batchProcess.field.description')">
          <a-textarea v-model:value="form.description" :rows="2" />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item :label="t('batchProcess.field.dataSourceType')">
              <a-input v-model:value="form.dataSourceType" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="t('batchProcess.field.shardStrategy')">
              <a-select v-model:value="form.shardStrategyType">
                <a-select-option :value="0">{{ t('batchProcess.shard.pkRange') }}</a-select-option>
                <a-select-option :value="1">{{ t('batchProcess.shard.timeWindow') }}</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="8">
            <a-form-item :label="t('batchProcess.field.batchSize')">
              <a-input-number v-model:value="form.batchSize" :min="1" :max="10000" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item :label="t('batchProcess.field.maxConcurrency')">
              <a-input-number v-model:value="form.maxConcurrency" :min="1" :max="64" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item :label="t('batchProcess.field.timeoutSeconds')">
              <a-input-number v-model:value="form.timeoutSeconds" :min="1" :max="86400" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item :label="t('batchProcess.field.dataSourceConfig')">
          <a-textarea v-model:value="form.dataSourceConfig" :rows="3" />
        </a-form-item>
        <a-form-item :label="t('batchProcess.field.shardConfig')">
          <a-textarea v-model:value="form.shardConfig" :rows="2" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { message } from 'ant-design-vue'
import {
  getBatchJobsPaged,
  createBatchJob,
  activateBatchJob,
  pauseBatchJob,
  triggerBatchJob,
  type BatchJobDefinitionListItem,
} from '@/services/api-batch-process'

const { t } = useI18n()
const router = useRouter()
const loading = ref(false)
const jobs = ref<BatchJobDefinitionListItem[]>([])
const keyword = ref('')
const statusFilter = ref<number | undefined>(undefined)
const showCreateModal = ref(false)
const total = ref(0)
const pageIndex = ref(1)
const pageSize = ref(10)

const form = reactive({
  name: '',
  description: '',
  dataSourceType: 'DynamicTable',
  dataSourceConfig: '{}',
  shardStrategyType: 0,
  shardConfig: '{}',
  batchSize: 100,
  maxConcurrency: 4,
  retryPolicy: '{"maxRetries":3,"retryDelayMs":1000}',
  timeoutSeconds: 3600,
})

const columns = computed(() => [
  { title: t('batchProcess.field.name'), dataIndex: 'name', key: 'name' },
  { title: t('batchProcess.field.shardStrategy'), dataIndex: 'shardStrategyType', key: 'shardStrategyType', width: 120 },
  { title: t('batchProcess.field.batchSize'), dataIndex: 'batchSize', key: 'batchSize', width: 100 },
  { title: t('batchProcess.field.maxConcurrency'), dataIndex: 'maxConcurrency', key: 'maxConcurrency', width: 100 },
  { title: t('batchProcess.common.status'), dataIndex: 'status', key: 'status', width: 100 },
  { title: t('batchProcess.common.createdAt'), dataIndex: 'createdAt', key: 'createdAt', width: 180 },
  { title: t('batchProcess.common.action'), key: 'action', width: 200 },
])

const pagination = computed(() => ({
  current: pageIndex.value,
  pageSize: pageSize.value,
  total: total.value,
  showSizeChanger: true,
  showTotal: (t: number) => `${t}`,
}))

function statusColor(s: number) {
  return ['default', 'processing', 'warning', 'default'][s] ?? 'default'
}
function statusLabel(s: number) {
  return [t('batchProcess.status.draft'), t('batchProcess.status.active'), t('batchProcess.status.paused'), t('batchProcess.status.archived')][s] ?? ''
}

async function loadJobs() {
  loading.value = true
  try {
    const res = await getBatchJobsPaged({ pageIndex: pageIndex.value, pageSize: pageSize.value, keyword: keyword.value, status: statusFilter.value as number | undefined })
    if (res?.data) {
      jobs.value = res.data.items
      total.value = res.data.total
    }
  } finally {
    loading.value = false
  }
}

function handleTableChange(pag: { current?: number; pageSize?: number }) {
  pageIndex.value = pag.current ?? 1
  pageSize.value = pag.pageSize ?? 10
  loadJobs()
}

async function handleCreate() {
  try {
    await createBatchJob({
      name: form.name,
      description: form.description || undefined,
      dataSourceType: form.dataSourceType,
      dataSourceConfig: form.dataSourceConfig,
      shardStrategyType: form.shardStrategyType,
      shardConfig: form.shardConfig,
      batchSize: form.batchSize,
      maxConcurrency: form.maxConcurrency,
      retryPolicy: form.retryPolicy,
      timeoutSeconds: form.timeoutSeconds,
    })
    showCreateModal.value = false
    message.success(t('batchProcess.msg.createSuccess'))
    loadJobs()
  } catch {
    message.error(t('batchProcess.msg.createFailed'))
  }
}

async function handleActivate(record: BatchJobDefinitionListItem) {
  await activateBatchJob(record.id)
  message.success(t('batchProcess.msg.activated'))
  loadJobs()
}

async function handlePause(record: BatchJobDefinitionListItem) {
  await pauseBatchJob(record.id)
  message.success(t('batchProcess.msg.paused'))
  loadJobs()
}

async function handleTrigger(record: BatchJobDefinitionListItem) {
  await triggerBatchJob(record.id)
  message.success(t('batchProcess.msg.triggered'))
}

function navigateToMonitor(record: BatchJobDefinitionListItem) {
  router.push({ name: 'app-batch-monitor', params: { ...router.currentRoute.value.params }, query: { jobId: record.id } })
}

onMounted(loadJobs)
</script>
