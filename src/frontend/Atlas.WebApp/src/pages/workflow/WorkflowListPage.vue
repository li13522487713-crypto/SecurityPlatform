<template>
  <CrudPageLayout
    v-model:keyword="keyword"
    :title="t('workflow.listTitle')"
    :search-placeholder="t('workflow.searchPlaceholder')"
    @search="handleSearch"
    @reset="handleReset"
  >
    <template #toolbar-actions>
      <a-button type="primary" @click="showCreateModal = true">
        <template #icon><PlusOutlined /></template>
        {{ t('workflow.newWorkflow') }}
      </a-button>
    </template>

    <template #toolbar-right>
      <a-space>
        <a-button @click="handleRefresh">
          <template #icon><ReloadOutlined /></template>
          {{ t('workflow.refresh') }}
        </a-button>
        <a-tag color="blue">{{ pagination.total }}</a-tag>
      </a-space>
    </template>

    <template #table>
      <div class="workflow-list-panel">
        <div class="workflow-list-panel__header">
          <a-tabs class="workflow-tabs" v-model:active-key="activeTab" @change="handleTabChange">
            <a-tab-pane key="all" :tab="t('workflow.tabAll')" />
            <a-tab-pane key="published" :tab="t('workflow.tabPublished')" />
          </a-tabs>
          <a-space class="workflow-summary" :size="8" wrap>
            <a-tag>{{ t('workflow.tabAll') }}: {{ listSummary.all }}</a-tag>
            <a-tag color="green">{{ t('workflow.tabPublished') }}: {{ listSummary.published }}</a-tag>
          </a-space>
        </div>

        <a-table
          :data-source="workflows"
          :columns="columns"
          :loading="loading"
          :pagination="tablePagination"
          :scroll="{ x: 920 }"
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
              <a-tag>{{ record.mode === 0 ? t('workflow.modeStandard') : t('workflow.modeChatFlow') }}</a-tag>
            </template>
            <template v-if="column.key === 'actions'">
              <a-space :size="[6, 8]" wrap>
                <a @click="openEditor(record.id)">{{ t('workflow.edit') }}</a>
                <a-divider type="vertical" />
                <a @click="handleCopy(record.id)">{{ t('workflow.copy') }}</a>
                <a-divider type="vertical" />
                <a-popconfirm :title="t('workflow.deleteConfirm')" @confirm="handleDelete(record.id)">
                  <a class="danger-link">{{ t('workflow.delete') }}</a>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </div>

      <a-collapse class="mt12" :bordered="false">
        <a-collapse-panel key="debug" :header="t('workflow.debugCardTitle')">
          <a-space wrap>
            <a-input-number
              v-model:value="executionIdInput"
              :min="1"
              :precision="0"
              style="width: 220px"
              :placeholder="t('workflow.execIdPlaceholder')"
            />
            <a-button :loading="inspecting" @click="inspectExecution">{{ t('workflow.getCheckpoint') }}</a-button>
            <a-button :loading="inspecting" @click="inspectDebugView">{{ t('workflow.getDebugView') }}</a-button>
            <a-button type="primary" :loading="recovering" @click="recoverFromCheckpoint">{{ t('workflow.recoverExec') }}</a-button>
          </a-space>
          <a-alert v-if="checkpointError" class="mt12" type="warning" :message="checkpointError" show-icon />
          <a-descriptions v-if="checkpoint" class="mt12" bordered :column="2" size="small">
            <a-descriptions-item :label="t('workflow.labelExecId')">{{ checkpoint.executionId }}</a-descriptions-item>
            <a-descriptions-item :label="t('workflow.labelWorkflowId')">{{ checkpoint.workflowId }}</a-descriptions-item>
            <a-descriptions-item :label="t('workflow.labelStatus')">{{ checkpoint.status }}</a-descriptions-item>
            <a-descriptions-item :label="t('workflow.labelLastNode')">{{ checkpoint.lastNodeKey || "-" }}</a-descriptions-item>
            <a-descriptions-item :label="t('workflow.labelStartedAt')">{{ checkpoint.startedAt }}</a-descriptions-item>
            <a-descriptions-item :label="t('workflow.checkpointCompletedAt')">{{ checkpoint.completedAt || "-" }}</a-descriptions-item>
          </a-descriptions>
          <a-typography-paragraph v-if="debugView" class="mt12 debug-reason">
            {{ debugView.focusReason }}
          </a-typography-paragraph>
          <pre v-if="debugView" class="debug-json">{{ JSON.stringify(debugView, null, 2) }}</pre>
        </a-collapse-panel>
      </a-collapse>
    </template>
  </CrudPageLayout>

  <a-modal
    v-model:open="showCreateModal"
    :title="t('workflow.createModalTitle')"
    :confirm-loading="creating"
    @ok="handleCreate"
  >
    <a-form :model="createForm" layout="vertical">
      <a-form-item :label="t('workflow.labelName')" required>
        <a-input v-model:value="createForm.name" :placeholder="t('workflow.phName')" />
      </a-form-item>
      <a-form-item :label="t('workflow.labelDescription')">
        <a-textarea v-model:value="createForm.description" :rows="3" :placeholder="t('workflow.phDescription')" />
      </a-form-item>
      <a-form-item :label="t('workflow.labelMode')">
        <a-select v-model:value="createForm.mode">
          <a-select-option :value="0">{{ t('workflow.modeStandardOption') }}</a-select-option>
          <a-select-option :value="1">{{ t('workflow.modeChatFlow') }}</a-select-option>
        </a-select>
      </a-form-item>
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons-vue'
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
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue"

const { t } = useI18n()
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

const columns = computed(() => [
  { title: t('workflow.colName'), key: 'name', dataIndex: 'name' },
  { title: t('workflow.colMode'), key: 'mode', dataIndex: 'mode', width: 100 },
  { title: t('workflow.colStatusShort'), key: 'status', dataIndex: 'status', width: 100 },
  { title: t('workflow.colLatestVersion'), key: 'latestVersionNumber', dataIndex: 'latestVersionNumber', width: 120 },
  { title: t('workflow.colUpdatedAt'), key: 'updatedAt', dataIndex: 'updatedAt', width: 180 },
  { title: t('workflow.colActions'), key: 'actions', width: 200 },
])

const tablePagination = computed(() => ({
  current: pagination.current,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  showTotal: (total: number) => `${total}`
}))

const listSummary = computed(() => {
  let published = 0
  for (const workflow of workflows.value) {
    if (workflow.status === 1) {
      published += 1
    }
  }
  return {
    all: workflows.value.length,
    published
  }
})

function statusColor(status: number) {
  return status === 0 ? 'default' : status === 1 ? 'green' : 'orange'
}
function statusLabel(status: number) {
  return status === 0 ? t('workflow.statusDraft') : status === 1 ? t('workflow.statusPublished') : t('workflow.statusArchived')
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

function handleReset() {
  keyword.value = ''
  pagination.current = 1
  void loadList()
}

function handleRefresh() {
  void loadList()
}

function handleTableChange(pag: { current: number; pageSize: number }) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  void loadList()
}

function openEditor(id: string) {
  const currentAppId = resolveCurrentAppId(route)
  if (!currentAppId) {
    router.push('/console/apps')
    return
  }
  router.push(`/apps/${currentAppId}/workflows/${id}/editor`)
}

async function handleCreate() {
  if (!createForm.name.trim()) {
    message.warning(t('workflow.warnName'))
    return
  }
  creating.value = true
  try {
    const res = await createWorkflow({ name: createForm.name, description: createForm.description, mode: createForm.mode })
    if (res.success && res.data) {
      showCreateModal.value = false
      message.success(t('workflow.createOk'))
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

async function handleCopy(id: string) {
  const res = await copyWorkflow(id)
  if (res.success) {
    message.success(t('workflow.copyOk'))
    void loadList()
  }
}

async function handleDelete(id: string) {
  const res = await deleteWorkflow(id)
  if (res.success) {
    message.success(t('workflow.deleteOk'))
    void loadList()
  }
}

function resolveExecutionId() {
  const value = executionIdInput.value
  if (!value || value <= 0) {
    message.warning(t('workflow.warnExecId'))
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
      checkpointError.value = res.message || t('workflow.checkpointMissing')
    }
  } catch (error) {
    checkpointError.value = (error as Error).message || t('workflow.checkpointFailed')
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
      message.warning(res.message || t('workflow.debugViewWarn'))
    }
  } catch (error) {
    message.error((error as Error).message || t('workflow.debugViewFailed'))
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
      message.success(t('workflow.recoverSubmitted', { id: res.data?.executionId ?? '-' }))
    } else {
      message.warning(res.message || t('workflow.recoverWarn'))
    }
  } catch (error) {
    message.error((error as Error).message || t('workflow.recoverFailed'))
  } finally {
    recovering.value = false
  }
}

onMounted(loadList)
</script>

<style scoped>
.mt12 {
  margin-top: 12px;
}

.workflow-list-panel {
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  background: #fff;
  overflow: hidden;
}

.workflow-list-panel__header {
  padding: 12px 16px;
  border-bottom: 1px solid #f0f0f0;
}

.workflow-tabs {
  margin-bottom: 8px;
}

.workflow-summary {
  margin-bottom: 0;
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

@media (max-width: 992px) {
  .workflow-tabs {
    margin-bottom: 10px;
  }
}
</style>
