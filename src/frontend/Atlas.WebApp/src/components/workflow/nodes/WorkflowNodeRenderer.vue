<template>
  <div
    class="workflow-node"
    :class="[`node-type-${nodeTypeLower}`, statusClass]"
    :style="{ borderColor: nodeColor }"
  >
    <Handle type="target" :position="Position.Left" class="node-handle" />

    <div class="node-header" :style="{ background: nodeColor }">
      <span class="node-icon">{{ nodeIcon }}</span>
      <span class="node-title">{{ data.title || nodeTypeName }}</span>
    </div>

    <div v-if="statusIcon" class="node-status-icon">
      <LoadingOutlined v-if="props.data.__status === 'running'" spin />
      <CheckCircleFilled v-else-if="props.data.__status === 'success'" style="color:#52c41a" />
      <CloseCircleFilled v-else-if="props.data.__status === 'failed'" style="color:#ff4d4f" />
      <ExclamationCircleFilled v-else-if="props.data.__status === 'interrupted'" style="color:#faad14" />
    </div>

    <!-- Selector: two output handles -->
    <template v-if="nodeType === 'Selector'">
      <Handle type="source" :position="Position.Right" id="true" class="node-handle handle-true">
        <span class="handle-label">{{ t("workflowUi.handleTrue") }}</span>
      </Handle>
      <Handle type="source" :position="Position.Right" id="false" class="node-handle handle-false">
        <span class="handle-label">{{ t("workflowUi.handleFalse") }}</span>
      </Handle>
    </template>
    <!-- Loop: two output handles -->
    <template v-else-if="nodeType === 'Loop'">
      <Handle type="source" :position="Position.Right" id="body" class="node-handle handle-loop-body">
        <span class="handle-label">{{ t("workflowUi.handleBody") }}</span>
      </Handle>
      <Handle type="source" :position="Position.Right" id="done" class="node-handle handle-loop-done">
        <span class="handle-label">{{ t("workflowUi.handleDone") }}</span>
      </Handle>
    </template>
    <!-- Default: single output -->
    <template v-else>
      <Handle type="source" :position="Position.Right" class="node-handle" />
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Handle, Position } from '@vue-flow/core'
import {
  LoadingOutlined,
  CheckCircleFilled,
  CloseCircleFilled,
  ExclamationCircleFilled,
} from '@ant-design/icons-vue'

interface NodeData {
  title?: string
  configs?: Record<string, unknown>
  inputMappings?: Record<string, string>
  nodeType?: string
  __status?: string
  __costMs?: number
}

const props = defineProps<{ data: NodeData }>()
const { t } = useI18n()

const KNOWN_NODE_TYPES = new Set([
  "Entry", "Exit", "Llm", "Selector", "Loop", "SubWorkflow", "HttpRequester", "CodeRunner", "DatabaseQuery",
  "AssignVariable", "VariableAggregator", "JsonSerialization", "JsonDeserialization", "TextProcessor", "LLM", "If"
])

const nodeType = computed(() => props.data.nodeType ?? 'Unknown')
const nodeTypeLower = computed(() => nodeType.value.toLowerCase())

const NODE_COLORS: Record<string, string> = {
  Entry: '#52c41a',
  Exit: '#ff4d4f',
  Llm: '#6366f1',
  Selector: '#f59e0b',
  Loop: '#f59e0b',
  SubWorkflow: '#8b5cf6',
  CodeRunner: '#10b981',
  HttpRequester: '#10b981',
  DatabaseQuery: '#3b82f6',
  AssignVariable: '#d946ef',
  VariableAggregator: '#d946ef',
  JsonSerialization: '#84cc16',
  JsonDeserialization: '#84cc16',
  TextProcessor: '#e2e8f0',
  LLM: '#6366f1',
  If: '#f59e0b',
}

const NODE_ICONS: Record<string, string> = {
  Entry: '▶',
  Exit: '⏹',
  Llm: '🤖',
  Selector: '⟟',
  Loop: '↻',
  SubWorkflow: '⊞',
  CodeRunner: '⌨',
  HttpRequester: '🌐',
  DatabaseQuery: '🔍',
  AssignVariable: '=',
  VariableAggregator: '∪',
  JsonSerialization: '{}',
  JsonDeserialization: '{}',
  TextProcessor: 'T',
  LLM: '🤖',
  If: '⟟',
}

const nodeColor = computed(() => NODE_COLORS[nodeType.value] ?? '#6b7280')
const nodeIcon = computed(() => NODE_ICONS[nodeType.value] ?? '□')
const nodeTypeName = computed(() => {
  const k = nodeType.value
  if (KNOWN_NODE_TYPES.has(k)) {
    return t(`wfUi.nodeTypes.${k}`)
  }
  return k
})

const statusClass = computed(() => {
  const s = props.data.__status
  if (s === 'running') return 'status-running'
  if (s === 'success') return 'status-success'
  if (s === 'failed') return 'status-failed'
  if (s === 'interrupted') return 'status-interrupted'
  return ''
})

const statusIcon = computed(() => !!props.data.__status && props.data.__status !== 'idle')
</script>

<style scoped>
.workflow-node {
  position: relative;
  min-width: 160px;
  border-radius: 8px;
  border: 2px solid #374151;
  background: #1f2937;
  box-shadow: 0 2px 8px rgba(0,0,0,0.4);
  cursor: pointer;
  transition: border-color 0.2s, box-shadow 0.2s;
  font-size: 13px;
  color: #e5e7eb;
}

.workflow-node:hover {
  box-shadow: 0 4px 16px rgba(0,0,0,0.6);
}

.node-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border-radius: 6px 6px 0 0;
  font-weight: 600;
  color: #fff;
}

.node-icon {
  font-size: 16px;
  flex-shrink: 0;
}

.node-title {
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.node-status-icon {
  position: absolute;
  top: 4px;
  right: 4px;
  font-size: 16px;
}

.node-handle {
  width: 10px;
  height: 10px;
  border: 2px solid #fff;
  background: #374151;
}

.handle-true {
  top: 30%;
}

.handle-false {
  top: 70%;
}

.handle-loop-body {
  top: 30%;
}

.handle-loop-done {
  top: 70%;
}

.handle-label {
  position: absolute;
  right: 14px;
  font-size: 10px;
  color: #9ca3af;
  white-space: nowrap;
}

.status-running {
  border-color: #1677ff !important;
  animation: pulse-blue 1.5s infinite;
}

.status-success {
  border-color: #52c41a !important;
}

.status-failed {
  border-color: #ff4d4f !important;
}

.status-interrupted {
  border-color: #faad14 !important;
}

@keyframes pulse-blue {
  0%, 100% { box-shadow: 0 0 0 0 rgba(22, 119, 255, 0.4); }
  50% { box-shadow: 0 0 0 6px rgba(22, 119, 255, 0); }
}
</style>
