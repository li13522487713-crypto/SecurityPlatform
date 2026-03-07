<template>
  <div class="writeback-monitor-page">
    <a-page-header title="审批回写监控" sub-title="查看回写失败记录并手动重试">
      <template #extra>
        <a-space>
          <a-select
            v-model:value="selectedAppId"
            style="width: 260px"
            :loading="appLoading"
            :options="appOptions"
            allow-clear
            show-search
            placeholder="按应用过滤"
            @change="handleAppScopeChange"
          />
          <a-button :loading="loading" @click="loadData">刷新</a-button>
        </a-space>
      </template>
    </a-page-header>

    <div class="monitor-content">
      <a-card size="small" style="margin-bottom: 16px">
        <a-space>
          <a-select v-model:value="retryStrategy" style="width: 180px">
            <a-select-option value="Immediate">立即重试</a-select-option>
            <a-select-option value="Backoff">指数退避重试</a-select-option>
            <a-select-option value="ManualOnly">仅手动重试</a-select-option>
          </a-select>
          <a-switch v-model:checked="alertEnabled" />
          <span>失败告警开关</span>
        </a-space>
      </a-card>

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
import { getLowCodeAppsPaged } from '@/services/lowcode'
import { getCurrentAppIdFromStorage, setCurrentAppIdToStorage } from '@/utils/app-context'

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
const appLoading = ref(false)
const failureList = ref<WritebackFailureDto[]>([])
const retryingIds = ref(new Set<number>())
const selectedAppId = ref<string | undefined>(getCurrentAppIdFromStorage() ?? undefined)
const appOptions = ref<Array<{ label: string; value: string }>>([])
const retryStrategy = ref<'Immediate' | 'Backoff' | 'ManualOnly'>('Backoff')
const alertEnabled = ref(true)

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
    const res = await requestApi<ApiResponse<WritebackFailureDto[]>>('/approval/writeback-failures?limit=100')
    failureList.value = res.data ?? []
  } catch (e) {
    message.error((e as Error)?.message || '加载失败')
  } finally {
    loading.value = false
  }
}

const loadAppOptions = async () => {
  appLoading.value = true
  try {
    const result = await getLowCodeAppsPaged({ pageIndex: 1, pageSize: 200 })
    appOptions.value = result.items.map((item) => ({
      label: `${item.name} (${item.appKey})`,
      value: item.id
    }))
  } catch (e) {
    message.error((e as Error)?.message || '加载应用列表失败')
  } finally {
    appLoading.value = false
  }
}

const handleAppScopeChange = (value: string | undefined) => {
  setCurrentAppIdToStorage(value)
  void loadData()
}

const handleRetry = async (record: WritebackFailureDto) => {
  retryingIds.value.add(record.id)
  try {
    const query = new URLSearchParams({
      strategy: retryStrategy.value,
      alertEnabled: alertEnabled.value ? 'true' : 'false'
    }).toString()
    await requestApi(`/approval/writeback-failures/${record.id}/retry?${query}`, { method: 'POST' })
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

onMounted(async () => {
  await loadAppOptions()
  await loadData()
})
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
