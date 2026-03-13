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

    <!-- If 节点：两个输出端口 -->
    <template v-if="nodeType === 'If'">
      <Handle type="source" :position="Position.Right" id="true" class="node-handle handle-true">
        <span class="handle-label">True</span>
      </Handle>
      <Handle type="source" :position="Position.Right" id="false" class="node-handle handle-false">
        <span class="handle-label">False</span>
      </Handle>
    </template>
    <!-- Loop 节点：两个输出端口 -->
    <template v-else-if="nodeType === 'Loop'">
      <Handle type="source" :position="Position.Right" id="body" class="node-handle handle-loop-body">
        <span class="handle-label">Body</span>
      </Handle>
      <Handle type="source" :position="Position.Right" id="done" class="node-handle handle-loop-done">
        <span class="handle-label">Done</span>
      </Handle>
    </template>
    <!-- 默认：单个输出端口 -->
    <template v-else>
      <Handle type="source" :position="Position.Right" class="node-handle" />
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
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

const nodeType = computed(() => props.data.nodeType ?? 'Unknown')
const nodeTypeLower = computed(() => nodeType.value.toLowerCase())

const NODE_COLORS: Record<string, string> = {
  Entry: '#52c41a',
  Exit: '#ff4d4f',
  LLM: '#6366f1',
  If: '#f59e0b',
  Loop: '#f59e0b',
  Break: '#f59e0b',
  Continue: '#f59e0b',
  Batch: '#f59e0b',
  SubWorkflow: '#8b5cf6',
  IntentDetector: '#6366f1',
  KnowledgeRetriever: '#06b6d4',
  KnowledgeIndexer: '#06b6d4',
  KnowledgeDeleter: '#06b6d4',
  CodeRunner: '#10b981',
  HttpRequester: '#10b981',
  PluginApi: '#10b981',
  DatabaseQuery: '#3b82f6',
  DatabaseInsert: '#3b82f6',
  DatabaseUpdate: '#3b82f6',
  DatabaseDelete: '#3b82f6',
  AssignVariable: '#d946ef',
  VariableAggregator: '#d946ef',
  JsonSerialization: '#84cc16',
  JsonDeserialization: '#84cc16',
  TextProcessor: '#e2e8f0',
  MessageList: '#f97316',
  CreateMessage: '#f97316',
  ConversationList: '#f97316',
  QuestionAnswer: '#ec4899',
  OutputEmitter: '#52c41a',
}

const NODE_ICONS: Record<string, string> = {
  Entry: '▶',
  Exit: '⏹',
  LLM: '🤖',
  If: '⟟',
  Loop: '↻',
  Break: '⏏',
  Continue: '⏩',
  Batch: '≡',
  SubWorkflow: '⊞',
  IntentDetector: '🎯',
  KnowledgeRetriever: '📖',
  KnowledgeIndexer: '📝',
  KnowledgeDeleter: '🗑',
  CodeRunner: '⌨',
  HttpRequester: '🌐',
  PluginApi: '🔌',
  DatabaseQuery: '🔍',
  DatabaseInsert: '➕',
  DatabaseUpdate: '✏',
  DatabaseDelete: '🗑',
  AssignVariable: '=',
  VariableAggregator: '∪',
  JsonSerialization: '{}',
  JsonDeserialization: '{}',
  TextProcessor: 'T',
  MessageList: '💬',
  CreateMessage: '✉',
  ConversationList: '📋',
  QuestionAnswer: '❓',
  OutputEmitter: '📤',
}

const NODE_NAMES: Record<string, string> = {
  Entry: '开始', Exit: '结束', LLM: '大模型', If: '条件判断',
  Loop: '循环', Break: 'Break', Continue: 'Continue', Batch: '批处理',
  SubWorkflow: '子流程', IntentDetector: '意图识别', KnowledgeRetriever: '知识库检索',
  KnowledgeIndexer: '知识库写入', KnowledgeDeleter: '知识库删除', CodeRunner: '代码执行',
  HttpRequester: 'HTTP请求', PluginApi: '插件调用', DatabaseQuery: '数据库查询',
  DatabaseInsert: '数据库插入', DatabaseUpdate: '数据库更新', DatabaseDelete: '数据库删除',
  AssignVariable: '变量赋值', VariableAggregator: '变量聚合', JsonSerialization: 'JSON序列化',
  JsonDeserialization: 'JSON反序列化', TextProcessor: '文本处理', MessageList: '消息列表',
  CreateMessage: '创建消息', ConversationList: '会话列表', QuestionAnswer: '提问等待',
  OutputEmitter: '流式输出',
}

const nodeColor = computed(() => NODE_COLORS[nodeType.value] ?? '#6b7280')
const nodeIcon = computed(() => NODE_ICONS[nodeType.value] ?? '□')
const nodeTypeName = computed(() => NODE_NAMES[nodeType.value] ?? nodeType.value)

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
