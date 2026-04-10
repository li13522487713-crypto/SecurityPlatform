<template>
  <div
    class="workflow-node"
    :class="[`node-type-${nodeTypeLower}`, statusClass]"
    :style="{ '--node-color': nodeColor, '--node-soft': `${nodeColor}1A` }"
  >
    <Handle type="target" :position="Position.Left" class="node-handle node-handle-in" />

    <div class="node-header">
      <span class="node-icon" v-html="nodeIconSvg"></span>
      <span class="node-title">{{ data.title || nodeTypeName }}</span>
      <span class="node-menu">...</span>
    </div>

    <div v-if="statusIcon" class="node-status-icon">
      <LoadingOutlined v-if="props.data.__status === 'running'" spin />
      <CheckCircleFilled v-else-if="props.data.__status === 'success'" style="color:#52c41a" />
      <CloseCircleFilled v-else-if="props.data.__status === 'failed'" style="color:#ff4d4f" />
      <ExclamationCircleFilled v-else-if="props.data.__status === 'interrupted'" style="color:#faad14" />
    </div>

    <div class="node-content">
      <div v-for="item in previewItems" :key="item.label" class="node-preview-row">
        <span class="row-label">{{ item.label }}</span>
        <span class="row-value">{{ item.value }}</span>
      </div>
      <div v-if="llmPreview" class="llm-preview">{{ llmPreview }}</div>
    </div>

    <!-- Selector: two output handles -->
    <template v-if="nodeType === 'Selector'">
      <Handle id="true" type="source" :position="Position.Right" class="node-handle node-handle-out handle-true">
        <span class="handle-label">{{ t("workflowUi.handleTrue") }}</span>
      </Handle>
      <Handle id="false" type="source" :position="Position.Right" class="node-handle node-handle-out handle-false">
        <span class="handle-label">{{ t("workflowUi.handleFalse") }}</span>
      </Handle>
    </template>
    <!-- Loop: two output handles -->
    <template v-else-if="nodeType === 'Loop'">
      <Handle id="body" type="source" :position="Position.Right" class="node-handle node-handle-out handle-loop-body">
        <span class="handle-label">{{ t("workflowUi.handleBody") }}</span>
      </Handle>
      <Handle id="done" type="source" :position="Position.Right" class="node-handle node-handle-out handle-loop-done">
        <span class="handle-label">{{ t("workflowUi.handleDone") }}</span>
      </Handle>
    </template>
    <!-- Batch: items/done output handles -->
    <template v-else-if="nodeType === 'Batch'">
      <Handle id="item" type="source" :position="Position.Right" class="node-handle node-handle-out handle-loop-body">
        <span class="handle-label">item</span>
      </Handle>
      <Handle id="done" type="source" :position="Position.Right" class="node-handle node-handle-out handle-loop-done">
        <span class="handle-label">{{ t("workflowUi.handleDone") }}</span>
      </Handle>
    </template>
    <!-- Default: single output -->
    <template v-else>
      <Handle type="source" :position="Position.Right" class="node-handle node-handle-out" />
    </template>

    <div v-if="statusMeta" class="node-status-strip">
      <span>{{ statusMeta }}</span>
    </div>
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
  __llmOutput?: string
}

const props = defineProps<{ data: NodeData }>()
const { t } = useI18n()

const KNOWN_NODE_TYPES = new Set([
  "Entry", "Exit", "Llm", "Agent", "Plugin", "IntentDetector", "QuestionAnswer", "Selector", "Loop", "Batch", "Break", "Continue",
  "SubWorkflow", "HttpRequester", "CodeRunner", "DatabaseQuery", "DatabaseInsert", "DatabaseUpdate", "DatabaseDelete", "DatabaseCustomSql",
  "AssignVariable", "VariableAssignerWithinLoop", "VariableAggregator", "JsonSerialization", "JsonDeserialization", "TextProcessor",
  "KnowledgeRetriever", "KnowledgeIndexer", "KnowledgeDeleter", "Ltm",
  "CreateConversation", "ConversationList", "ConversationUpdate", "ConversationDelete", "ConversationHistory", "ClearConversationHistory",
  "MessageList", "CreateMessage", "EditMessage", "DeleteMessage", "InputReceiver", "OutputEmitter", "Comment",
  "LLM", "If"
])

const nodeType = computed(() => props.data.nodeType ?? 'Unknown')
const nodeTypeLower = computed(() => nodeType.value.toLowerCase())

const NODE_COLORS: Record<string, string> = {
  Entry: '#6366F1',
  Exit: '#6366F1',
  Selector: '#6366F1',
  Loop: '#6366F1',
  Batch: '#6366F1',
  Break: '#6366F1',
  Continue: '#6366F1',
  If: '#6366F1',
  Llm: '#8B5CF6',
  LLM: '#8B5CF6',
  IntentDetector: '#8B5CF6',
  QuestionAnswer: '#8B5CF6',
  Agent: '#8B5CF6',
  CodeRunner: '#06B6D4',
  TextProcessor: '#06B6D4',
  JsonSerialization: '#06B6D4',
  JsonDeserialization: '#06B6D4',
  VariableAggregator: '#06B6D4',
  AssignVariable: '#06B6D4',
  VariableAssignerWithinLoop: '#06B6D4',
  Plugin: '#F59E0B',
  HttpRequester: '#F59E0B',
  SubWorkflow: '#F59E0B',
  KnowledgeRetriever: '#10B981',
  KnowledgeIndexer: '#10B981',
  KnowledgeDeleter: '#10B981',
  Ltm: '#10B981',
  DatabaseQuery: '#3B82F6',
  DatabaseInsert: '#3B82F6',
  DatabaseUpdate: '#3B82F6',
  DatabaseDelete: '#3B82F6',
  DatabaseCustomSql: '#3B82F6',
  CreateConversation: '#EC4899',
  ConversationList: '#EC4899',
  ConversationUpdate: '#EC4899',
  ConversationDelete: '#EC4899',
  ConversationHistory: '#EC4899',
  ClearConversationHistory: '#EC4899',
  MessageList: '#EC4899',
  CreateMessage: '#EC4899',
  EditMessage: '#EC4899',
  DeleteMessage: '#EC4899',
  InputReceiver: '#EF4444',
  OutputEmitter: '#EF4444',
  Comment: '#64748B'
}

const NODE_ICON_KEY: Record<string, string> = {
  Entry: "ST", Exit: "ED", Selector: "IF", Loop: "LP", Batch: "BT", Break: "BR", Continue: "CT",
  Llm: "AI", IntentDetector: "ID", QuestionAnswer: "QA", Agent: "AG", CodeRunner: "CD", TextProcessor: "TX",
  JsonSerialization: "JS", JsonDeserialization: "JD", VariableAggregator: "VA", AssignVariable: "SV", VariableAssignerWithinLoop: "SV",
  Plugin: "PL", HttpRequester: "HT", SubWorkflow: "SW", KnowledgeRetriever: "KR", KnowledgeIndexer: "KW", KnowledgeDeleter: "KD",
  Ltm: "LT", DatabaseQuery: "DQ", DatabaseInsert: "DI", DatabaseUpdate: "DU", DatabaseDelete: "DD", DatabaseCustomSql: "SQ",
  CreateConversation: "CC", ConversationList: "CL", ConversationUpdate: "CU", ConversationDelete: "CD", ConversationHistory: "CH",
  ClearConversationHistory: "CX", MessageList: "ML", CreateMessage: "CM", EditMessage: "EM", DeleteMessage: "DM",
  InputReceiver: "IN", OutputEmitter: "OU", Comment: "CM", LLM: "AI", If: "IF"
}

const nodeColor = computed(() => NODE_COLORS[nodeType.value] ?? '#6b7280')
const nodeIconSvg = computed(() => createNodeIconSvg(NODE_ICON_KEY[nodeType.value] ?? "WF", nodeColor.value))
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
  if (s === 'skipped') return 'status-skipped'
  return ''
})

const statusIcon = computed(() => !!props.data.__status && props.data.__status !== 'idle')

const previewItems = computed(() => {
  const cfg = props.data.configs ?? {}
  const firstString = (keys: string[]) => {
    for (const key of keys) {
      const value = cfg[key]
      if (typeof value === 'string' && value.trim().length > 0) {
        return value.trim()
      }
    }
    return "-"
  }
  if (nodeType.value === "Llm" || nodeType.value === "LLM") {
    return [
      { label: "模型", value: firstString(["modelName", "model", "providerModel"]) },
      { label: "Prompt", value: firstString(["systemPrompt", "prompt", "userPrompt"]).slice(0, 22) }
    ]
  }
  if (nodeType.value === "HttpRequester") {
    return [
      { label: "Method", value: firstString(["method"]) },
      { label: "URL", value: firstString(["url", "endpoint"]).slice(0, 22) }
    ]
  }
  if (nodeType.value === "Selector" || nodeType.value === "If") {
    return [
      { label: "条件", value: firstString(["expression", "conditionExpression"]).slice(0, 22) },
      { label: "分支", value: "true / false" }
    ]
  }
  return [
    { label: "类型", value: nodeTypeName.value },
    { label: "输入", value: String(Object.keys(props.data.inputMappings ?? {}).length) }
  ]
})

const llmPreview = computed(() => {
  if (nodeType.value !== "Llm" && nodeType.value !== "LLM") {
    return ""
  }
  const raw = props.data.__llmOutput
  if (!raw) {
    return ""
  }
  return raw.slice(0, 60)
})

const statusMeta = computed(() => {
  const costMs = props.data.__costMs
  if (props.data.__status === "running") {
    return "Running..."
  }
  if (props.data.__status && costMs) {
    return `${props.data.__status} ${costMs}ms`
  }
  if (props.data.__status) {
    return props.data.__status
  }
  return ""
})

function createNodeIconSvg(label: string, color: string) {
  return `<svg width="20" height="20" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg"><rect x="1" y="1" width="18" height="18" rx="5" fill="${color}22" stroke="${color}" stroke-width="1.2"/><text x="10" y="12.2" text-anchor="middle" font-size="7.2" fill="${color}" font-family="Inter, Arial, sans-serif" font-weight="700">${label}</text></svg>`
}
</script>

<style scoped>
.workflow-node {
  position: relative;
  width: 320px;
  min-height: 160px;
  border-radius: 8px;
  border: 1px solid #e5e7eb;
  background: #fff;
  box-shadow: 0 8px 18px rgba(2, 6, 23, 0.08);
  cursor: pointer;
  transition: border-color 0.2s, box-shadow 0.2s, transform 0.15s;
  font-size: 13px;
  color: #0f172a;
}

.workflow-node:hover {
  transform: translateY(-1px);
  box-shadow: 0 12px 28px rgba(2, 6, 23, 0.16);
}

.node-header {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border-radius: 6px 6px 0 0;
  font-weight: 600;
  color: #0f172a;
  border-bottom: 1px solid #eef2f7;
  background: linear-gradient(90deg, var(--node-soft), #fff);
}

.node-icon {
  display: flex;
  width: 20px;
  height: 20px;
  flex-shrink: 0;
}

.node-title {
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.node-menu {
  color: #94a3b8;
  letter-spacing: 1px;
}

.node-status-icon {
  position: absolute;
  top: 10px;
  right: 10px;
  font-size: 16px;
}

.node-content {
  padding: 12px;
  display: grid;
  gap: 6px;
}

.node-preview-row {
  display: grid;
  grid-template-columns: 74px minmax(0, 1fr);
  gap: 8px;
}

.row-label {
  font-size: 12px;
  color: #64748b;
  text-align: right;
}

.row-value {
  min-width: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  color: #0f172a;
}

.llm-preview {
  margin-top: 2px;
  padding: 8px 10px;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  font-size: 12px;
  color: #475569;
  background: #f8fafc;
}

.node-handle {
  width: 20px;
  height: 20px;
  border: 0;
  background: transparent;
}

.node-handle::after {
  content: "";
  position: absolute;
  left: 5px;
  top: 5px;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  border: 1px solid #fff;
  background: var(--node-color);
  box-shadow: 0 0 0 1px #cbd5e1;
}

.handle-true,
.handle-loop-body {
  top: 30%;
}

.handle-false,
.handle-loop-done {
  top: 70%;
}

.handle-label {
  position: absolute;
  right: 22px;
  font-size: 10px;
  color: #64748b;
  white-space: nowrap;
}

.node-status-strip {
  margin: 0 12px 12px;
  padding: 5px 8px;
  border-radius: 6px;
  font-size: 11px;
  color: #334155;
  border: 1px solid #e2e8f0;
  background: #f8fafc;
}

.workflow-node:focus-within,
.workflow-node.selected {
  border-color: var(--node-color);
  box-shadow: 0 0 0 2px var(--node-soft);
}

.status-running {
  border-color: #4e40e5 !important;
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

.status-skipped {
  opacity: 0.55;
  border-style: dashed;
}

@keyframes pulse-blue {
  0%, 100% { box-shadow: 0 0 0 0 rgba(78, 64, 229, 0.34); }
  50% { box-shadow: 0 0 0 6px rgba(78, 64, 229, 0); }
}
</style>
