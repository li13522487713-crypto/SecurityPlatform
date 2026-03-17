<template>
  <div class="workflow-editor-page">
    <!-- 顶部工具栏 -->
    <div class="editor-header">
      <div class="header-left">
        <a-button type="text" @click="$router.push('/workflow')" style="color:#fff">
          <LeftOutlined />
        </a-button>
        <a-input
          v-model:value="workflowName"
          class="name-input"
          @blur="handleNameBlur"
          @pressEnter="handleNameBlur"
        />
      </div>
      <div class="header-right">
        <a-space>
          <a-tag v-if="isDirty" color="orange">未保存</a-tag>
          <a-button @click="handleSaveDraft" :loading="saving">保存草稿</a-button>
          <a-button type="primary" @click="showPublishModal = true">发布</a-button>
          <a-button :type="showTestPanel ? 'primary' : 'default'" @click="showTestPanel = !showTestPanel">
            <PlayCircleOutlined />
            测试运行
          </a-button>
        </a-space>
      </div>
    </div>

    <div class="editor-body">
      <!-- 左侧节点面板 -->
      <NodePanel @drag-start="handleNodeDragStart" />

      <!-- 中央 Vue Flow 画布 -->
      <div class="canvas-container" @dragover.prevent @drop="handleDrop">
        <VueFlow
          v-model:nodes="vfNodes"
          v-model:edges="vfEdges"
          :node-types="nodeTypes"
          :default-edge-options="defaultEdgeOptions"
          :connection-mode="ConnectionMode.Loose"
          :fit-view-on-init="true"
          @node-click="handleNodeClick"
          @pane-click="handlePaneClick"
          @connect="handleConnect"
          @nodes-change="handleNodesChange"
          @edges-change="handleEdgesChange"
          class="workflow-canvas"
        >
          <Controls />
          <MiniMap
            :node-color="getNodeStatusColor"
            style="background: #0d1117; border: 1px solid #21262d;"
          />
        </VueFlow>
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
        @close="showTestPanel = false"
        @publish="showPublishModal = true"
        @node-status-update="handleNodeStatusUpdate"
      />
    </div>

    <!-- 发布弹窗 -->
    <a-modal
      v-model:open="showPublishModal"
      title="发布工作流"
      @ok="handlePublish"
      :confirm-loading="publishing"
      ok-text="确认发布"
      cancel-text="取消"
    >
      <a-form layout="vertical">
        <a-form-item label="变更说明">
          <a-textarea v-model:value="changeLog" :rows="3" placeholder="描述本次发布的更新内容" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, markRaw } from 'vue'
import { useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  LeftOutlined,
  PlayCircleOutlined,
} from '@ant-design/icons-vue'
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

import NodePanel from '@/components/workflow/panels/NodePanel.vue'
import PropertiesPanel from '@/components/workflow/panels/PropertiesPanel.vue'
import TestRunPanel from '@/components/workflow/panels/TestRunPanel.vue'
import WorkflowNodeRenderer from '@/components/workflow/nodes/WorkflowNodeRenderer.vue'

import { workflowV2Api } from '@/services/api-workflow-v2'
import type {
  CanvasSchema,
  NodeSchema,
  ConnectionSchema,
  NodeTypeMetadata,
  WorkflowVersionItem,
} from '@/types/workflow-v2'

const route = useRoute()
const workflowId = computed(() => Number(route.params.id))

const workflowName = ref('未命名工作流')
const isDirty = ref(false)
const saving = ref(false)
const publishing = ref(false)
const showPublishModal = ref(false)
const showTestPanel = ref(false)
const changeLog = ref('')
const selectedNode = ref<NodeSchema | null>(null)
const nodeTypesMetadata = ref<NodeTypeMetadata[]>([])
const workflowVersions = ref<WorkflowVersionItem[]>([])

const vfNodes = ref<VfNode[]>([])
const vfEdges = ref<VfEdge[]>([])

// 节点执行状态（key → status string）
const nodeRunStatus = ref<Record<string, string>>({})

const _nr = markRaw(WorkflowNodeRenderer)
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const nodeTypes: Record<string, any> = {
  Entry: _nr, Exit: _nr, LLM: _nr, If: _nr, Loop: _nr, Break: _nr, Continue: _nr,
  Batch: _nr, SubWorkflow: _nr, IntentDetector: _nr, KnowledgeRetriever: _nr,
  KnowledgeIndexer: _nr, KnowledgeDeleter: _nr, CodeRunner: _nr, HttpRequester: _nr,
  PluginApi: _nr, DatabaseQuery: _nr, DatabaseInsert: _nr, DatabaseUpdate: _nr,
  DatabaseDelete: _nr, AssignVariable: _nr, VariableAggregator: _nr, JsonSerialization: _nr,
  JsonDeserialization: _nr, TextProcessor: _nr, MessageList: _nr, CreateMessage: _nr,
  ConversationList: _nr, QuestionAnswer: _nr, OutputEmitter: _nr,
}

const defaultEdgeOptions = {
  type: 'smoothstep',
  animated: false,
  style: { stroke: '#4b5563', strokeWidth: 2 },
}

let draggingNodeType: string | null = null

const currentCanvas = computed<CanvasSchema>(() => {
  const nodes: NodeSchema[] = vfNodes.value.map(n => ({
    key: n.id,
    type: (n.type ?? 'Unknown') as NodeSchema['type'],
    title: (n.data?.title as string) ?? n.type ?? '',
    layout: { x: n.position.x, y: n.position.y },
    configs: (n.data?.configs as Record<string, unknown>) ?? {},
    inputMappings: (n.data?.inputMappings as Record<string, string>) ?? {},
  }))

  const connections: ConnectionSchema[] = vfEdges.value.map((e, i) => ({
    fromNode: e.source,
    fromPort: e.sourceHandle ?? undefined,
    toNode: e.target,
    toPort: e.targetHandle ?? undefined,
  }))

  return { nodes, connections }
})

async function loadWorkflow() {
  try {
    const res = await workflowV2Api.getDetail(workflowId.value)
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
    const vRes = await workflowV2Api.getVersions(workflowId.value)
    if (vRes.success && vRes.data) {
      workflowVersions.value = vRes.data
    }
  } catch (e) {
    initDefaultCanvas()
  }
}

async function loadNodeTypes() {
  try {
    const res = await workflowV2Api.getNodeTypes()
    if (res.success && res.data) {
      nodeTypesMetadata.value = res.data
    }
  } catch {
    // ignore
  }
}

function applyCanvasToVueFlow(canvas: CanvasSchema) {
  vfNodes.value = canvas.nodes.map(n => ({
    id: n.key,
    type: n.type,
    position: { x: n.layout?.x ?? 0, y: n.layout?.y ?? 0 },
    data: {
      title: n.title,
      configs: n.configs,
      inputMappings: n.inputMappings,
      nodeType: n.type,
      __status: nodeRunStatus.value[n.key] ?? '',
    },
  }))

  vfEdges.value = canvas.connections.map((c, i) => ({
    id: `e-${c.fromNode}-${c.toNode}-${i}`,
    source: c.fromNode,
    sourceHandle: c.fromPort,
    target: c.toNode,
    targetHandle: c.toPort,
    type: 'smoothstep',
    style: { stroke: '#4b5563', strokeWidth: 2 },
  }))

  isDirty.value = false
}

function initDefaultCanvas() {
  vfNodes.value = [
    {
      id: 'entry_1',
      type: 'Entry',
      position: { x: 100, y: 200 },
      data: { title: '开始', configs: {}, inputMappings: {}, nodeType: 'Entry', __status: '' },
    },
    {
      id: 'exit_1',
      type: 'Exit',
      position: { x: 600, y: 200 },
      data: { title: '结束', configs: {}, inputMappings: {}, nodeType: 'Exit', __status: '' },
    },
  ]
  vfEdges.value = [
    {
      id: 'e-entry-exit',
      source: 'entry_1',
      target: 'exit_1',
      type: 'smoothstep',
      style: { stroke: '#4b5563', strokeWidth: 2 },
    },
  ]
  isDirty.value = false
}

async function handleSaveDraft() {
  saving.value = true
  try {
    const canvasJson = JSON.stringify(currentCanvas.value)
    const res = await workflowV2Api.saveDraft(workflowId.value, { canvasJson })
    if (res.success) {
      isDirty.value = false
      message.success('草稿已保存')
    }
  } finally {
    saving.value = false
  }
}

async function handlePublish() {
  publishing.value = true
  try {
    const canvasJson = JSON.stringify(currentCanvas.value)
    await workflowV2Api.saveDraft(workflowId.value, { canvasJson })

    const res = await workflowV2Api.publish(workflowId.value, { changeLog: changeLog.value })
    if (res.success) {
      showPublishModal.value = false
      message.success(`已发布版本 ${res.data?.version}`)
      changeLog.value = ''
      // 刷新版本列表
      const vRes = await workflowV2Api.getVersions(workflowId.value)
      if (vRes.success && vRes.data) {
        workflowVersions.value = vRes.data
      }
    }
  } finally {
    publishing.value = false
  }
}

async function handleNameBlur() {
  if (!workflowName.value.trim()) return
  await workflowV2Api.updateMeta(workflowId.value, {
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
    layout: { x: vfNode.position.x, y: vfNode.position.y },
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
    type: 'smoothstep',
    style: { stroke: '#4b5563', strokeWidth: 2 },
  }
  vfEdges.value = [...vfEdges.value, newEdge]
  isDirty.value = true
}

function handleNodesChange(changes: NodeChange[]) {
  if (changes.some(c => c.type !== 'select' && c.type !== 'dimensions')) {
    isDirty.value = true
  }
}

function handleEdgesChange(changes: EdgeChange[]) {
  if (changes.some(c => c.type !== 'select')) {
    isDirty.value = true
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

  const meta = nodeTypesMetadata.value.find(m => nodeTypeEnumToString(m.type) === draggingNodeType)
  const title = meta?.name ?? draggingNodeType

  vfNodes.value = [
    ...vfNodes.value,
    {
      id: nodeKey,
      type: draggingNodeType,
      position: { x, y },
      data: { title, configs: {}, inputMappings: {}, nodeType: draggingNodeType, __status: '' },
    },
  ]

  isDirty.value = true
  draggingNodeType = null
}

function nodeTypeEnumToString(type: number): string {
  const map: Record<number, string> = {
    1: 'Entry', 2: 'Exit', 3: 'LLM', 4: 'If', 5: 'Loop', 6: 'Break', 7: 'Continue',
    8: 'Batch', 9: 'SubWorkflow', 10: 'IntentDetector', 11: 'KnowledgeRetriever',
    12: 'KnowledgeIndexer', 13: 'KnowledgeDeleter', 14: 'CodeRunner', 15: 'HttpRequester',
    16: 'PluginApi', 17: 'DatabaseQuery', 18: 'DatabaseInsert', 19: 'DatabaseUpdate',
    20: 'DatabaseDelete', 21: 'AssignVariable', 22: 'VariableAggregator', 23: 'JsonSerialization',
    24: 'JsonDeserialization', 25: 'TextProcessor', 26: 'MessageList', 27: 'CreateMessage',
    28: 'ConversationList', 29: 'QuestionAnswer', 30: 'OutputEmitter',
  }
  return map[type] ?? 'Unknown'
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
    isDirty.value = true
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
}

function getNodeStatusColor(node: VfNode): string {
  const status = (node.data as Record<string, unknown>)?.__status as string
  if (status === 'running') return '#1677ff'
  if (status === 'success') return '#52c41a'
  if (status === 'failed') return '#ff4d4f'
  if (status === 'interrupted') return '#faad14'
  return '#374151'
}

onMounted(() => {
  loadWorkflow()
  loadNodeTypes()
})
</script>

<style scoped>
.workflow-editor-page {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background: #0d1117;
  overflow: hidden;
}

.editor-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 52px;
  padding: 0 12px;
  background: #161b22;
  border-bottom: 1px solid #30363d;
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.name-input {
  background: transparent;
  border: none;
  color: #e6edf3;
  font-size: 15px;
  font-weight: 600;
  width: 240px;
  box-shadow: none;
}

.name-input:focus,
:deep(.name-input input:focus) {
  background: rgba(255,255,255,0.05);
  box-shadow: none;
  border-color: #388bfd !important;
}

.editor-body {
  display: flex;
  flex: 1;
  overflow: hidden;
}

.canvas-container {
  flex: 1;
  position: relative;
  overflow: hidden;
}

.workflow-canvas {
  width: 100%;
  height: 100%;
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
</style>
