<template>
  <div class="workflow-list-page">
    <a-page-header title="工作流引擎" subtitle="创建和管理 DAG 工作流">
      <template #extra>
        <a-button type="primary" @click="showCreateModal = true">
          <template #icon><PlusOutlined /></template>
          新建工作流
        </a-button>
      </template>
    </a-page-header>

    <div class="page-content">
      <a-card :bordered="false">
        <template #extra>
          <a-space>
            <a-input-search
              v-model:value="keyword"
              placeholder="搜索工作流名称"
              style="width: 280px"
              allow-clear
              @search="handleSearch"
            />
          </a-space>
        </template>

        <a-tabs v-model:active-key="activeTab" @change="handleTabChange">
          <a-tab-pane key="all" tab="全部工作流" />
          <a-tab-pane key="published" tab="已发布工作流" />
        </a-tabs>

        <a-table
          :data-source="workflows"
          :columns="columns"
          :loading="loading"
          :pagination="pagination"
          row-key="id"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'name'">
              <a @click="openEditor(record.id)">{{ record.name }}</a>
            </template>
            <template v-if="column.key === 'status'">
              <a-tag :color="statusColor(record.status)">{{ statusLabel(record.status) }}</a-tag>
            </template>
            <template v-if="column.key === 'mode'">
              <a-tag>{{ record.mode === 0 ? '标准' : 'ChatFlow' }}</a-tag>
            </template>
            <template v-if="column.key === 'actions'">
              <a-space>
                <a @click="openEditor(record.id)">编辑</a>
                <a-divider type="vertical" />
                <a @click="handleCopy(record.id)">复制</a>
                <a-divider type="vertical" />
                <a-popconfirm title="确认删除此工作流？" @confirm="handleDelete(record.id)">
                  <a class="danger-link">删除</a>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </a-card>

      <a-card class="mt12" title="执行恢复与调试视图">
        <a-space wrap>
          <a-input-number
            v-model:value="executionIdInput"
            :min="1"
            :precision="0"
            style="width: 220px"
            placeholder="输入执行ID"
          />
          <a-button :loading="inspecting" @click="inspectExecution">获取检查点</a-button>
          <a-button :loading="inspecting" @click="inspectDebugView">获取调试视图</a-button>
          <a-button type="primary" :loading="recovering" @click="recoverFromCheckpoint">执行恢复</a-button>
        </a-space>
        <a-alert v-if="checkpointError" class="mt12" type="warning" :message="checkpointError" show-icon />
        <a-descriptions v-if="checkpoint" class="mt12" bordered :column="2" size="small">
          <a-descriptions-item label="执行ID">{{ checkpoint.executionId }}</a-descriptions-item>
          <a-descriptions-item label="工作流ID">{{ checkpoint.workflowId }}</a-descriptions-item>
          <a-descriptions-item label="状态">{{ checkpoint.status }}</a-descriptions-item>
          <a-descriptions-item label="最近节点">{{ checkpoint.lastNodeKey || "-" }}</a-descriptions-item>
          <a-descriptions-item label="开始时间">{{ checkpoint.startedAt }}</a-descriptions-item>
          <a-descriptions-item label="完成时间">{{ checkpoint.completedAt || "-" }}</a-descriptions-item>
        </a-descriptions>
        <a-typography-paragraph v-if="debugView" class="mt12 debug-reason">
          {{ debugView.focusReason }}
        </a-typography-paragraph>
        <pre v-if="debugView" class="debug-json">{{ JSON.stringify(debugView, null, 2) }}</pre>
      </a-card>
    </div>

    <!-- 新建工作流弹窗 -->
    <a-modal
      v-model:open="showCreateModal"
      title="新建工作流"
      :confirm-loading="creating"
      @ok="handleCreate"
    >
      <a-form :model="createForm" layout="vertical">
        <a-form-item label="工作流名称" required>
          <a-input v-model:value="createForm.name" placeholder="请输入工作流名称" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="createForm.description" :rows="3" placeholder="可选描述" />
        </a-form-item>
        <a-form-item label="模式">
          <a-select v-model:value="createForm.mode">
            <a-select-option :value="0">标准工作流</a-select-option>
            <a-select-option :value="1">ChatFlow</a-select-option>
          </a-select>
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted } from 'vue'

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import {
  listWorkflows,
  listPublishedWorkflows,
  getExecutionCheckpoint,
  getExecutionDebugView,
  recoverExecution,
  createWorkflow,
  copyWorkflow,
  deleteWorkflow,
} from '@/services/api-workflow-v2'
import type {
  WorkflowExecutionCheckpointResponse,
  WorkflowExecutionDebugViewResponse,
  WorkflowListItem
} from '@/types/workflow-v2'
import { resolveCurrentAppId } from '@/utils/app-context'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const creating = ref(false)
const showCreateModal = ref(false)
const keyword = ref('')
const activeTab = ref<'all' | 'published'>('all')
const workflows = ref<WorkflowListItem[]>([])
const pagination = reactive({ current: 1, pageSize: 20, total: 0 })
const executionIdInput = ref<number>()
const checkpoint = ref<WorkflowExecutionCheckpointResponse | null>(null)
const debugView = ref<WorkflowExecutionDebugViewResponse | null>(null)
const checkpointError = ref('')
const inspecting = ref(false)
const recovering = ref(false)

const createForm = reactive({ name: '', description: '', mode: 0 as 0 | 1 })

const columns = [
  { title: '名称', key: 'name', dataIndex: 'name' },
  { title: '模式', key: 'mode', dataIndex: 'mode', width: 100 },
  { title: '状态', key: 'status', dataIndex: 'status', width: 100 },
  { title: '最新版本', key: 'latestVersionNumber', dataIndex: 'latestVersionNumber', width: 120 },
  { title: '更新时间', key: 'updatedAt', dataIndex: 'updatedAt', width: 180 },
  { title: '操作', key: 'actions', width: 180 },
]

function statusColor(status: number) {
  return status === 0 ? 'default' : status === 1 ? 'green' : 'orange'
}
function statusLabel(status: number) {
  return status === 0 ? '草稿' : status === 1 ? '已发布' : '已归档'
}

async function loadList() {
  loading.value = true
  try {
    const query = keyword.value || undefined
    const res = activeTab.value === 'published'
      ? await listPublishedWorkflows(pagination.current, pagination.pageSize, query)
      : await listWorkflows(pagination.current, pagination.pageSize, query)
    if (res.success && res.data) {
      workflows.value = res.data.items as WorkflowListItem[]
      pagination.total = Number(res.data.total)
    }
  } finally {
    loading.value = false
  }
}

function handleTabChange() {
  pagination.current = 1
  void loadList()
}

function handleSearch() {
  pagination.current = 1
  loadList()
}

function handleTableChange(pag: { current: number; pageSize: number }) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  loadList()
}

function openEditor(id: number) {
  const currentAppId = resolveCurrentAppId(route)
  if (!currentAppId) {
    router.push('/console/apps')
    return
  }
  router.push(`/apps/${currentAppId}/workflows/${id}/editor`)
}

async function handleCreate() {
  if (!createForm.name.trim()) {
    message.warning('请输入工作流名称')
    return
  }
  creating.value = true
  try {
    const res = await createWorkflow({ name: createForm.name, description: createForm.description, mode: createForm.mode })
    if (res.success && res.data) {
      showCreateModal.value = false
      message.success('创建成功')
      const currentAppId = resolveCurrentAppId(route)
      if (!currentAppId) {
        router.push('/console/apps')
        return
      }
      router.push(`/apps/${currentAppId}/workflows/${res.data}/editor`)
    }
  } finally {
    creating.value = false
  }
}

async function handleCopy(id: number) {
  const res = await copyWorkflow(id)
  if (res.success) {
    message.success('复制成功')
    loadList()
  }
}

async function handleDelete(id: number) {
  const res = await deleteWorkflow(id)
  if (res.success) {
    message.success('删除成功')
    loadList()
  }
}

function resolveExecutionId() {
  const value = executionIdInput.value
  if (!value || value <= 0) {
    message.warning('请输入有效执行ID')
    return null
  }
  return value
}

async function inspectExecution() {
  const executionId = resolveExecutionId()
  if (!executionId) {
    return
  }

  inspecting.value = true
  checkpointError.value = ''
  try {
    const res = await getExecutionCheckpoint(executionId)
    if (res.success && res.data) {
      checkpoint.value = res.data
    } else {
      checkpointError.value = res.message || '未获取到检查点信息'
    }
  } catch (error) {
    checkpointError.value = (error as Error).message || '获取检查点失败'
  } finally {
    inspecting.value = false
  }
}

async function inspectDebugView() {
  const executionId = resolveExecutionId()
  if (!executionId) {
    return
  }

  inspecting.value = true
  try {
    const res = await getExecutionDebugView(executionId)
    if (res.success && res.data) {
      debugView.value = res.data
    } else {
      message.warning(res.message || '未获取到调试视图')
    }
  } catch (error) {
    message.error((error as Error).message || '获取调试视图失败')
  } finally {
    inspecting.value = false
  }
}

async function recoverFromCheckpoint() {
  const executionId = resolveExecutionId()
  if (!executionId) {
    return
  }

  recovering.value = true
  try {
    const res = await recoverExecution(executionId)
    if (res.success) {
      message.success(`恢复执行已提交，新的执行ID：${res.data?.executionId ?? '-'}`)
    } else {
      message.warning(res.message || '恢复执行未成功')
    }
  } catch (error) {
    message.error((error as Error).message || '恢复执行失败')
  } finally {
    recovering.value = false
  }
}

onMounted(loadList)
</script>

<style scoped>
.workflow-list-page {
  min-height: 100vh;
  background: #f5f5f5;
}
.page-content {
  padding: 0 24px 24px;
}

.mt12 {
  margin-top: 12px;
}

.debug-reason {
  margin-bottom: 8px;
}

.debug-json {
  max-height: 320px;
  overflow: auto;
  white-space: pre-wrap;
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 12px;
}
.danger-link {
  color: #ff4d4f;
}
</style>
