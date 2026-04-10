<template>
  <div class="workflow-editor-page">
    <EditorToolbar
      v-model:name="workflowName"
      :is-dirty="isDirty"
      :saving="saving"
      :show-test-panel="showTestPanel"
      :auto-saved-at="lastSavedAt"
      @back="backToList"
      @name-blur="handleNameBlur"
      @save-draft="handleSaveDraft"
      @publish="showPublishModal = true"
      @toggle-test-panel="showTestPanel = !showTestPanel"
      @open-version-history="showVersionDrawer = true"
      @menu-action="handleToolbarMenuAction"
    />

    <div class="editor-body">
      <!-- 左侧节点面板 -->
      <NodePanel @drag-start="handleNodeDragStart" />

      <!-- 中央 Vue Flow 画布 -->
      <div class="canvas-container" @dragover.prevent @drop="handleDrop">
        <VueFlow
          v-model:nodes="vfNodes"
          v-model:edges="vfEdges"
          :node-types="nodeTypes"
          :edge-types="edgeTypes"
          :default-edge-options="defaultEdgeOptions"
          :connection-mode="ConnectionMode.Loose"
          :fit-view-on-init="true"
          class="workflow-canvas"
          @node-click="handleNodeClick"
          @pane-click="handlePaneClick"
          @connect="handleConnect"
          @nodes-change="handleNodesChange"
          @edges-change="handleEdgesChange"
        >
          <Controls />
          <MiniMap
            :pannable="true"
            :zoomable="true"
            :node-color="getNodeStatusColor"
            style="background: #0d1117; border: 1px solid #21262d;"
          />
        </VueFlow>
        <div class="debug-panel">
          <div class="debug-panel-header">
            <span>{{ t("workflow.debugCardTitle") }}</span>
            <a-button type="text" size="small" @click="debugLines = []">Clear</a-button>
          </div>
          <div class="debug-panel-body">
            <div v-if="debugLines.length === 0" class="debug-empty">No logs</div>
            <div v-for="(line, index) in debugLines.slice(-120)" :key="`${index}-${line}`" class="debug-line">{{ line }}</div>
          </div>
        </div>
      </div>

      <!-- 右侧属性面板（节点未选中且未开测试时不显示） -->
      <PropertiesPanel
        v-if="selectedNode && !showTestPanel"
        :node="selectedNode"
        :node-types-metadata="nodeTypesMetadata"
        @update="handleNodeConfigUpdate"
        @close="selectedNode = null"
      />

      <!-- 右侧测试运行面板 -->
      <TestRunPanel
        v-if="showTestPanel"
        :workflow-id="workflowId"
        :versions="workflowVersions"
        :api-client="apiClient"
        @close="showTestPanel = false"
        @publish="showPublishModal = true"
        @node-status-update="handleNodeStatusUpdate"
        @debug-log="handleDebugLog"
      />
    </div>

    <!-- 发布弹窗 -->
    <a-modal
      v-model:open="showPublishModal"
      :title="t('workflow.publishModalTitle')"
      :confirm-loading="publishing"
      :ok-text="t('workflow.publishOk')"
      :cancel-text="t('common.cancel')"
      @ok="handlePublish"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('workflow.labelChangelog')">
          <a-textarea v-model:value="changeLog" :rows="3" :placeholder="t('workflow.phChangelog')" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-drawer v-model:open="showVersionDrawer" :title="t('workflow.colLatestVersion')" width="420">
      <a-form layout="vertical" size="small" style="margin-bottom: 12px">
        <a-form-item label="版本对比">
          <a-space>
            <a-select v-model:value="diffFromVersionId" style="width: 150px" placeholder="旧版本">
              <a-select-option v-for="item in workflowVersions" :key="`from-${item.id}`" :value="item.id">
                v{{ item.versionNumber }}
              </a-select-option>
            </a-select>
            <a-select v-model:value="diffToVersionId" style="width: 150px" placeholder="新版本">
              <a-select-option v-for="item in workflowVersions" :key="`to-${item.id}`" :value="item.id">
                v{{ item.versionNumber }}
              </a-select-option>
            </a-select>
            <a-button size="small" @click="computeLocalVersionDiff">对比</a-button>
          </a-space>
        </a-form-item>
        <a-form-item v-if="versionDiffText">
          <a-textarea :value="versionDiffText" :rows="6" readonly />
        </a-form-item>
      </a-form>
      <a-timeline>
        <a-timeline-item v-for="item in workflowVersions" :key="item.id">
          <div class="version-title">v{{ item.versionNumber }}</div>
          <div class="version-meta">{{ item.publishedAt }}</div>
          <div class="version-meta">{{ item.changeLog || "-" }}</div>
        </a-timeline-item>
      </a-timeline>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, markRaw, type Component, onUnmounted } from 'vue'
import { useI18n } from 'vue-i18n'

import { message } from 'ant-design-vue'
import {
  VueFlow,
  type Node as VfNode,
  type Edge as VfEdge,
  ConnectionMode,
  type Connection,
  type NodeChange,
  type EdgeChange,
} from '@vue-flow/core'
import { Controls } from '@vue-flow/controls'
import { MiniMap } from '@vue-flow/minimap'
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import '@vue-flow/controls/dist/style.css'
import '@vue-flow/minimap/dist/style.css'

import EditorToolbar from '../components/workflow/EditorToolbar.vue'
import NodePanel from '../components/workflow/panels/NodePanel.vue'
import PropertiesPanel from '../components/workflow/panels/PropertiesPanel.vue'
import TestRunPanel from '../components/workflow/panels/TestRunPanel.vue'
import WorkflowNodeRenderer from '../components/workflow/nodes/WorkflowNodeRenderer.vue'
import WorkflowEdgeRenderer from '../components/workflow/nodes/WorkflowEdgeRenderer.vue'
import { mergeWorkflowEditorLocaleMessages } from '../i18n'

import type { createWorkflowV2Api } from '../api'
import { normalizeNodeTypeKey } from '../types'
import type { CanvasSchema, ConnectionSchema, NodeSchema, NodeTypeMetadata, WorkflowVersionItem } from '../types'

const i18n = useI18n({ useScope: 'global' })
const { t } = i18n
mergeWorkflowEditorLocaleMessages(i18n)
type WorkflowEditorApiClient = ReturnType<typeof createWorkflowV2Api>

const props = defineProps<{
  workflowId: string
  apiClient: WorkflowEditorApiClient
}>()

const emit = defineEmits<{
  (e: 'back'): void
}>()

const workflowId = computed(() => String(props.workflowId))
const apiClient = computed(() => props.apiClient)

const workflowName = ref('')
const isDirty = ref(false)
const saving = ref(false)
const publishing = ref(false)
const showPublishModal = ref(false)
const showVersionDrawer = ref(false)
const showTestPanel = ref(false)
const changeLog = ref('')
const lastSavedAt = ref('')
const selectedNode = ref<NodeSchema | null>(null)
const nodeTypesMetadata = ref<NodeTypeMetadata[]>([])
const workflowVersions = ref<WorkflowVersionItem[]>([])
const diffFromVersionId = ref<string>()
const diffToVersionId = ref<string>()
const versionDiffText = ref("")

const vfNodes = ref<VfNode[]>([])
const vfEdges = ref<VfEdge[]>([])
const historySnapshots = ref<CanvasSchema[]>([])
const historyIndex = ref(-1)
const copiedNode = ref<NodeSchema | null>(null)

// 节点执行状态（key → status string）
const nodeRunStatus = ref<Record<string, string>>({})
const debugLines = ref<string[]>([])

const _nr = markRaw(WorkflowNodeRenderer) as Component
const _er = markRaw(WorkflowEdgeRenderer) as Component
const nodeTypes: Record<string, Component> = {
  Entry: _nr,
  Exit: _nr,
  Llm: _nr,
  Agent: _nr,
  Plugin: _nr,
  IntentDetector: _nr,
  QuestionAnswer: _nr,
  Selector: _nr,
  Loop: _nr,
  Batch: _nr,
  Break: _nr,
  Continue: _nr,
  SubWorkflow: _nr,
  CodeRunner: _nr,
  HttpRequester: _nr,
  DatabaseQuery: _nr,
  DatabaseInsert: _nr,
  DatabaseUpdate: _nr,
  DatabaseDelete: _nr,
  DatabaseCustomSql: _nr,
  KnowledgeRetriever: _nr,
  KnowledgeIndexer: _nr,
  Ltm: _nr,
  AssignVariable: _nr,
  VariableAssignerWithinLoop: _nr,
  VariableAggregator: _nr,
  InputReceiver: _nr,
  OutputEmitter: _nr,
  CreateConversation: _nr,
  ConversationList: _nr,
  ConversationUpdate: _nr,
  ConversationDelete: _nr,
  ConversationHistory: _nr,
  ClearConversationHistory: _nr,
  MessageList: _nr,
  CreateMessage: _nr,
  EditMessage: _nr,
  DeleteMessage: _nr,
  JsonSerialization: _nr,
  JsonDeserialization: _nr,
  TextProcessor: _nr,
  Comment: _nr,
  // 兼容历史草稿中的旧键名
  LLM: _nr,
  If: _nr,
}
const edgeTypes: Record<string, Component> = {
  workflow: _er,
}

const defaultEdgeOptions = {
  type: 'workflow',
  animated: false,
  style: { stroke: '#4b5563', strokeWidth: 2 },
}

let draggingNodeType: string | null = null

function snapshotCanvas() {
  const snapshot: CanvasSchema = JSON.parse(JSON.stringify(currentCanvas.value)) as CanvasSchema
  if (historyIndex.value >= 0) {
    const current = JSON.stringify(historySnapshots.value[historyIndex.value] ?? {})
    const next = JSON.stringify(snapshot)
    if (current === next) {
      return
    }
  }
  historySnapshots.value = historySnapshots.value.slice(0, historyIndex.value + 1)
  historySnapshots.value.push(snapshot)
  if (historySnapshots.value.length > 50) {
    historySnapshots.value.shift()
  }
  historyIndex.value = historySnapshots.value.length - 1
}

function markDirtyAndSnapshot() {
  isDirty.value = true
  snapshotCanvas()
}

function cloneNodeSchema(node: NodeSchema): NodeSchema {
  return JSON.parse(JSON.stringify(node)) as NodeSchema
}

function backToList() {
  emit('back')
}

const currentCanvas = computed<CanvasSchema>(() => {
  const nodes: NodeSchema[] = vfNodes.value.map(n => ({
    key: n.id,
    type: (n.type ?? 'Unknown') as NodeSchema['type'],
    title: (n.data?.title as string) ?? n.type ?? '',
    layout: { x: n.position.x, y: n.position.y, width: 160, height: 60 },
    configs: (n.data?.configs as Record<string, unknown>) ?? {},
    inputMappings: (n.data?.inputMappings as Record<string, string>) ?? {},
  }))

  const connections: ConnectionSchema[] = vfEdges.value.map((e) => ({
    fromNode: e.source,
    fromPort: e.sourceHandle ?? 'output',
    toNode: e.target,
    toPort: e.targetHandle ?? 'input',
    condition: (e.data as { condition?: string } | undefined)?.condition ?? null,
  }))

  return { nodes, connections }
})

async function loadWorkflow() {
  try {
    const res = await apiClient.value.getDetail(workflowId.value)
    if (res.success && res.data) {
      workflowName.value = res.data.name
      if (res.data.canvasJson) {
        const canvas = JSON.parse(res.data.canvasJson) as CanvasSchema
        applyCanvasToVueFlow(canvas)
      } else {
        initDefaultCanvas()
      }
    }
    // 加载版本列表
    const vRes = await apiClient.value.getVersions(workflowId.value)
    if (vRes.success && vRes.data) {
      workflowVersions.value = vRes.data
      diffFromVersionId.value = vRes.data[1]?.id ?? vRes.data[0]?.id
      diffToVersionId.value = vRes.data[0]?.id
    }
  } catch {
    workflowName.value = t('workflow.defaultName')
    initDefaultCanvas()
  }
}

async function loadNodeTypes() {
  try {
    const res = await apiClient.value.getNodeTypes()
    if (res.success && res.data) {
      nodeTypesMetadata.value = res.data
    }
  } catch {
    // ignore
  }
}

function applyCanvasToVueFlow(canvas: CanvasSchema) {
  const toVfNode = (n: NodeSchema): VfNode => {
    const nodeType = String(n.type)
    return {
      id: n.key,
      type: nodeType,
      position: { x: n.layout?.x ?? 0, y: n.layout?.y ?? 0 },
      data: {
        title: n.title,
        configs: n.configs,
        inputMappings: n.inputMappings,
        nodeType,
        __status: nodeRunStatus.value[n.key] ?? '',
      },
    }
  }

  const toVfEdge = (c: ConnectionSchema, i: number): VfEdge => ({
    id: `e-${c.fromNode}-${c.toNode}-${i}`,
    source: c.fromNode,
    sourceHandle: c.fromPort ?? 'output',
    target: c.toNode,
    targetHandle: c.toPort ?? 'input',
    type: 'workflow',
    style: { stroke: '#4b5563', strokeWidth: 2 },
    data: { condition: c.condition ?? '' },
    animated: !!c.condition,
  })

  vfNodes.value = canvas.nodes.map(toVfNode)
  vfEdges.value = canvas.connections.map(toVfEdge)

  isDirty.value = false
  snapshotCanvas()
}

function initDefaultCanvas() {
  vfNodes.value = [
    {
      id: 'entry_1',
      type: 'Entry',
      position: { x: 100, y: 200 },
      data: { title: t('workflow.nodeStart'), configs: {}, inputMappings: {}, nodeType: 'Entry', __status: '' },
    },
    {
      id: 'exit_1',
      type: 'Exit',
      position: { x: 600, y: 200 },
      data: { title: t('workflow.nodeEnd'), configs: {}, inputMappings: {}, nodeType: 'Exit', __status: '' },
    },
  ]
  vfEdges.value = [
    {
      id: 'e-entry-exit',
      source: 'entry_1',
      target: 'exit_1',
      type: 'workflow',
      style: { stroke: '#4b5563', strokeWidth: 2 },
      data: { condition: '' },
    },
  ]
  isDirty.value = false
  snapshotCanvas()
}

async function handleSaveDraft() {
  saving.value = true
  try {
    const canvasJson = JSON.stringify(currentCanvas.value)
    const res = await apiClient.value.saveDraft(workflowId.value, { canvasJson })
    if (res.success) {
      isDirty.value = false
      lastSavedAt.value = new Date().toLocaleTimeString()
      message.success(t('workflow.draftSaved'))
    }
  } finally {
    saving.value = false
  }
}

async function handlePublish() {
  publishing.value = true
  try {
    const canvasJson = JSON.stringify(currentCanvas.value)
    await apiClient.value.saveDraft(workflowId.value, { canvasJson })
    lastSavedAt.value = new Date().toLocaleTimeString()

    const res = await apiClient.value.publish(workflowId.value, { changeLog: changeLog.value })
    if (res.success) {
      showPublishModal.value = false
      message.success(t('workflow.publishSuccess'))
      changeLog.value = ''
      // 刷新版本列表
      const vRes = await apiClient.value.getVersions(workflowId.value)
      if (vRes.success && vRes.data) {
        workflowVersions.value = vRes.data
      }
    }
  } finally {
    publishing.value = false
  }
}

function handleToolbarMenuAction(key: string) {
  if (key === 'export-json') {
    const content = JSON.stringify(currentCanvas.value, null, 2)
    const blob = new Blob([content], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `workflow-${workflowId.value}-canvas.json`
    link.click()
    URL.revokeObjectURL(url)
    return
  }

  if (key === 'import-json') {
    const input = document.createElement('input')
    input.type = 'file'
    input.accept = 'application/json'
    input.onchange = () => {
      const file = input.files?.[0]
      if (!file) {
        return
      }

      const reader = new FileReader()
      reader.onload = () => {
        try {
          const parsed = JSON.parse(String(reader.result ?? '{}')) as CanvasSchema
          applyCanvasToVueFlow(parsed)
          markDirtyAndSnapshot()
          message.success(t('workflow.importJsonSuccess'))
        } catch {
          message.error(t('workflow.importJsonFailed'))
        }
      }
      reader.readAsText(file)
    }
    input.click()
    return
  }

  if (key === 'reset-canvas') {
    initDefaultCanvas()
    markDirtyAndSnapshot()
    message.success(t('workflow.resetCanvasSuccess'))
  }
}

async function handleNameBlur() {
  if (!workflowName.value.trim()) return
  await apiClient.value.updateMeta(workflowId.value, {
    name: workflowName.value,
    description: '',
  })
}

function handleNodeClick(event: { node: VfNode }) {
  const vfNode = event.node
  selectedNode.value = {
    key: vfNode.id,
    type: (vfNode.type ?? 'Unknown') as NodeSchema['type'],
    title: (vfNode.data?.title as string) ?? '',
    layout: { x: vfNode.position.x, y: vfNode.position.y, width: 160, height: 60 },
    configs: (vfNode.data?.configs as Record<string, unknown>) ?? {},
    inputMappings: (vfNode.data?.inputMappings as Record<string, string>) ?? {},
  }
}

function handlePaneClick() {
  selectedNode.value = null
}

function handleConnect(params: Connection) {
  const newEdge: VfEdge = {
    id: `e-${params.source}-${params.target}-${Date.now()}`,
    source: params.source!,
    sourceHandle: params.sourceHandle,
    target: params.target!,
    targetHandle: params.targetHandle,
    type: 'workflow',
    style: { stroke: '#4b5563', strokeWidth: 2 },
    data: { condition: '' },
  }
  // @ts-expect-error VueFlow Edge 泛型在 vue-tsc 中会触发深度推导错误（TS2589）
  vfEdges.value.push(newEdge)
  markDirtyAndSnapshot()
}

function handleNodesChange(changes: NodeChange[]) {
  if (changes.some(c => c.type !== 'select' && c.type !== 'dimensions')) {
    markDirtyAndSnapshot()
  }
}

function handleEdgesChange(changes: EdgeChange[]) {
  if (changes.some(c => c.type !== 'select')) {
    markDirtyAndSnapshot()
  }
}

function handleNodeDragStart(nodeType: string) {
  draggingNodeType = nodeType
}

function handleDrop(event: DragEvent) {
  if (!draggingNodeType) return
  const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
  const x = event.clientX - rect.left - 80
  const y = event.clientY - rect.top - 30
  const nodeKey = `${draggingNodeType.toLowerCase()}_${Date.now()}`

  const normalizedDraggingType = normalizeNodeTypeKey(draggingNodeType)
  const meta = nodeTypesMetadata.value.find(m => normalizeNodeTypeKey(String(m.key)) === normalizedDraggingType)
  const title = meta?.name ?? draggingNodeType

  vfNodes.value = vfNodes.value.concat({
    id: nodeKey,
    type: normalizedDraggingType,
    position: { x, y },
    data: { title, configs: {}, inputMappings: {}, nodeType: normalizedDraggingType, __status: '' },
  })

  markDirtyAndSnapshot()
  draggingNodeType = null
}

function handleNodeConfigUpdate(
  nodeKey: string,
  configs: Record<string, unknown>,
  inputMappings: Record<string, string>,
  title: string
) {
  const idx = vfNodes.value.findIndex(n => n.id === nodeKey)
  if (idx !== -1) {
    const updated = [...vfNodes.value]
    updated[idx] = {
      ...updated[idx],
      data: { ...updated[idx].data, configs, inputMappings, title },
    }
    vfNodes.value = updated

    // 同步更新 selectedNode
    if (selectedNode.value?.key === nodeKey) {
      selectedNode.value = { ...selectedNode.value, configs, inputMappings, title }
    }
    markDirtyAndSnapshot()
  }
}

// 测试执行时高亮节点状态
function handleNodeStatusUpdate(nodeKey: string, status: string) {
  nodeRunStatus.value = { ...nodeRunStatus.value, [nodeKey]: status }
  const idx = vfNodes.value.findIndex(n => n.id === nodeKey)
  if (idx !== -1) {
    const updated = [...vfNodes.value]
    updated[idx] = {
      ...updated[idx],
      data: { ...updated[idx].data, __status: status },
    }
    vfNodes.value = updated
  }

  vfEdges.value = vfEdges.value.map((edge) => {
    if (edge.source !== nodeKey) {
      return edge
    }

    if (status === 'running') {
      return {
        ...edge,
        animated: true,
        style: { ...(edge.style ?? {}), stroke: '#4e40e5', strokeWidth: 2.8 },
        data: { ...(edge.data as Record<string, unknown> ?? {}), running: true }
      }
    }

    if (status === 'success') {
      return {
        ...edge,
        animated: false,
        style: { ...(edge.style ?? {}), stroke: '#52c41a', strokeWidth: 2.2 },
        data: { ...(edge.data as Record<string, unknown> ?? {}), running: false }
      }
    }

    if (status === 'failed') {
      return {
        ...edge,
        animated: false,
        style: { ...(edge.style ?? {}), stroke: '#ff4d4f', strokeWidth: 2.2 },
        data: { ...(edge.data as Record<string, unknown> ?? {}), running: false }
      }
    }

    return {
      ...edge,
      animated: false,
      style: { ...(edge.style ?? {}), stroke: '#4b5563', strokeWidth: 2 },
      data: { ...(edge.data as Record<string, unknown> ?? {}), running: false }
    }
  })
}

function handleDebugLog(line: string) {
  debugLines.value = [...debugLines.value, line]
}

function computeLocalVersionDiff() {
  const from = workflowVersions.value.find((item) => item.id === diffFromVersionId.value)
  const to = workflowVersions.value.find((item) => item.id === diffToVersionId.value)
  if (!from || !to) {
    versionDiffText.value = "请选择两个版本"
    return
  }
  try {
    const fromCanvas = JSON.parse(from.canvasJson) as CanvasSchema
    const toCanvas = JSON.parse(to.canvasJson) as CanvasSchema
    const fromNodeKeys = new Set((fromCanvas.nodes ?? []).map((node) => node.key))
    const toNodeKeys = new Set((toCanvas.nodes ?? []).map((node) => node.key))
    const added = [...toNodeKeys].filter((key) => !fromNodeKeys.has(key))
    const removed = [...fromNodeKeys].filter((key) => !toNodeKeys.has(key))
    const lines = [
      `from: v${from.versionNumber}`,
      `to: v${to.versionNumber}`,
      `nodes: ${fromCanvas.nodes.length} -> ${toCanvas.nodes.length}`,
      `connections: ${fromCanvas.connections.length} -> ${toCanvas.connections.length}`,
      `added: ${added.length > 0 ? added.join(", ") : "-"}`,
      `removed: ${removed.length > 0 ? removed.join(", ") : "-"}`
    ]
    versionDiffText.value = lines.join("\n")
  } catch {
    versionDiffText.value = "版本内容不是有效 JSON"
  }
}

function getNodeStatusColor(node: VfNode): string {
  const status = (node.data as Record<string, unknown>)?.__status as string
  if (status === 'running') return '#1677ff'
  if (status === 'success') return '#52c41a'
  if (status === 'failed') return '#ff4d4f'
  if (status === 'interrupted') return '#faad14'
  return '#374151'
}

function applySnapshot(snapshot: CanvasSchema) {
  applyCanvasToVueFlow(snapshot)
  isDirty.value = true
}

function deleteSelectedNode() {
  if (!selectedNode.value) {
    return
  }
  const key = selectedNode.value.key
  vfNodes.value = vfNodes.value.filter((node) => node.id !== key)
  vfEdges.value = vfEdges.value.filter((edge) => edge.source !== key && edge.target !== key)
  selectedNode.value = null
  markDirtyAndSnapshot()
}

function handleKeyboardShortcuts(event: KeyboardEvent) {
  const ctrlOrMeta = event.ctrlKey || event.metaKey
  if (ctrlOrMeta && event.key.toLowerCase() === "c" && selectedNode.value) {
    copiedNode.value = cloneNodeSchema(selectedNode.value)
    event.preventDefault()
    return
  }
  if (ctrlOrMeta && event.key.toLowerCase() === "v" && copiedNode.value) {
    const source = copiedNode.value
    const newKey = `${source.type.toLowerCase()}_${Date.now()}`
    const newNode: VfNode = {
      id: newKey,
      type: source.type,
      position: { x: source.layout.x + 28, y: source.layout.y + 28 },
      data: {
        title: `${source.title}_copy`,
        configs: JSON.parse(JSON.stringify(source.configs)),
        inputMappings: JSON.parse(JSON.stringify(source.inputMappings)),
        nodeType: source.type,
        __status: ""
      }
    }
    vfNodes.value = vfNodes.value.concat(newNode)
    markDirtyAndSnapshot()
    event.preventDefault()
    return
  }
  if (ctrlOrMeta && event.key.toLowerCase() === "z") {
    if (historyIndex.value > 0) {
      historyIndex.value -= 1
      const snapshot = historySnapshots.value[historyIndex.value]
      if (snapshot) {
        applySnapshot(snapshot)
      }
    }
    event.preventDefault()
    return
  }
  if (ctrlOrMeta && event.key.toLowerCase() === "y") {
    if (historyIndex.value < historySnapshots.value.length - 1) {
      historyIndex.value += 1
      const snapshot = historySnapshots.value[historyIndex.value]
      if (snapshot) {
        applySnapshot(snapshot)
      }
    }
    event.preventDefault()
    return
  }
  if ((event.key === "Delete" || event.key === "Backspace") && selectedNode.value) {
    deleteSelectedNode()
    event.preventDefault()
  }
}

onMounted(() => {
  loadWorkflow()
  loadNodeTypes()
  window.addEventListener("keydown", handleKeyboardShortcuts)
})

onUnmounted(() => {
  window.removeEventListener("keydown", handleKeyboardShortcuts)
})
</script>

<style scoped>
.workflow-editor-page {
  display: grid;
  grid-template-rows: auto minmax(0, 1fr);
  height: 100%;
  min-height: 0;
  background: #0d1117;
  overflow: hidden;
}

.editor-body {
  display: grid;
  grid-auto-flow: column;
  grid-auto-columns: max-content;
  grid-template-columns: max-content minmax(0, 1fr) max-content;
  min-height: 0;
  overflow: hidden;
}

.canvas-container {
  flex: 1;
  min-width: 0;
  position: relative;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.workflow-canvas {
  width: 100%;
  height: calc(100% - 170px);
}

.debug-panel {
  height: 170px;
  border-top: 1px solid #30363d;
  background: #111820;
}

.debug-panel-header {
  height: 32px;
  padding: 0 8px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  color: #8f99a6;
  font-size: 12px;
  border-bottom: 1px solid #21262d;
}

.debug-panel-body {
  height: calc(100% - 32px);
  overflow-y: auto;
  padding: 6px 8px;
}

.debug-line {
  color: #c9d1d9;
  font-family: Consolas, "Courier New", monospace;
  font-size: 12px;
  line-height: 18px;
}

.debug-empty {
  color: #6b7280;
  font-size: 12px;
  padding-top: 8px;
}

:deep(.vue-flow__background) {
  background: #0d1117;
}

:deep(.vue-flow__edge-path) {
  stroke: #4b5563;
  stroke-width: 2;
}

:deep(.vue-flow__edge.selected .vue-flow__edge-path) {
  stroke: #388bfd;
}

:deep(.vue-flow__controls) {
  background: #161b22;
  border: 1px solid #30363d;
  border-radius: 8px;
  overflow: hidden;
}

:deep(.vue-flow__controls-button) {
  background: #161b22;
  border-color: #30363d;
  color: #e6edf3;
}

:deep(.vue-flow__controls-button:hover) {
  background: #1f2937;
}

:deep(.vue-flow__minimap) {
  border-radius: 8px;
  overflow: hidden;
}

.version-title {
  font-weight: 600;
  color: #dbe4ef;
}

.version-meta {
  margin-top: 4px;
  color: #8f99a6;
  font-size: 12px;
}

@media (max-width: 1366px) {
  :deep(.node-panel) {
    width: 196px;
  }

  :deep(.properties-panel),
  :deep(.test-run-panel) {
    width: 300px;
  }
}

@media (max-width: 1200px) {
  :deep(.node-panel) {
    width: 176px;
  }

  :deep(.properties-panel),
  :deep(.test-run-panel) {
    width: 280px;
  }
}
</style>
