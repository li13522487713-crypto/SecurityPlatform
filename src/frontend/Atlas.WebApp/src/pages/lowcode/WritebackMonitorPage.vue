<template>
  <div class="writeback-monitor-page">
    <a-page-header title="审批回写监控" sub-title="查看回写失败记录并手动重试">
      <template #extra>
        <a-button :loading="loading" @click="loadData">刷新</a-button>
      </template>
    </a-page-header>

    <div class="monitor-content">
      <!-- 统计卡片 -->
      <a-row :gutter="16" style="margin-bottom: 16px">
        <a-col :span="8">
          <a-card>
            <a-statistic
              title="未解决失败数"
              :value="failureList.length"
              :value-style="{ color: failureList.length > 0 ? '#ff4d4f' : '#52c41a' }"
            />
          </a-card>
        </a-col>
      </a-row>

      <!-- 失败列表 -->
      <a-table
        :data-source="failureList"
        :columns="columns"
        :loading="loading"
        row-key="id"
        :pagination="{ pageSize: 20 }"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'targetStatus'">
            <a-tag color="orange">{{ record.targetStatus }}</a-tag>
          </template>
          <template v-if="column.key === 'retryCount'">
            <a-badge :count="record.retryCount" :overflow-count="99" color="red" />
          </template>
          <template v-if="column.key === 'firstFailedAt'">
            {{ formatDate(record.firstFailedAt) }}
          </template>
          <template v-if="column.key === 'lastAttemptAt'">
            {{ formatDate(record.lastAttemptAt) }}
          </template>
          <template v-if="column.key === 'errorMessage'">
            <a-tooltip :title="record.errorMessage">
              <span class="error-msg-ellipsis">{{ record.errorMessage }}</span>
            </a-tooltip>
          </template>
          <template v-if="column.key === 'actions'">
            <a-button
              type="link"
              size="small"
              :loading="retryingIds.has(record.id)"
              @click="handleRetry(record)"
            >手动重试</a-button>
          </template>
        </template>
      </a-table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { message } from 'ant-design-vue'
import { requestApi } from '@/services/api-core'
import type { ApiResponse } from '@/types/api'

interface WritebackFailureDto {
  id: number
  businessKey: string
  targetStatus: string
  retryCount: number
  errorMessage: string
  firstFailedAt: string
  lastAttemptAt: string
  isResolved: boolean
}

const loading = ref(false)
const failureList = ref<WritebackFailureDto[]>([])
const retryingIds = ref(new Set<number>())

const columns = [
  { title: 'BusinessKey', dataIndex: 'businessKey', key: 'businessKey', width: 200 },
  { title: '目标状态', dataIndex: 'targetStatus', key: 'targetStatus', width: 100 },
  { title: '重试次数', dataIndex: 'retryCount', key: 'retryCount', width: 80, align: 'center' },
  { title: '错误信息', dataIndex: 'errorMessage', key: 'errorMessage', ellipsis: true },
  { title: '首次失败', dataIndex: 'firstFailedAt', key: 'firstFailedAt', width: 150 },
  { title: '最后尝试', dataIndex: 'lastAttemptAt', key: 'lastAttemptAt', width: 150 },
  { title: '操作', key: 'actions', width: 90 },
]

const loadData = async () => {
  loading.value = true
  try {
    const res = await requestApi<ApiResponse<WritebackFailureDto[]>>('/api/v1/approval/writeback-failures?limit=100')
    failureList.value = res.data ?? []
  } catch (e) {
    message.error((e as Error)?.message || '加载失败')
  } finally {
    loading.value = false
  }
}

const handleRetry = async (record: WritebackFailureDto) => {
  retryingIds.value.add(record.id)
  try {
    await requestApi(`/api/v1/approval/writeback-failures/${record.id}/retry`, { method: 'POST' })
    message.success('重试成功')
    await loadData()
  } catch (e) {
    message.error((e as Error)?.message || '重试失败')
  } finally {
    retryingIds.value.delete(record.id)
  }
}

const formatDate = (iso: string) =>
  iso ? new Date(iso).toLocaleString('zh-CN') : '-'

onMounted(loadData)
</script>

<style scoped>
.writeback-monitor-page {
  padding: 16px;
}

.monitor-content {
  padding: 0 16px 16px;
}

.error-msg-ellipsis {
  max-width: 250px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  display: inline-block;
  vertical-align: bottom;
}
</style>
