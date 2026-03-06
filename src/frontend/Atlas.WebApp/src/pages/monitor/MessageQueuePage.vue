<template>
  <div class="mq-page">
    <a-page-header title="消息队列监控" subtitle="队列统计、消息查看与死信管理" />

    <!-- 全局统计 -->
    <a-row :gutter="16" class="stats-row">
      <a-col :span="6" v-for="item in globalStatItems" :key="item.key">
        <a-statistic :title="statLabel(item.key)" :value="item.value" />
      </a-col>
    </a-row>

    <!-- 队列列表 -->
    <a-card title="队列列表" :bordered="false" class="queue-card">
      <template #extra>
        <a-button :loading="loading" @click="fetchAll">
          <ReloadOutlined />刷新
        </a-button>
      </template>
      <a-table
        :columns="queueColumns"
        :data-source="queues"
        :loading="loading"
        row-key="queueName"
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'actions'">
            <a-space>
              <a-button size="small" type="link" @click="openMessages(record.queueName)">查看消息</a-button>
              <a-popconfirm title="重试所有死信?" @confirm="retryDeadLetters(record.queueName)">
                <a-button size="small" type="link" :disabled="record.deadLettered === 0">重试死信</a-button>
              </a-popconfirm>
              <a-popconfirm title="清理所有死信?" @confirm="deleteDeadLetters(record.queueName)">
                <a-button size="small" type="link" danger :disabled="record.deadLettered === 0">清理死信</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 消息列表 Drawer -->
    <a-drawer
      v-if="selectedQueue"
      :title="`消息列表 — ${selectedQueue}`"
      width="760"
      :open="true"
      @close="selectedQueue = null"
    >
      <a-space class="filter-bar">
        <a-select v-model:value="messageStatusFilter" allow-clear placeholder="状态" style="width: 140px" @change="fetchMessages">
          <a-select-option value="0">Pending</a-select-option>
          <a-select-option value="1">Processing</a-select-option>
          <a-select-option value="2">Completed</a-select-option>
          <a-select-option value="3">Failed</a-select-option>
          <a-select-option value="4">DeadLettered</a-select-option>
        </a-select>
      </a-space>
      <a-table
        :columns="msgColumns"
        :data-source="messages"
        :loading="msgLoading"
        row-key="id"
        :pagination="{ total: msgTotal, current: msgPage, pageSize: 20, onChange: (p: number) => { msgPage = p; fetchMessages() } }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="statusColor(record.status)">{{ record.status }}</a-tag>
          </template>
        </template>
      </a-table>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import { requestApi } from '@/services/api-core'
import type { ApiResponse } from '@/types/api'

const loading = ref(false)
const queues = ref<QueueStat[]>([])
const globalStats = ref<QueueStat | null>(null)
const selectedQueue = ref<string | null>(null)
const messages = ref<QueueMsg[]>([])
const msgLoading = ref(false)
const msgTotal = ref(0)
const msgPage = ref(1)
const messageStatusFilter = ref<string | undefined>()

interface QueueStat {
  queueName: string
  pending: number
  processing: number
  completed: number
  failed: number
  deadLettered: number
}

interface QueueMsg {
  id: number
  queueName: string
  messageType: string
  status: string
  retryCount: number
  errorMessage?: string
  enqueuedAt: string
  completedAt?: string
}

interface QueueMessagePage {
  pageIndex: number
  pageSize: number
  total: number
  items: QueueMsg[]
}

const queueColumns = [
  { title: '队列名', dataIndex: 'queueName', key: 'queueName' },
  { title: '待处理', dataIndex: 'pending', key: 'pending' },
  { title: '处理中', dataIndex: 'processing', key: 'processing' },
  { title: '已完成', dataIndex: 'completed', key: 'completed' },
  { title: '失败', dataIndex: 'failed', key: 'failed' },
  { title: '死信', dataIndex: 'deadLettered', key: 'deadLettered' },
  { title: '操作', key: 'actions', width: 220 },
]

const msgColumns = [
  { title: 'ID', dataIndex: 'id', key: 'id', width: 80 },
  { title: '消息类型', dataIndex: 'messageType', key: 'messageType' },
  { title: '状态', key: 'status' },
  { title: '重试次数', dataIndex: 'retryCount', key: 'retryCount', width: 80 },
  { title: '错误', dataIndex: 'errorMessage', key: 'errorMessage', ellipsis: true },
  { title: '入队时间', dataIndex: 'enqueuedAt', key: 'enqueuedAt', customRender: ({ value }: { value: string }) => new Date(value).toLocaleString('zh-CN') },
]

const globalStatItems = computed(() => {
  const stats = globalStats.value
  if (!stats) {
    return []
  }

  return [
    { key: 'pending', value: stats.pending },
    { key: 'processing', value: stats.processing },
    { key: 'completed', value: stats.completed },
    { key: 'failed', value: stats.failed },
    { key: 'deadLettered', value: stats.deadLettered },
  ]
})

async function fetchAll() {
  loading.value = true
  try {
    const [queuesRes, statsRes] = await Promise.all([
      requestApi<ApiResponse<QueueStat[]>>('/admin/message-queue/queues'),
      requestApi<ApiResponse<QueueStat>>('/admin/message-queue/stats'),
    ])
    if (queuesRes.success) queues.value = queuesRes.data ?? []
    if (statsRes.success) globalStats.value = statsRes.data ?? null
  } catch (error) {
    queues.value = []
    globalStats.value = null
    const status = (error as { status?: number })?.status
    if (status !== 401) {
      message.error('加载消息队列监控数据失败')
    }
  } finally {
    loading.value = false
  }
}

async function fetchMessages() {
  if (!selectedQueue.value) return
  msgLoading.value = true
  try {
    const params = new URLSearchParams({ pageIndex: String(msgPage.value), pageSize: '20' })
    if (messageStatusFilter.value) params.set('status', messageStatusFilter.value)
    const res = await requestApi<ApiResponse<QueueMessagePage>>(
      `/admin/message-queue/queues/${encodeURIComponent(selectedQueue.value)}/messages?${params.toString()}`
    )
    if (res.success && res.data) {
      messages.value = res.data.items
      msgTotal.value = res.data.total
    }
  } finally {
    msgLoading.value = false
  }
}

function openMessages(queueName: string) {
  selectedQueue.value = queueName
  msgPage.value = 1
  messageStatusFilter.value = undefined
  fetchMessages()
}

async function retryDeadLetters(queueName: string) {
  await requestApi<ApiResponse<unknown>>(
    `/admin/message-queue/queues/${encodeURIComponent(queueName)}/dead-letters/retry`,
    { method: 'POST' }
  )
  message.success('重试已触发')
  fetchAll()
}

async function deleteDeadLetters(queueName: string) {
  await requestApi<ApiResponse<unknown>>(
    `/admin/message-queue/queues/${encodeURIComponent(queueName)}/dead-letters`,
    { method: 'DELETE' }
  )
  message.success('死信已清理')
  fetchAll()
}

function statusColor(status: string) {
  const map: Record<string, string> = {
    Pending: 'blue',
    Processing: 'orange',
    Completed: 'green',
    Failed: 'red',
    DeadLettered: 'purple',
  }
  return map[status] ?? 'default'
}

function statLabel(key: string) {
  const map: Record<string, string> = {
    queueName: '队列',
    pending: '待处理',
    processing: '处理中',
    completed: '已完成',
    failed: '失败',
    deadLettered: '死信',
  }
  return map[key] ?? key
}

onMounted(fetchAll)
</script>

<style scoped>
.mq-page {
  padding: 0 24px 24px;
}
.stats-row {
  margin-bottom: 16px;
  background: #fff;
  padding: 16px;
  border-radius: 8px;
}
.queue-card {
  margin-bottom: 16px;
}
.filter-bar {
  margin-bottom: 12px;
}
</style>
