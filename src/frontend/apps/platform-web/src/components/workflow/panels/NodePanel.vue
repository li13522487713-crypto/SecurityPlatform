<template>
  <div class="node-panel">
    <div class="panel-header">
      <a-input
        v-model:value="searchText"
        :placeholder="t('wfUi.nodePanel.phSearch')"
        size="small"
        allow-clear
      >
        <template #prefix><SearchOutlined /></template>
      </a-input>
    </div>

    <div class="panel-body">
      <div v-for="category in filteredCategories" :key="category.name" class="node-category">
        <div class="category-title">{{ category.name }}</div>
        <div class="category-nodes">
          <div
            v-for="node in category.nodes"
            :key="node.type"
            class="node-item"
            draggable="true"
            @dragstart="handleDragStart(node.type)"
          >
            <span class="node-item-icon" :style="{ background: getNodeColor(node.type) }">
              {{ getNodeIcon(node.type) }}
            </span>
            <div class="node-item-info">
              <div class="node-item-name">{{ node.name }}</div>
              <div class="node-item-desc">{{ node.description }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { SearchOutlined } from '@ant-design/icons-vue'

const { t } = useI18n()

const emit = defineEmits<{
  (e: 'drag-start', nodeType: string): void
}>()

const searchText = ref('')

interface NodeItem {
  type: string
  name: string
  description: string
  category: string
}

const NODE_DEFS: ReadonlyArray<{ type: string; desc: string; cat: string }> = [
  { type: 'Entry', desc: 'descEntry', cat: 'catFlowControl' },
  { type: 'Exit', desc: 'descExit', cat: 'catFlowControl' },
  { type: 'Selector', desc: 'descSelector', cat: 'catFlowControl' },
  { type: 'Loop', desc: 'descLoop', cat: 'catFlowControl' },
  { type: 'Batch', desc: 'descBatch', cat: 'catFlowControl' },
  { type: 'Break', desc: 'descBreak', cat: 'catFlowControl' },
  { type: 'Continue', desc: 'descContinue', cat: 'catFlowControl' },
  { type: 'Llm', desc: 'descLlm', cat: 'catAi' },
  { type: 'IntentDetector', desc: 'descIntentDetector', cat: 'catAi' },
  { type: 'QuestionAnswer', desc: 'descQuestionAnswer', cat: 'catAi' },
  { type: 'CodeRunner', desc: 'descCode', cat: 'catDataProcess' },
  { type: 'TextProcessor', desc: 'descText', cat: 'catDataProcess' },
  { type: 'JsonSerialization', desc: 'descSer', cat: 'catDataProcess' },
  { type: 'JsonDeserialization', desc: 'descDe', cat: 'catDataProcess' },
  { type: 'VariableAggregator', desc: 'descAgg', cat: 'catDataProcess' },
  { type: 'AssignVariable', desc: 'descAssign', cat: 'catDataProcess' },
  { type: 'Plugin', desc: 'descPlugin', cat: 'catExternal' },
  { type: 'HttpRequester', desc: 'descHttp', cat: 'catExternal' },
  { type: 'SubWorkflow', desc: 'descSub', cat: 'catExternal' },
  { type: 'KnowledgeRetriever', desc: 'descKnowledgeRetriever', cat: 'catKnowledge' },
  { type: 'KnowledgeIndexer', desc: 'descKnowledgeIndexer', cat: 'catKnowledge' },
  { type: 'Ltm', desc: 'descLtm', cat: 'catKnowledge' },
  { type: 'DatabaseQuery', desc: 'descDb', cat: 'catDatabase' },
  { type: 'DatabaseInsert', desc: 'descDatabaseInsert', cat: 'catDatabase' },
  { type: 'DatabaseUpdate', desc: 'descDatabaseUpdate', cat: 'catDatabase' },
  { type: 'DatabaseDelete', desc: 'descDatabaseDelete', cat: 'catDatabase' },
  { type: 'DatabaseCustomSql', desc: 'descDatabaseCustomSql', cat: 'catDatabase' },
  { type: 'CreateConversation', desc: 'descCreateConversation', cat: 'catConversation' },
  { type: 'ConversationList', desc: 'descConversationList', cat: 'catConversation' },
  { type: 'ConversationUpdate', desc: 'descConversationUpdate', cat: 'catConversation' },
  { type: 'ConversationDelete', desc: 'descConversationDelete', cat: 'catConversation' },
  { type: 'ConversationHistory', desc: 'descConversationHistory', cat: 'catConversation' },
  { type: 'ClearConversationHistory', desc: 'descClearConversationHistory', cat: 'catConversation' },
  { type: 'MessageList', desc: 'descMessageList', cat: 'catConversation' },
  { type: 'CreateMessage', desc: 'descCreateMessage', cat: 'catConversation' },
  { type: 'EditMessage', desc: 'descEditMessage', cat: 'catConversation' },
  { type: 'DeleteMessage', desc: 'descDeleteMessage', cat: 'catConversation' },
  { type: 'InputReceiver', desc: 'descInputReceiver', cat: 'catConversation' },
  { type: 'OutputEmitter', desc: 'descOutputEmitter', cat: 'catConversation' },
]

function tr(path: string, fallback: string) {
  const translated = t(path)
  return translated === path ? fallback : translated
}

const allNodes = computed<NodeItem[]>(() =>
  NODE_DEFS.map((d) => ({
    type: d.type,
    name: tr(`wfUi.nodeTypes.${d.type}`, d.type),
    description: tr(`wfUi.nodePalette.${d.desc}`, d.type),
    category: tr(`wfUi.nodePanel.${d.cat}`, d.cat)
  }))
)

const NODE_COLORS: Record<string, string> = {
  Entry: '#52c41a', Exit: '#ff4d4f', Llm: '#6366f1', Selector: '#f59e0b', Batch: '#f59e0b', Break: '#f59e0b', Continue: '#f59e0b',
  Agent: '#8b5cf6', Plugin: '#14b8a6',
  Loop: '#f59e0b', SubWorkflow: '#8b5cf6', CodeRunner: '#10b981', IntentDetector: '#6366f1', QuestionAnswer: '#6366f1',
  HttpRequester: '#10b981', DatabaseQuery: '#3b82f6', DatabaseInsert: '#3b82f6', DatabaseUpdate: '#3b82f6', DatabaseDelete: '#3b82f6', DatabaseCustomSql: '#3b82f6',
  AssignVariable: '#d946ef', VariableAggregator: '#d946ef', JsonSerialization: '#84cc16', JsonDeserialization: '#84cc16', TextProcessor: '#6b7280',
  KnowledgeRetriever: '#22c55e', KnowledgeIndexer: '#22c55e', Ltm: '#22c55e',
  CreateConversation: '#ec4899', ConversationList: '#ec4899', ConversationUpdate: '#ec4899', ConversationDelete: '#ec4899', ConversationHistory: '#ec4899',
  ClearConversationHistory: '#ec4899', MessageList: '#ec4899', CreateMessage: '#ec4899', EditMessage: '#ec4899', DeleteMessage: '#ec4899',
  InputReceiver: '#ec4899', OutputEmitter: '#ec4899'
}

const NODE_ICONS: Record<string, string> = {
  Entry: '▶', Exit: '⏹', Llm: '🤖', Selector: '⟟', Loop: '↻', Batch: '▦', Break: '⏸', Continue: '↩',
  Agent: '🧠', Plugin: '🔌',
  SubWorkflow: '⊞', CodeRunner: '⌨', IntentDetector: '🎯', QuestionAnswer: '❓', HttpRequester: '🌐',
  DatabaseQuery: '🔍', DatabaseInsert: '+', DatabaseUpdate: '✎', DatabaseDelete: '✕', DatabaseCustomSql: 'SQL',
  AssignVariable: '=', VariableAggregator: '∪', JsonSerialization: '{}', JsonDeserialization: '{}', TextProcessor: 'T',
  KnowledgeRetriever: '📚', KnowledgeIndexer: '🧩', Ltm: '🧠',
  CreateConversation: '💬', ConversationList: '☰', ConversationUpdate: '✎', ConversationDelete: '✕', ConversationHistory: '🕘',
  ClearConversationHistory: '🧹', MessageList: '☰', CreateMessage: '+', EditMessage: '✎', DeleteMessage: '✕',
  InputReceiver: '⇣', OutputEmitter: '⇡'
}

function getNodeColor(type: string) { return NODE_COLORS[type] ?? '#6b7280' }
function getNodeIcon(type: string) { return NODE_ICONS[type] ?? '□' }

const filteredCategories = computed(() => {
  const filtered = searchText.value
    ? allNodes.value.filter(n =>
        n.name.includes(searchText.value) ||
        n.description.includes(searchText.value) ||
        n.type.toLowerCase().includes(searchText.value.toLowerCase())
      )
    : allNodes.value

  const map = new Map<string, NodeItem[]>()
  for (const node of filtered) {
    if (!map.has(node.category)) map.set(node.category, [])
    map.get(node.category)!.push(node)
  }

  return Array.from(map.entries()).map(([name, nodes]) => ({ name, nodes }))
})

function handleDragStart(nodeType: string) {
  emit('drag-start', nodeType)
}
</script>

<style scoped>
.node-panel {
  width: clamp(176px, 16vw, 220px);
  background: linear-gradient(180deg, #161b22 0%, #111820 100%);
  border-right: 1px solid #2a3440;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  flex-shrink: 0;
}

.panel-header {
  padding: 12px;
  border-bottom: 1px solid #2a3440;
  background: rgba(13, 17, 23, 0.75);
  backdrop-filter: blur(4px);
}

.panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 8px 0;
  scrollbar-width: thin;
  scrollbar-color: #3b4755 #121a23;
}

.node-category {
  margin-bottom: 8px;
}

.category-title {
  padding: 4px 12px;
  font-size: 11px;
  font-weight: 700;
  color: #7d8590;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.category-nodes {
  padding: 0 8px;
}

.node-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px;
  border-radius: 6px;
  border: 1px solid transparent;
  cursor: grab;
  transition: background 0.15s, border-color 0.15s;
  margin-bottom: 2px;
}

.node-item:hover {
  background: #1b2734;
  border-color: #2f3b4a;
}

.node-item:active {
  cursor: grabbing;
}

.node-item-icon {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  flex-shrink: 0;
}

.node-item-info {
  flex: 1;
  min-width: 0;
}

.node-item-name {
  font-size: 13px;
  font-weight: 600;
  color: #e6edf3;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.node-item-desc {
  font-size: 11px;
  color: #8f99a6;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.panel-body::-webkit-scrollbar {
  width: 10px;
}

.panel-body::-webkit-scrollbar-track {
  background: #121a23;
}

.panel-body::-webkit-scrollbar-thumb {
  background: #3b4755;
  border-radius: 999px;
  border: 2px solid #121a23;
}

.panel-body::-webkit-scrollbar-thumb:hover {
  background: #4a5a6c;
}

:deep(.panel-header .ant-input-affix-wrapper) {
  background: #0f1822 !important;
  border-color: #2f3b4a !important;
  color: #e6edf3 !important;
}

:deep(.panel-header .ant-input-affix-wrapper .ant-input) {
  background: transparent !important;
  color: #e6edf3 !important;
}

:deep(.panel-header .ant-input-affix-wrapper .ant-input::placeholder) {
  color: #8f99a6;
}

:deep(.panel-header .ant-input-prefix) {
  color: #8f99a6;
}
</style>
