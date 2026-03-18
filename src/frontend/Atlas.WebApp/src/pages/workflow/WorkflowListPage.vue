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
          <a-input-search
            v-model:value="keyword"
            placeholder="搜索工作流名称"
            style="width: 280px"
            @search="handleSearch"
            allow-clear
          />
        </template>

        <a-table
          :dataSource="workflows"
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
    </div>

    <!-- 新建工作流弹窗 -->
    <a-modal
      v-model:open="showCreateModal"
      title="新建工作流"
      @ok="handleCreate"
      :confirm-loading="creating"
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
            <a-select-option :value="3">ChatFlow</a-select-option>
          </a-select>
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import {
  listWorkflows,
  createWorkflow,
  copyWorkflow,
  deleteWorkflow,
} from '@/services/api-workflow-v2'
import type { WorkflowListItem } from '@/types/workflow-v2'
import { resolveCurrentAppId } from '@/utils/app-context'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const creating = ref(false)
const showCreateModal = ref(false)
const keyword = ref('')
const workflows = ref<WorkflowListItem[]>([])
const pagination = reactive({ current: 1, pageSize: 20, total: 0 })

const createForm = reactive({ name: '', description: '', mode: 0 as 0 | 3 })

const columns = [
  { title: '名称', key: 'name', dataIndex: 'name' },
  { title: '模式', key: 'mode', dataIndex: 'mode', width: 100 },
  { title: '状态', key: 'status', dataIndex: 'status', width: 100 },
  { title: '最新版本', key: 'latestVersion', dataIndex: 'latestVersion', width: 120 },
  { title: '更新时间', key: 'updatedAt', dataIndex: 'updatedAt', width: 180 },
  { title: '操作', key: 'actions', width: 180 },
]

function statusColor(status: number) {
  return status === 0 ? 'default' : status === 1 ? 'green' : 'orange'
}
function statusLabel(status: number) {
  return status === 0 ? '草稿' : status === 1 ? '已发布' : '已禁用'
}

async function loadList() {
  loading.value = true
  try {
    const res = await listWorkflows(pagination.current, pagination.pageSize, keyword.value || undefined)
    if (res.success && res.data) {
      workflows.value = res.data.items as WorkflowListItem[]
      pagination.total = Number(res.data.total)
    }
  } finally {
    loading.value = false
  }
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
.danger-link {
  color: #ff4d4f;
}
</style>
