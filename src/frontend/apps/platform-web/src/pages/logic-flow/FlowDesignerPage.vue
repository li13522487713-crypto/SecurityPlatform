<template>
  <div class="logic-flow-designer">
    <a-page-header :title="t('logicFlow.designer')" @back="$router.back()">
      <template #extra>
        <a-button type="primary" @click="openCreateModal">{{ t('common.create') }}</a-button>
      </template>
    </a-page-header>

    <a-row :gutter="16" class="designer-row">
      <a-col :xs="24" :lg="7">
        <a-card :bordered="false" :title="t('logicFlow.flowName')">
          <a-input-search
            v-model:value="keyword"
            allow-clear
            style="margin-bottom: 12px"
            @search="loadFlows"
          />
          <a-select
            v-model:value="statusFilter"
            allow-clear
            style="width: 100%; margin-bottom: 12px"
            :placeholder="t('logicFlow.flowStatus')"
            @change="loadFlows"
          >
            <a-select-option :value="LogicFlowStatus.Draft">{{ t('logicFlow.statusDraft') }}</a-select-option>
            <a-select-option :value="LogicFlowStatus.Published">{{ t('logicFlow.statusPublished') }}</a-select-option>
            <a-select-option :value="LogicFlowStatus.Archived">{{ t('logicFlow.statusArchived') }}</a-select-option>
            <a-select-option :value="LogicFlowStatus.Disabled">{{ t('logicFlow.statusDisabled') }}</a-select-option>
          </a-select>
          <a-table
            :columns="listColumns"
            :data-source="flows"
            :loading="loading"
            :pagination="pagination"
            row-key="id"
            size="small"
            @change="handleTableChange"
            :custom-row="flowListCustomRow"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'status'">
                <a-tag :color="flowStatusColor(record.status)">{{ flowStatusLabel(record.status) }}</a-tag>
              </template>
              <template v-if="column.key === 'triggerType'">
                {{ triggerLabel(record.triggerType) }}
              </template>
            </template>
          </a-table>
        </a-card>
      </a-col>

      <a-col :xs="24" :lg="10">
        <a-card :bordered="false" :title="t('logicFlow.canvasTitle')">
          <FlowDesignerToolbar
            :can-undo="false"
            :can-redo="false"
            :minimap-visible="false"
            @new="openCreateModal"
            @save="openEditModal"
            @publish="handlePublish"
            @validate="handleValidate"
            @undo="showTodoHint"
            @redo="showTodoHint"
            @zoom-in="showTodoHint"
            @zoom-out="showTodoHint"
            @fit="showTodoHint"
            @toggle-minimap="showTodoHint"
          />
          <a-tabs v-model:active-key="centerTab" size="small">
            <a-tab-pane key="canvas" :tab="t('logicFlow.canvasTitle')">
              <FlowCanvas
                :flow-loaded="Boolean(selectedFlow)"
                @canvas-click="designerSelection = { kind: 'none' }"
              />
            </a-tab-pane>
            <a-tab-pane key="structure" :tab="t('logicFlow.designerUi.structure.mainFlow')">
              <FlowStructureTree
                v-model:selected-keys="selectedObjectKeys"
                @select-node="onSelectStructureNode"
              />
            </a-tab-pane>
            <a-tab-pane key="diff" :tab="t('logicFlow.designerUi.diff.summaryTitle')">
              <FlowDiffView />
            </a-tab-pane>
            <a-tab-pane key="debug" :tab="t('logicFlow.designerUi.debug.logTitle')">
              <FlowDebugPanel />
            </a-tab-pane>
          </a-tabs>
        </a-card>
      </a-col>

      <a-col :xs="24" :lg="7">
        <a-card :bordered="false" :title="t('logicFlow.propertyPanel')">
          <a-tabs v-model:active-key="rightTab" size="small">
            <a-tab-pane key="property" :tab="t('logicFlow.propertyPanel')">
              <FlowPropertyPanel :selection="designerSelection" />
              <a-divider />
              <a-space direction="vertical" style="width: 100%">
                <a-button block :disabled="!selectedFlow" @click="openEditModal">{{ t('common.edit') }}</a-button>
                <a-button block :disabled="!selectedFlow" @click="handleArchive">{{ t('logicFlow.archive') }}</a-button>
                <a-button block danger :disabled="!selectedFlow" @click="handleDelete">{{ t('common.delete') }}</a-button>
              </a-space>
            </a-tab-pane>
            <a-tab-pane key="nodes" :tab="t('logicFlow.nodePanel.title')">
              <FlowNodePanel @node-drag-start="onNodeDragStart" />
            </a-tab-pane>
            <a-tab-pane key="objects" :tab="t('logicFlow.designerUi.objectPanel.title')">
              <FlowObjectPanel
                v-model:selected-keys="selectedObjectKeys"
                @select-object="onSelectObject"
              />
            </a-tab-pane>
          </a-tabs>
        </a-card>
      </a-col>
    </a-row>

    <a-modal
      v-model:open="modalOpen"
      :title="isEdit ? t('common.edit') : t('common.create')"
      :confirm-loading="saving"
      width="640px"
      @ok="submitModal"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('logicFlow.flowName')" required>
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item :label="t('logicFlow.functionDesigner.displayName')">
          <a-input v-model:value="form.displayName" />
        </a-form-item>
        <a-form-item :label="t('logicFlow.nodePanel.description')">
          <a-textarea v-model:value="form.description" :rows="2" />
        </a-form-item>
        <a-row :gutter="12">
          <a-col :span="12">
            <a-form-item :label="t('logicFlow.version')">
              <a-input v-model:value="form.version" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="t('logicFlow.triggerType')">
              <a-select v-model:value="form.triggerType" style="width: 100%">
                <a-select-option :value="0">{{ t('logicFlow.triggerManual') }}</a-select-option>
                <a-select-option :value="1">{{ t('logicFlow.triggerScheduled') }}</a-select-option>
                <a-select-option :value="2">{{ t('logicFlow.triggerEvent') }}</a-select-option>
                <a-select-option :value="3">{{ t('logicFlow.triggerApi') }}</a-select-option>
                <a-select-option :value="4">{{ t('logicFlow.triggerDataChange') }}</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item v-if="isEdit" :label="t('common.statusEnabled')">
          <a-switch v-model:checked="form.isEnabled" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { message, Modal } from 'ant-design-vue'
import FlowCanvas from './designer/FlowCanvas.vue'
import FlowPropertyPanel, { type FlowSelection } from './designer/FlowPropertyPanel.vue'
import FlowNodePanel from './designer/FlowNodePanel.vue'
import FlowObjectPanel from './designer/FlowObjectPanel.vue'
import FlowDesignerToolbar from './designer/FlowDesignerToolbar.vue'
import FlowDebugPanel from './designer/FlowDebugPanel.vue'
import FlowStructureTree from './designer/FlowStructureTree.vue'
import FlowDiffView from './designer/FlowDiffView.vue'
import {
  LogicFlowStatus,
  getLogicFlowsPaged,
  getLogicFlowById,
  createLogicFlow,
  updateLogicFlow,
  publishLogicFlow,
  archiveLogicFlow,
  deleteLogicFlow,
  type LogicFlowListItem,
  type LogicFlowDetailResponse,
} from '@/services/api-logic-flow'

const { t } = useI18n()

const loading = ref(false)
const saving = ref(false)
const flows = ref<LogicFlowListItem[]>([])
const total = ref(0)
const pageIndex = ref(1)
const pageSize = ref(10)
const keyword = ref('')
const statusFilter = ref<LogicFlowStatus | undefined>(undefined)
const selectedFlow = ref<LogicFlowDetailResponse | null>(null)
const modalOpen = ref(false)
const isEdit = ref(false)
const centerTab = ref('canvas')
const rightTab = ref('property')
const selectedObjectKeys = ref<string[]>([])
const designerSelection = ref<FlowSelection>({ kind: 'none' })

const form = reactive({
  name: '',
  displayName: '',
  description: '',
  version: '1.0.0',
  triggerType: 0,
  isEnabled: true,
})

const listColumns = computed(() => [
  { title: t('logicFlow.flowName'), dataIndex: 'name', key: 'name', ellipsis: true },
  { title: t('logicFlow.flowStatus'), key: 'status', width: 96 },
  { title: t('logicFlow.triggerType'), key: 'triggerType', width: 112 },
])

const pagination = computed(() => ({
  current: pageIndex.value,
  pageSize: pageSize.value,
  total: total.value,
  showSizeChanger: true,
}))

function flowStatusColor(s: LogicFlowStatus) {
  const map: Record<number, string> = {
    [LogicFlowStatus.Draft]: 'default',
    [LogicFlowStatus.Published]: 'success',
    [LogicFlowStatus.Archived]: 'warning',
    [LogicFlowStatus.Disabled]: 'error',
  }
  return map[s] ?? 'default'
}

function flowStatusLabel(s: LogicFlowStatus) {
  const map: Record<number, string> = {
    [LogicFlowStatus.Draft]: t('logicFlow.statusDraft'),
    [LogicFlowStatus.Published]: t('logicFlow.statusPublished'),
    [LogicFlowStatus.Archived]: t('logicFlow.statusArchived'),
    [LogicFlowStatus.Disabled]: t('logicFlow.statusDisabled'),
  }
  return map[s] ?? String(s)
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

async function loadFlows() {
  loading.value = true
  try {
    const res = await getLogicFlowsPaged({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: keyword.value || undefined,
      status: statusFilter.value,
    })
    if (res?.data) {
      flows.value = res.data.items
      total.value = res.data.total
    }
  } finally {
    loading.value = false
  }
}

function handleTableChange(pag: { current?: number; pageSize?: number }) {
  pageIndex.value = pag.current ?? 1
  pageSize.value = pag.pageSize ?? 10
  loadFlows()
}

function flowListCustomRow(record: LogicFlowListItem) {
  return {
    style: { cursor: 'pointer' },
    onClick: () => {
      void selectFlowByRecord(record)
    },
  }
}

async function selectFlowByRecord(record: LogicFlowListItem) {
  const res = await getLogicFlowById(record.id)
  if (res?.data) {
    selectedFlow.value = res.data
    designerSelection.value = { kind: 'none' }
  }
}

function onSelectObject(key: string) {
  selectedObjectKeys.value = [key]
  designerSelection.value = { kind: 'node', nodeId: key }
}

function onSelectStructureNode(key: string) {
  selectedObjectKeys.value = [key]
  designerSelection.value = { kind: 'node', nodeId: key }
}

function onNodeDragStart() {
  message.info(t('logicFlow.canvasHint'))
}

function showTodoHint() {
  message.info(t('logicFlow.validateHint'))
}

function resetForm() {
  form.name = ''
  form.displayName = ''
  form.description = ''
  form.version = '1.0.0'
  form.triggerType = 0
  form.isEnabled = true
}

function openCreateModal() {
  isEdit.value = false
  resetForm()
  modalOpen.value = true
}

async function openEditModal() {
  if (!selectedFlow.value) return
  isEdit.value = true
  const d = selectedFlow.value
  form.name = d.name
  form.displayName = d.displayName
  form.description = d.description ?? ''
  form.version = d.version
  form.triggerType = d.triggerType
  form.isEnabled = d.isEnabled
  modalOpen.value = true
}

async function submitModal() {
  if (!form.name.trim()) {
    message.warning(t('logicFlow.flowNameRequired'))
    return
  }
  saving.value = true
  try {
    if (isEdit.value && selectedFlow.value) {
      await updateLogicFlow(selectedFlow.value.id, {
        flow: {
          name: form.name,
          displayName: form.displayName,
          description: form.description,
          version: form.version,
          triggerType: form.triggerType,
          triggerConfigJson: selectedFlow.value.triggerConfigJson,
          inputSchemaJson: selectedFlow.value.inputSchemaJson,
          outputSchemaJson: selectedFlow.value.outputSchemaJson,
          maxRetries: selectedFlow.value.maxRetries,
          timeoutSeconds: selectedFlow.value.timeoutSeconds,
          isEnabled: form.isEnabled,
        },
        nodes: selectedFlow.value.nodes.map((n) => ({
          nodeTypeKey: n.nodeTypeKey,
          nodeInstanceKey: n.nodeInstanceKey,
          displayName: n.displayName,
          configJson: n.configJson,
          positionX: n.positionX,
          positionY: n.positionY,
          sortOrder: n.sortOrder,
        })),
        edges: selectedFlow.value.edges.map((e) => ({
          sourceNodeKey: e.sourceNodeKey,
          sourcePortKey: e.sourcePortKey,
          targetNodeKey: e.targetNodeKey,
          targetPortKey: e.targetPortKey,
          conditionExpression: e.conditionExpression ?? undefined,
          priority: e.priority,
          label: e.label ?? undefined,
          edgeStyle: e.edgeStyle ?? undefined,
        })),
      })
      message.success(t('crud.updateSuccess'))
    } else {
      await createLogicFlow({
        flow: {
          name: form.name,
          displayName: form.displayName,
          description: form.description,
          version: form.version,
          triggerType: form.triggerType,
          triggerConfigJson: '{}',
          inputSchemaJson: '{}',
          outputSchemaJson: '{}',
          maxRetries: 3,
          timeoutSeconds: 300,
        },
        nodes: [],
        edges: [],
      })
      message.success(t('crud.createSuccess'))
    }
    modalOpen.value = false
    await loadFlows()
  } finally {
    saving.value = false
  }
}

async function handlePublish() {
  if (!selectedFlow.value) return
  await publishLogicFlow(selectedFlow.value.id)
  message.success(t('logicFlow.publishOk'))
  await loadFlows()
  const res = await getLogicFlowById(selectedFlow.value.id)
  if (res?.data) selectedFlow.value = res.data
}

function handleValidate() {
  message.info(t('logicFlow.validateHint'))
}

async function handleArchive() {
  if (!selectedFlow.value) return
  await archiveLogicFlow(selectedFlow.value.id)
  message.success(t('logicFlow.archiveOk'))
  await loadFlows()
  selectedFlow.value = null
}

function handleDelete() {
  if (!selectedFlow.value) return
  Modal.confirm({
    title: t('common.delete'),
    onOk: async () => {
      await deleteLogicFlow(selectedFlow.value!.id)
      message.success(t('crud.deleteSuccess'))
      selectedFlow.value = null
      await loadFlows()
    },
  })
}

onMounted(loadFlows)
</script>

<style scoped>
.designer-row {
  margin-top: 8px;
}
</style>
